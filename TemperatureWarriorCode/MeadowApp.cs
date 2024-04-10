using Meadow;
using Meadow.Foundation;
using Meadow.Foundation.Sensors.Temperature;
using Meadow.Foundation.Graphics;
using Meadow.Foundation.Displays;
using Meadow.Devices;
using Meadow.Hardware;
using Meadow.Gateway.WiFi;
using Meadow.Units;
using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using TemperatureWarriorCode.Web;
using NETDuinoWar;
using Meadow.Foundation.Controllers.Pid;


namespace TemperatureWarriorCode {
    public class MeadowApp : App<F7FeatherV2> {

        //Temperature Sensor
        AnalogTemperature sensor;

        //Time Controller Values
        public static int total_time = 0;
        public static int total_time_in_range = 0;
        public static int total_time_out_of_range = 0;

        public int count = 0;

        //Initialize PID controller with appropriate gains and limits
        public static float Kp = 0.1f;
        public static float Ki = 0.1f;
        public static float Kd = 0.05f;
        public static int outputUpperLimit = 50;
        public static int outputLowerLimit = -50;

        public override async Task Run() {
            if (count == 0) {
                Console.WriteLine("Initialization...");

                // TODO uncomment when needed 
                // Temperature Sensor Configuration
                sensor = new AnalogTemperature(analogPin: Device.Pins.A01, sensorType: AnalogTemperature.KnownSensorType.TMP36);
                sensor.TemperatureUpdated += AnalogTemperatureUpdated;
                sensor.StartUpdating(TimeSpan.FromSeconds(2));

                // TODO Local Network configuration (uncomment when needed)
                var wifi = Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>();
                wifi.NetworkConnected += WiFiAdapter_ConnectionCompleted;

                //WiFi Channel
                WifiNetwork wifiNetwork = ScanForAccessPoints(Secrets.WIFI_NAME);

                wifi.NetworkConnected += WiFiAdapter_WiFiConnected;
                await wifi.Connect(Secrets.WIFI_NAME, Secrets.WIFI_PASSWORD);

                string IPAddress = wifi.IpAddress.ToString();

                //Connnect to the WiFi network.
                Console.WriteLine($"IP Address: {IPAddress}");
                Data.IP = IPAddress;
                if (!string.IsNullOrWhiteSpace(IPAddress))
                {
                    Data.IP = IPAddress;
                    WebServer webServer = new WebServer(wifi.IpAddress, Data.Port);
                    if (webServer != null)
                    {
                        webServer.Start();
                    }
                }

                // TODO Display initialization (uncomment when needed)
                //Display();

                Console.WriteLine("Meadow Initialized!");

                count = count + 1;
            }
        }

