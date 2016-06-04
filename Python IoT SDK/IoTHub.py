import requests
import time
import base64
import hashlib
import hmac
import sys
import urllib
import json
import os

from constants import (
    _USER_AGENT_STRING,
    DEFAULT_RULE_NAME,
    AZURE_IOT_NAMESPACE,
    AZURE_IOT_ACCESS_KEY,
    AZURE_IOT_SHARED_ACCESS_KEY_NAME,
    AZURE_IOT_DEVICEID,
    AZURE_IOT_DEVICEKEY,
    IOT_HUB_BASE,
    DEFAULT_HTTP_TIMEOUT,
    API_VERSION
)

from _http import (
    HTTPError,
    HTTPRequest,
)
from _http.httpclient import (
    _HTTPClient)

from _common_conversion import (
    _decode_base64_to_bytes,
    _encode_base64,
    _sign_string,
)

from _common_error import (
    _validate_not_none,
    _general_error_handler,
)

from _common_serialization import (
    _get_request_body_bytes_only,
)

from _serialization import (
    _iot_hub_error_handler,
)

from urllib.parse import quote as url_quote

class IoTHub(object):
    def __init__(self, iot_hub_name=None, device_id=None, shared_access_key_name=None, shared_access_key_value=None,
                authentication=None, timeout=DEFAULT_HTTP_TIMEOUT, request_session=None):
        
        '''
        Initialises the iot hub for a namespace with the specified authentication settings (SAS)
        
        iot_hub_name:
            The name of the IoT Hub
        device_id: 
            The name of the IoT Device
        shared_access_key_name: 
            The name of the shared access policy
        shared_access_key_value:
            SAS authentication key value for the policy
        authentication:
            Instance of the authentication class. If this is specified then the SAS parameters are ignored.
        timeout: 
            Optional. Timeout for the http request, in seconds
        request_session:
            Optional. Session object to use for http request
        '''
        
        self.requestid = None
        self.iot_hub_name = iot_hub_name
        self.device_id = device_id
        
        if not self.iot_hub_name:
            self.iot_hub_name = os.environ.get(AZURE_IOT_NAMESPACE)
            
        if not self.iot_hub_name:
            raise ValueError('You need to provide the IoT hub name')
            
        if not self.device_id:
            self.device_id = os.environ.get(AZURE_IOT_DEVICEID)
            
        if not self.device_id:
            raise ValueError('You need to provide the device id')
            
        if authentication:
            self.authentication = authentication
        else:
            if shared_access_key_name and shared_access_key_value:
                self.authentication =  IoTHubSASAuthentication(
                    shared_access_key_name,
                    shared_access_key_value,
                    self._get_host()
                )
            else:
                raise ValueError('You need to provide iot hub access key and value')
               
        self._httpclient = _HTTPClient(
            service_instance=self,
            timeout=timeout,
            request_session=request_session or requests.Session(),
            user_agent=_USER_AGENT_STRING,
        )
        
        self._filter = self._httpclient.perform_request
        
    def set_proxy(self, host, port, user=None, password=None):
        '''
        Sets the proxy server host and port for the HTTP CONNECT Tunnelling.

        host:
            Address of the proxy. Ex: '192.168.0.100'
        port:
            Port of the proxy. Ex: 6000
        user:
            User for proxy authorization.
        password:
            Password for proxy authorization.
        '''
        self._httpclient.set_proxy(host, port, user, password)

    @property
    def timeout(self):
        return self._httpclient.timeout

    @timeout.setter
    def timeout(self, value):
        self._httpclient.timeout = value

    def send_device_to_cloud_message(self, message):
        request = HTTPRequest()
        request.method = 'POST'
        request.host = self._get_host()
        request.path = '/devices/' + self.device_id + '/messages/events?api-version=' + API_VERSION
        request.body = _get_request_body_bytes_only(message)
        request.path, request.query = self._httpclient._update_request_uri_query(request)
        request.headers = self._update_iot_hub_header(request)
        self._perform_request(request)
        
    def get_cloud_to_device_message(self, complete):
        request = HTTPRequest()
        request.method = 'GET'
        request.host = self._get_host()
        request.path = '/devices/' + self.device_id + '/messages/devicebound?api-version=' + API_VERSION
        request.path, request.query = self._httpclient._update_request_uri_query(request)
        request.headers = self._update_iot_hub_header(request)
        response = self._perform_request(request)
        
        if complete == 1:
            # Send an acknowledge back to the IoT Hub
            for key, name in response.headers:
                if key == 'iothub-sequencenumber':
                    sequenceId = name
                    print(sequenceId)
                elif key == 'etag':
                    etag = name[1:]
                    etag = etag[:-1]
                    print(etag)
        
            self.complete_cloud_to_device_message(sequenceId, etag)
        
        return response
    
    # Needs more work
    def complete_cloud_to_device_message(self, sequenceId, messageLockId):
        request = HTTPRequest()
        request.method = 'DELETE'
        request.host = self._get_host()
        request.path = '/devices/' + self.device_id + '/messages/devicebound/' + sequenceId + '?api-version=' + API_VERSION
        request.path, request.query = self._httpclient._update_request_uri_query(request)
        request.headers = self._update_iot_hub_header(request)
        request.headers.append(('If-Match', messageLockId))
        
        self._perform_request(request)
        
    def _get_host(self):
        return self.iot_hub_name + IOT_HUB_BASE
        
    def _update_iot_hub_header(self, request):
        ''' Add additional headers for IoT Hub. '''

        if request.method in ['PUT', 'POST', 'MERGE', 'DELETE']:
            request.headers.append(('Content-Length', str(len(request.body))))

        # if it is not GET or HEAD request, must set content-type.
        if not request.method in ['GET', 'HEAD']:
            for name, _ in request.headers:
                if 'content-type' == name.lower():
                    break
            else:
                request.headers.append(
                    ('Content-Type',
                     'application/atom+xml;type=entry;charset=utf-8'))

        # Adds authorization header for authentication.
        self.authentication.sign_request(request, self._httpclient)

        return request.headers
        
    def _perform_request(self, request):
        try:
            resp = self._filter(request)
        except HTTPError as ex:
            return _iot_hub_error_handler(ex)

        return resp
        
class IoTHubSASAuthentication:
    def __init__(self, key_name, key_value, host):
        self.key_name = key_name
        self.key_value = key_value
        self.host = host
    
    def sign_request(self, request, httpclient):
        request.headers.append(
            ('Authorization', self._get_authorization(request, httpclient)))

    def _get_authorization(self, request, httpclient):
        uri = self.host
        uri = url_quote(uri, '').lower()
        expiry = str(self._get_expiry())

        to_sign = uri + '\n' + expiry
        signature = url_quote(_sign_string(self.key_value, to_sign, 1))

        auth_format = 'SharedAccessSignature sig={0}&se={1}&skn={2}&sr={3}'
        auth = auth_format.format(signature, expiry, self.key_name, self.host)
        return auth

    def _get_expiry(self):
        '''Returns the UTC datetime, in seconds since Epoch, when this signed 
        request expires (5 minutes from now).'''
        return int(round(time.time() + 300))
    