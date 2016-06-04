import sys
import thread
import time
import RPi.GPIO as GPIO
import bluetooth._bluetooth as bluez

from Python_IoT_SDK.IoTHub import (
    IoTHub
)

import ibeaconscan

# IBeacon settings
IBEACON_UDID = 'b9407f30f5f8466eaff925556b57fe6d'
IBEACON_MAJOR = 59267
IBEACON_MINOR = 12181

# Azure IoT Hub settings
AZURE_IOT_NAMESPACE = ''
AZURE_IOT_ACCESS_KEY = ''
AZURE_IOT_SHARED_ACCESS_KEY_NAME = 'device'
AZURE_IOT_DEVICEID = ''

# GPIO settings
LEDPIN = 11

def _flash(pin):
    for i in range(0,50):
        GPIO.output(pin, GPIO.High)
        time.sleep(1)
        GPIO.output(pin, GPIO.LOW)
        time.sleep(1)
        print 'flash'
    return
    
# Initialise GPIO
GPIO.setmode(GPIO.BOARD)
GPIO.setup(LEDPIN, GPIO.OUT)

# Initialise IoTHub
hub = IoTHub(AZURE_IOT_NAMESPACE, shared_access_key_name=AZURE_IOT_SHARED_ACCESS_KEY_NAME, shared_access_key_value=AZURE_IOT_ACCESS_KEY, device_id=AZURE_IOT_DEVICEID)

# Initialise blue tooth adapter scanning
dev_id = 0
try:
    sock = bluez.hci_open_dev(dev_id)
    print 'ble thread started'

except:
    print 'error accessing bluetooth device...'
    sys.exit(1)
        
ibeaconscan.hci_le_set_scan_parameters(sock)
ibeaconscan.hci_enable_le_scan(sock)

# Scan for ibeacons
while True:
    returnedList = ibeaconscan.parse_events(sock, 10)
    for beacon in returnedList:
        if IBEACON_UDID == beacon.udid:
            if IBEACON_MAJOR == beacon.major:
                print 'found' + str(beacon.rssi)
                # Send data to IoTHub
                hub.send_device_to_cloud_message('{\'udid\' : ' + beacon.UDID + ', \'major\' : ' + beacon.major + ', \'minor\' : ' + beacon.minor + ', \'rssi\' : ' + beacon.rssi +' }')
                              
    # Check for device to cloud messages
    response = hub.get_cloud_to_device_message(0)
    if "flash" in response.body:
        #print 'Flashing'
        thread.start_new_thread(flash, (LEDPIN))
        flash(LEDPIN)
    

