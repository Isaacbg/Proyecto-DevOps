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


namespace TemperatureWarriorCode
{
    public class MeadowApp : App<F7FeatherV2>
    {

        //Temperature Sensor
        AnalogTemperature sensor;

        //Time Controller Values
        public static int total_time = 0;
        public static int total_time_in_range = 0;
        public static int total_time_out_of_range = 0;

        public int count = 0;

        //Initialize PID controller with appropriate gains and limits
        public static float Kp = 1f;
        public static float Ki = 0.5f;
        public static float Kd = 0.2f;
        public static int outputUpperLimit = 50;
        public static int outputLowerLimit = -50;

        //IDigitalOutputPort switchPort = Device.CreateDigitalOutputPort(Device.Pins.D02);
        public static Frequency frequency = new Frequency(20, Frequency.UnitType.Hertz);
        public static IPwmPort switchPort;
        public static IDigitalOutputPort port1;
        public static IDigitalOutputPort port2;

        public override async Task Run()
        {
            if (count == 0)
            {
                Console.WriteLine("Initialization...");

                switchPort = Device.CreatePwmPort(Device.Pins.D02, frequency, 0);
                port1 = Device.CreateDigitalOutputPort(Device.Pins.D03);
                port2 = Device.CreateDigitalOutputPort(Device.Pins.D04);

                // TODO uncomment when needed 
                // Temperature Sensor Configuration
                sensor = new AnalogTemperature(analogPin: Device.Pins.A01, sensorType: AnalogTemperature.KnownSensorType.TMP36);
                sensor.TemperatureUpdated += AnalogTemperatureUpdated;
                sensor.StartUpdating(TimeSpan.FromSeconds(0.1));

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
        public static void StartRound()
        {

            total_time = 0;

            // Initialize PID controller
            StandardPidController standardPidController = new StandardPidController
            {
                OutputMax = outputUpperLimit,
                OutputMin = outputLowerLimit,
                ProportionalComponent = Kp,
                IntegralComponent = Ki,
                DerivativeComponent = Kd,
                OutputTuningInformation = true,

            };


            // Initialize reles
            //Rele rele_switch = new Rele(switchPort);
            Rele rele1 = new Rele(port1);
            Rele rele2 = new Rele(port2);

            // Turn on the relay
            switchPort.DutyCycle = 1;
            switchPort.Start();
            //rele_switch.TurnOn();

            // Change polarity of the relay

            //Controller variables

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
            for (int i = 0; i < Data.temp_min.Length; i++)
            {
                temperatureRanges[i] = new TemperatureRange(double.Parse(Data.temp_min[i]), double.Parse(Data.temp_max[i]), int.Parse(Data.round_time[i]) * 1000);
                total_time += int.Parse(Data.round_time[i]);
            }

            //Initialization of timecontroller with the ranges
            timeController.DEBUG_MODE = false;
            success = timeController.Configure(temperatureRanges, total_time * 1000, Data.refresh, out error_message);
            Console.WriteLine(error_message);
            Console.WriteLine(success);

            //Initialization of timer
            Thread t = new Thread(Timer);
            t.Start();

            Stopwatch regTempTimer = new Stopwatch();
            timeController.StartOperation(); // aquí se inicia el conteo en la librería de control
            regTempTimer.Start();

            Console.WriteLine("STARTING");

            var currentperiod = Data.current_period;
            float min_temp = float.Parse(Data.temp_min[Data.current_period]);
            float max_temp = float.Parse(Data.temp_max[Data.current_period]);
            standardPidController.TargetInput = (min_temp + max_temp) / 2;
            float output = 0;
            //THE TW START WORKING
            while (Data.is_working)
            {

                //This is the time refresh we did not do before
                Thread.Sleep(Data.refresh - sleep_time);

                if (currentperiod != Data.current_period)
                {
                    // Get current target temperature (Getting min and max temp and getting mean value)
                    standardPidController.ResetIntegrator();
                    min_temp = float.Parse(Data.temp_min[Data.current_period]);
                    max_temp = float.Parse(Data.temp_max[Data.current_period]);
                    standardPidController.TargetInput = (min_temp + max_temp) / 2;
                    Console.WriteLine("Updating for period: " + Data.current_period);
                }
                
                // Get actual sensor temperature
                Console.WriteLine("Target: " + standardPidController.TargetInput);
                Console.WriteLine("Temp: " + Data.temp_act.ToString());
                standardPidController.ActualInput = float.Parse(Data.temp_act);
                

                // Get output from PID controller
                output = standardPidController.CalculateControlOutput();
                Console.WriteLine("PID: " + output.ToString());
                //Get voltage to apply

                if (output < 0 || (output == 50 && float.Parse(Data.temp_act) > max_temp && Data.refresh < 1500))
                { 
                    Console.WriteLine("Enfriando");
                    switchPort.DutyCycle = 1;
                    switchPort.Start();
                    //rele_switch.TurnOn();
                    // Change polarity

                    
                    Thread.Sleep(10);
                    rele1.TurnOn();
                    rele2.TurnOn();

                    //switchPort.DutyCycle = Math.Abs(output)/50;
                    //switchPort.Start();
                    //rele_switch.TurnOn();
                    
                }
                else if (output > 0 || (output == -50 && float.Parse(Data.temp_act) < min_temp && Data.refresh < 1500))
                {
                    Console.WriteLine("Calentando");

                    switchPort.DutyCycle = Math.Abs(output)/100;
                    switchPort.Start();
                    //rele_switch.TurnOn();
                    
                    //switchPort.Stop();
                    //rele_switch.TurnOff();
                    // Sleep 10 ms
                    Thread.Sleep(10);
                    rele1.TurnOff();
                    rele2.TurnOff();

                    //switchPort.DutyCycle = Math.Abs(output) / 50;
                    //switchPort.Start();
                    //rele_switch.TurnOn();
                    
                }
                else
                {
                    //Stop heating or cooling
                    switchPort.Stop();
                    //rele_switch.TurnOff();
                }

                Console.WriteLine($"Temperature each interval={Data.temp_act}");

                // Temperature registration algorithm
                if (Data.interval_temps.Count > 0) {
                    // If any value is within the min and max temp
                    double matchingTemp = Data.interval_temps.Find(x => x >= min_temp && x <= max_temp);
                    if (matchingTemp != 0) {
                        timeController.RegisterTemperature(matchingTemp);
                        Console.WriteLine($"Temp within range = {matchingTemp}");
                    } else {
                        // If no value is within the min and max temp get the mean value
                        double mean = Data.interval_temps.Average();
                        timeController.RegisterTemperature(mean);
                        Console.WriteLine($"Mean temp value = {mean}");
                    }
                } else {
                    Console.WriteLine($"RegTempTimer={regTempTimer.Elapsed.ToString()}, enviando Temp={Data.temp_act}");
                    timeController.RegisterTemperature(double.Parse(Data.temp_act));
                }
                // Clear interval temperatures
                Data.interval_temps.Clear();

                
                regTempTimer.Restart();
                currentperiod = Data.current_period;

            }
            Console.WriteLine("Round Finish");
            switchPort.Stop();
            rele1.TurnOn();
            rele2.TurnOn();
            //rele_switch.TurnOff();
            // Reset PID controller
            //standardPidController.ResetIntegrator();
            t.Abort();

            total_time_in_range = timeController.TimeInRangeInMilliseconds;
            total_time_out_of_range = timeController.TimeOutOfRangeInMilliseconds;
            Data.time_in_range_temp = (timeController.TimeInRangeInMilliseconds / 1000);

            Console.WriteLine("Tiempo dentro del rango " + (((double)timeController.TimeInRangeInMilliseconds / 1000)) + " s de " + total_time + " s");
            Console.WriteLine("Tiempo fuera del rango " + ((double)total_time_out_of_range / 1000) + " s de " + total_time + " s");

            timeController.FinishOperation();
            switchPort.Dispose();
            port1.Dispose();
            port2.Dispose();
        }

        //Round Timer
        private static void Timer()
        {
            Data.is_working = true;
            for (int i = 0; i < Data.round_time.Length; i++)
            {
                Data.time_left = int.Parse(Data.round_time[i]);
                Data.current_period = i;

                while (Data.time_left > 0)
                {
                    Data.time_left--;
                    Thread.Sleep(1000);
                }
            }
            Data.is_working = false;
        }

        //Temperature and Display Updated
        void AnalogTemperatureUpdated(object sender, IChangeResult<Meadow.Units.Temperature> e)
        {
            // New temperature
            double temp = Math.Round((Double)e.New.Celsius, 2);
            // Add temperature to the list
            Data.interval_temps.Add(temp);
            // Update actual temperature
            Data.temp_act = Math.Round((Double)e.New.Celsius, 2).ToString();
            if (double.Parse(Data.temp_act) > 55) {
                // End the round
                Data.is_working = false;
                // Stop the relay
                switchPort.DutyCycle = 0;
                switchPort.Stop();
                // End the program
                Console.WriteLine("Temperature too high, ending round");
                Environment.Exit(0);
            }
        }

        void WiFiAdapter_WiFiConnected(object sender, EventArgs e)
        {
            if (sender != null)
            {
                Console.WriteLine($"Connecting to WiFi Network {Secrets.WIFI_NAME}");
            }
        }

        void WiFiAdapter_ConnectionCompleted(object sender, EventArgs e)
        {
            Console.WriteLine("Connection request completed.");
        }

        protected WifiNetwork ScanForAccessPoints(string SSID)
        {
            WifiNetwork wifiNetwork = null;
            ObservableCollection<WifiNetwork> networks = new ObservableCollection<WifiNetwork>(Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>().Scan()?.Result?.ToList()); //REVISAR SI ESTO ESTA BIEN
            wifiNetwork = networks?.FirstOrDefault(x => string.Compare(x.Ssid, SSID, true) == 0);
            return wifiNetwork;
        }
    }
}