        //TW Combat Round
        public static void StartRound() {

            // Initialize PID controller
            StandardPidController standardPidController = new StandardPidController
            {
                OutputMax = outputUpperLimit,
                OutputMin = outputLowerLimit,
                ProportionalComponent = Kp,
                IntegralComponent = Ki,
                DerivativeComponent = Kd
            };

            // Initialize reles
            Rele rele_switch = new Rele(Device.CreateDigitalOutputPort(Device.Pins.D02));
            Rele rele1 = new Rele(Device.CreateDigitalOutputPort(Device.Pins.D03));
            Rele rele2 = new Rele(Device.CreateDigitalOutputPort(Device.Pins.D04));

            // Turn on the relay
            rele_switch.TurnOn();

            // Change polarity of the relay

            //Controller variables

            bool is_heating = false;
            bool is_cooling = false;

            Stopwatch timer = Stopwatch.StartNew();
            timer.Start();

            //Value to control the time for heating and cooling
            //First iteration is 100 for the time spend creating timecontroller and thread
            int sleep_time = 20;

            //Initialization of time controller
            TimeController timeController = new TimeController();

            //Configuration of differents ranges
            TemperatureRange[] temperatureRanges = new TemperatureRange[Data.round_time.Length];

            //Range configurations
            bool success;
            string error_message = null;
            Data.is_working = true;

            //define ranges
            for (int i = 0; i < Data.temp_min.Length; i++) {
                temperatureRanges[i] = new TemperatureRange(double.Parse(Data.temp_min[i]), double.Parse(Data.temp_max[i]), int.Parse(Data.round_time[i]) * 1000);
                total_time += int.Parse(Data.round_time[i]);
            }
            
            //Initialization of timecontroller with the ranges
            timeController.DEBUG_MODE = false;
            success = timeController.Configure(temperatureRanges, total_time * 1000, Data.refresh, out error_message);
            Console.WriteLine(success);

            //Initialization of timer
            Thread t = new Thread(Timer);
            t.Start();

            Stopwatch regTempTimer = new Stopwatch();
            timeController.StartOperation(); // aquí se inicia el conteo en la librería de control
            regTempTimer.Start();

            Console.WriteLine("STARTING");

            //THE TW START WORKING
            while (Data.is_working) {

                //This is the time refresh we did not do before
                Thread.Sleep(Data.refresh - sleep_time);

                // Get current target temperature (Getting min and max temp and getting mean value)
                float min_temp = float.Parse(Data.temp_min[Data.current_period]);
                float max_temp = float.Parse(Data.temp_max[Data.current_period]);
                standardPidController.TargetInput = (min_temp + max_temp) / 2;

                Console.WriteLine("Temp: "+ Data.temp_act.ToString());
                // Get actual sensor temperature
                standardPidController.ActualInput = float.Parse(Data.temp_act);

                // Get output from PID controller
                float output = standardPidController.CalculateControlOutput();
                Console.WriteLine("PID: " + output.ToString());
                //Get voltage to apply
                if (output > 2) {
                    rele_switch.TurnOn();
                    // Change polarity
                    if (is_cooling) {
                        rele_switch.TurnOff();
                        // Sleep 10 ms
                        Thread.Sleep(10);
                        rele1.TurnOn();
                        rele2.TurnOn();
                        rele_switch.TurnOn();
                    }
                    //Heating
                    is_heating = true;
                    is_cooling = false;
                } else if (output < -2) {
                    rele_switch.TurnOn();
                    if (is_heating) {
                        rele_switch.TurnOff();
                        // Sleep 10 ms
                        Thread.Sleep(10);
                        rele1.TurnOff();
                        rele2.TurnOff();
                        rele_switch.TurnOn();
                    }
                    //Cooling
                    is_heating = false;
                    is_cooling = true;
                } else {
                    //Stop heating or cooling
                    is_heating = false;
                    is_cooling = false;
                    rele_switch.TurnOff();
                }

                // Update controller with the new voltage


                //Temperature registration
                Console.WriteLine($"RegTempTimer={regTempTimer.Elapsed.ToString()}, enviando Temp={Data.temp_act}");
                timeController.RegisterTemperature(double.Parse(Data.temp_act));
                regTempTimer.Restart();

            }
            Console.WriteLine("Round Finish");
            rele_switch.TurnOff();
            // Reset PID controller
            standardPidController.ResetIntegrator();
            t.Abort();

            total_time_in_range += timeController.TimeInRangeInMilliseconds;
            total_time_out_of_range += timeController.TimeOutOfRangeInMilliseconds;
            Data.time_in_range_temp = (timeController.TimeInRangeInMilliseconds / 1000);

            Console.WriteLine("Tiempo dentro del rango " + (((double)timeController.TimeInRangeInMilliseconds / 1000)) + " s de " + total_time + " s");
            Console.WriteLine("Tiempo fuera del rango " + ((double)total_time_out_of_range / 1000) + " s de " + total_time + " s");
        }

        //Round Timer
        private static void Timer() {
            Data.is_working = true;
            for (int i = 0; i < Data.round_time.Length; i++) {
                Data.time_left = int.Parse(Data.round_time[i]);
                Data.current_period = i;

                while (Data.time_left > 0) {
                    Data.time_left--;
                    Thread.Sleep(1000);
                }
            }
            Data.is_working = false;
        }

        //Temperature and Display Updated
        void AnalogTemperatureUpdated(object sender, IChangeResult<Meadow.Units.Temperature> e) {

           Data.temp_act = Math.Round((Double)e.New.Celsius, 2).ToString();

           //Console.WriteLine($"Temperature={Data.temp_act}");
        }

        void WiFiAdapter_WiFiConnected(object sender, EventArgs e) {
            if (sender != null) {
                Console.WriteLine($"Connecting to WiFi Network {Secrets.WIFI_NAME}");
            }
        }

        void WiFiAdapter_ConnectionCompleted(object sender, EventArgs e) {
            Console.WriteLine("Connection request completed.");
        }

        protected WifiNetwork ScanForAccessPoints(string SSID) {
            WifiNetwork wifiNetwork = null;
            ObservableCollection<WifiNetwork> networks = new ObservableCollection<WifiNetwork>(Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>().Scan()?.Result?.ToList()); //REVISAR SI ESTO ESTA BIEN
            wifiNetwork = networks?.FirstOrDefault(x => string.Compare(x.Ssid, SSID, true) == 0);
            return wifiNetwork;
        }
    }
}