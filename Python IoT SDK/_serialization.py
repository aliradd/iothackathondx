
from _common_error import (
    _general_error_handler,
)

def _iot_hub_error_handler(http_error):
        ''' Simple error handler for IoT hub. '''
        return _general_error_handler(http_error)