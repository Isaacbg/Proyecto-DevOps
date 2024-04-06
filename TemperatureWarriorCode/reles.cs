using System.Threading.Tasks;
using Meadow;
using Meadow.Devices;
using Meadow.Hardware;

namespace TemperatureWarriorCode
{
    public class MeadowApp : App<F7Micro>
      // Define the pin that the relay is connected to
      IDigitalOutputPort relayPort;

      public MeadowApp()
        {
            Initialize.().Wait();
            CycleRelay().Wait();
        }
      
      async Task Initialize()
        {
            // Create the relay port, in the case going to D02
            relayPort = Device.CreateDigitalOutputPort(Device.Pins.D02);
        }
      // Function to cycle the relay on and off, every 2 seconds
      async Task CycleRelay()
        {
            while (true)
            {
                // Turn the relay on
                relayPort.State = true;
                Console.WriteLine("Relay On");
                await Task.Delay(2000);
                // Turn the relay off
                relayPort.State = false;
                Console.WriteLine("Relay Off");
                await Task.Delay(2000);
            }
        }
    }
}
