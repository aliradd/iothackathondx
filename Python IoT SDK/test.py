from IoTHub import IoTHub

hub = IoTHub('iothackathondx', shared_access_key_name='device', shared_access_key_value='+3hL6b1Ecd7NYl64/NE8vbHbnp91ji/JeRjQdvo3JTc=', device_id='testdevice')

# Send message
hub.send_device_to_cloud_message('Test:10')
# Get message
response = hub.get_cloud_to_device_message(1)
print (response.body)
print (response.status)
print (response.message)

for key, name in response.headers:
    print(key)
    print(name)
