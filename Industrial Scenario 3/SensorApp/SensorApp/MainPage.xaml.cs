﻿using Microsoft.Azure.Devices.Client;
using System;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using SensorApp.Common;
using GIS = GHIElectronics.UWP.Shields;

namespace SensorApp
{
    public sealed partial class MainPage : BindablePage
    {
        private const string IOTHUBCONNECTIONSTRING = "HostName=<IoTHubName>.azure-devices.net;DeviceId=<DeviceName>;SharedAccessKey=<SharedAccessKey>";
        private const int INITIALREADINGPERIOD = 10;
        private const int LEDPIN = 27;
        private const int DHTPIN = 4;

        // GPIO 
        private GpioPin dhtPin = null;
        private GpioPin ledPin = null;
        private GpioPinValue ledPinValue = GpioPinValue.High;
        
        // IOT Hub
        private DeviceClient deviceClient;

        // Sensor readings offsets and poll interval
        private int temperatureOffset = 1;
        private int lightLevelOffset = 1;
        private int sensorInterval = 2;
        private DispatcherTimer sensorTimer = new DispatcherTimer();
        private bool temperatureOffsetEnabled = true;
        private bool lightLevelOffsetEnabled = true;
        private DateTimeOffset readingsStartedAt = DateTime.MinValue;

        // Flash
        private DispatcherTimer flashTimer = new DispatcherTimer();
        private int flashInterval = 1;
        private int flashDuration = 10;
        private DateTimeOffset flashStartedAt = DateTime.MinValue;

        private GIS.FEZHAT hat;

        // Properties
        private float _humidity = 0f;
        public float Humidity
        {
            get
            {
                return _humidity;
            }

            set
            {
                this.SetProperty(ref _humidity, value);
                this.OnPropertyChanged(nameof(HumidityDisplay));
            }
        }

        public string HumidityDisplay
        {
            get
            {
                return string.Format("{0:0.0}% RH", this.Humidity);
            }
        }

        private float _temperature = 0f;
        public float Temperature
        {
            get
            {
                return _temperature;
            }
            set
            {
                this.SetProperty(ref _temperature, value);
                this.OnPropertyChanged(nameof(TemperatureDisplay));
            }
        }

        public string TemperatureDisplay
        {
            get
            {
                return string.Format("{0:0.0} °C", this.Temperature);
            }
        }

        private string _log;
        public string Log
        {
            get
            {
                return _log;
            }
            set
            {
                this.SetProperty(ref _log, value);
                this.OnPropertyChanged(nameof(LogDisplay));
            }
        }

        public string LogDisplay
        {
            get
            {
                return this.Log;
            }
        }

