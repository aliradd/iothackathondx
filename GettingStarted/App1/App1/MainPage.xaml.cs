using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Sensors.Dht;
using Microsoft.Azure.Devices.Client;
using Windows.Devices.Gpio;
using System.Text;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace App1
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string IOTHUBCONNECTIONSTRING = "HostName=<IoTHubName>.azure-devices.net;DeviceId=<DeviceID>;SharedAccessKey=<SharedAccessKey>";
        private const int DHTPIN = 4;
        private IDht dht = null;
        private GpioPin dhtPin = null;
        private DispatcherTimer sensorTimer = new DispatcherTimer();

        public MainPage()
        {
            this.InitializeComponent();

            dhtPin = GpioController.GetDefault().OpenPin(DHTPIN, GpioSharingMode.Exclusive);
            dht = new Dht11(dhtPin, GpioPinDriveMode.Input);
            sensorTimer.Interval = TimeSpan.FromSeconds(10);
            sensorTimer.Tick += sensorTimer_Tick;

            sensorTimer.Start();

        }

        private void sensorTimer_Tick(object sender, object e)
        {
            readSensor();
        }

        private async void readSensor()
        {
            DhtReading reading = await dht.GetReadingAsync().AsTask();

            if (reading.IsValid)
            {
                // Send reading to IoT Hub
                string message = "{\"temperature\":" + reading.Temperature.ToString() + ", \"humidity\":" + reading.Humidity.ToString() + "}";

                Message eventMessage = new Message(Encoding.UTF8.GetBytes(message));

                DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(IOTHUBCONNECTIONSTRING);
                await deviceClient.SendEventAsync(eventMessage);
            }
        }
    }
}
