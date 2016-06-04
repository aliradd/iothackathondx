from ._common_models import (
    WindowsAzureData,
    _unicode_type
)

def _get_request_body_bytes_only(param_value):
    '''Validates the request body passed in and converts it to bytes
    if our policy allows it.'''
    if param_value is None:
        return b''

    if isinstance(param_value, bytes):
        return param_value

    return _get_request_body(param_value)
    
def _get_request_body(request_body):
    '''Converts an object into a request body.  If it's None
    we'll return an empty string, if it's one of our objects it'll
    convert it to XML and return it.  Otherwise we just use the object
    directly'''
    if request_body is None:
        return b''

    if isinstance(request_body, WindowsAzureData):
        request_body = _convert_class_to_xml(request_body)

    if isinstance(request_body, bytes):
        return request_body

    if isinstance(request_body, _unicode_type):
        return request_body.encode('utf-8')

    request_body = str(request_body)
    if isinstance(request_body, _unicode_type):
        return request_body.encode('utf-8')

    return request_body