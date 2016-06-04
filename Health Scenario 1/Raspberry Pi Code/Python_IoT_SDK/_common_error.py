_ERROR_VALUE_NONE = '{0} should not be None.'

def _validate_not_none(param_name, param):
    if param is None:
        raise ValueError(_ERROR_VALUE_NONE.format(param_name))
        
def _general_error_handler(http_error):
    ''' Simple error handler for azure.'''
    message = str(http_error)
    if http_error.respbody is not None:
        message += '\n' + http_error.respbody.decode('utf-8-sig')
    raise AzureHttpError(message, http_error.status)

class AzureException(Exception):
    pass
    
class AzureHttpError(AzureException):
    def __init__(self, message, status_code):
        super(AzureHttpError, self).__init__(message)
        self.status_code = status_code

    def __new__(cls, message, status_code, *args, **kwargs):
        if cls is AzureHttpError:
            if status_code == 404:
                cls = AzureMissingResourceHttpError
            elif status_code == 409:
                cls = AzureConflictHttpError
        return AzureException.__new__(cls, message, status_code, *args, **kwargs)