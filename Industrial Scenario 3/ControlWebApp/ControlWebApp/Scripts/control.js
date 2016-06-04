function sendCloudToDeviceMessage(message)
{
    $.post('/api/iothub/', { '': message })
        .done(function (data) {
        });
}