        public MainPage()
        {
            this.InitializeComponent();

            try
            {
                // InitialiseGPIO();
                Setup();
           
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private async void Setup()
        {
            sensorTimer.Interval = TimeSpan.FromSeconds(sensorInterval);
            sensorTimer.Tick += sensorTimer_Tick;
            flashTimer.Interval = TimeSpan.FromSeconds(flashInterval);
            flashTimer.Tick += flashTimer_Tick;

            this.hat = await GIS.FEZHAT.CreateAsync();

            //deviceClient = DeviceClient.CreateFromConnectionString(IOTHUBCONNECTIONSTRING);
            //ReceiveCommands();

            sensorTimer.Start();

            readingsStartedAt = DateTimeOffset.Now;
        }


        private void InitialiseGPIO()
        {
            GpioController gpioController = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpioController == null)
            {
                System.Diagnostics.Debug.WriteLine("Unable to create GPIO controller");
                return;
            }
            ledPin = gpioController.OpenPin(LEDPIN);
            ledPin.SetDriveMode(GpioPinDriveMode.Output);
        }

        private void sensorTimer_Tick(object sender, object e)
        {
            readSensors();
        }

        private async void readSensors()
        {
            double temperature = this.hat.GetTemperature();
            double lightLevel = this.hat.GetLightLevel();

            TimeSpan elapsed = DateTimeOffset.Now.Subtract(readingsStartedAt);

            // Check initial reading period has elapsed
            if (elapsed.Seconds > INITIALREADINGPERIOD)
            {
                if (temperatureOffsetEnabled == true)
                {
                    temperature += temperatureOffset;
                    temperatureOffset += 1;
                }
                if (lightLevelOffsetEnabled == true)
                {
                    lightLevel += lightLevelOffset;
                    // Only increment humidity to 100 as it's a percentage
                    if (lightLevel < 100)
                    {
                        lightLevelOffset += 1;
                    }

                }

                // Display data and send to IoT Hub
                await SendDeviceToCloudMessage(temperature, lightLevel);

                this.Temperature = Convert.ToSingle(temperature);
                this.Humidity = Convert.ToSingle(lightLevel);
                this.Log = string.Format("Read temperature: {0}, light level: {1}", temperature, lightLevel);
                this.OnPropertyChanged(nameof(TemperatureDisplay));
                this.OnPropertyChanged(nameof(HumidityDisplay));
                this.OnPropertyChanged(nameof(LogDisplay));
            }
        }

        private void flashTimer_Tick(object sender, object e)
        {
            if (DateTimeOffset.Now.Subtract(flashStartedAt).Seconds > flashDuration)
            {
                flashTimer.Stop();
                ledPinValue = GpioPinValue.High;
                ledPin.Write(ledPinValue);
            }
            else
            {
                if (ledPinValue == GpioPinValue.Low)
                {
                    ledPinValue = GpioPinValue.High;
                }
                else
                {
                    ledPinValue = GpioPinValue.Low;
                }

                ledPin.Write(ledPinValue);
            }
          
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        private async void ReceiveCommands()
        {

            while (true)
            {
                Message receivedMessage = await deviceClient.ReceiveAsync();

                if (receivedMessage != null)
                {
                    string messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                    this.Log = string.Format("{0}> Received message: {1}", DateTime.Now.ToLocalTime(), messageData);
                    this.OnPropertyChanged(nameof(LogDisplay));
                    string command = messageData;
                    if (command.Contains(":"))
                    {
                        command = command.Substring(0, command.IndexOf(":"));
                    }

                    switch (command)
                    {
                        case "resettemperature":
                            temperatureOffset = 0;
                            break;
                        case "resethumidity":
                            lightLevelOffset = 0;
                            break;
                        case "resetall":
                            temperatureOffset = 0;
                            lightLevelOffset = 0;
                            break;
                        case "changepoll":
                            sensorInterval = Convert.ToInt32(messageData.Substring(messageData.IndexOf(":") +1, messageData.Length - (messageData.IndexOf(":") + 1)));
                            sensorTimer.Interval = TimeSpan.FromSeconds(sensorInterval);
                            break;
                        case "toggletemperatureoffsetenabled":
                            temperatureOffsetEnabled = !temperatureOffsetEnabled;
                            break;
                        case "togglehumidityoffsetenabled":
                            lightLevelOffsetEnabled = !lightLevelOffsetEnabled;
                            break;
                        case "flash":
                            if (flashTimer.IsEnabled == false)
                            {
                                flashStartedAt = DateTime.Now;
                                flashTimer.Start();
                            }
                            break;
                        default:
                            break;
                    }
                    await deviceClient.CompleteAsync(receivedMessage);
                    messageData = null;
                }
                await Task.Delay(1000);
                receivedMessage = null;
            }
        }

        private async Task SendDeviceToCloudMessage(double temperature, double humidity)
        {
            try
            {
                string message = "{\"temperature\":" + temperature.ToString() + ", \"humidity\":" + humidity.ToString() + "}";

                Message eventMessage = new Message(Encoding.UTF8.GetBytes(message));

                DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(IOTHUBCONNECTIONSTRING);
                await deviceClient.SendEventAsync(eventMessage);
                deviceClient = null;

                eventMessage = null;
                message = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
    }
}
