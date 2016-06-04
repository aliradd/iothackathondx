__author__ = 'James Brand'
__version__ = '0.1.1'

_USER_AGENT_STRING = 'pyazure/' + __version__

# default rule name for subscription
DEFAULT_RULE_NAME = '$Default'

#-----------------------------------------------------------------------------
# Constants for Azure app environment settings.
AZURE_IOT_NAMESPACE = 'iothackathondx'
AZURE_IOT_ACCESS_KEY = 'wLm/ZZDuLPVQgQ4ZjE2oGC+yLJtfewtENXcOqYuupc4='
AZURE_IOT_SHARED_ACCESS_KEY_NAME = 'iothubowner'
AZURE_IOT_DEVICEID = 'testdevice'
AZURE_IOT_DEVICEKEY = 'zGrqyBdClrPDfLFdkjr3pKNYtYA4hWKC4iaAqTkDFM0='

# Live URLs
IOT_HUB_BASE = '.azure-devices.net'

# Default timeout for http requests (in secs)
DEFAULT_HTTP_TIMEOUT = 65

# API Version
API_VERSION = '2015-08-15-preview'

#HostName=iothackathondx.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=wLm/ZZDuLPVQgQ4ZjE2oGC+yLJtfewtENXcOqYuupc4=