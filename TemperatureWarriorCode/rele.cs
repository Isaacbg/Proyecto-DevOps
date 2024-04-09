using System.Threading.Tasks;
using Meadow;
using Meadow.Devices;
using Meadow.Hardware;

namespace TemperatureWarriorCode
{
    public class Rele
    {
        // Define the pin that the relay is connected to
        IDigitalOutputPort relayPort;

       // Constructor
         public Rele(IDigitalOutputPort relayPort)
         {
              this.relayPort = relayPort;
         }
        
        // Function to turn on the relay
        public async Task TurnOn()
        {
            relayPort.State = true; // NO
            Console.WriteLine("Relay On");
        }

        // Function to turn off the relay
        public async Task TurnOff()
        {
            relayPort.State = false; // NC
            Console.WriteLine("Relay Off");
        }

        // Function to ivert the polarity of the relay
        public async Task Invert()
        {
            relayPort.State = !relayPort.State;
            Console.WriteLine("Relay Inverted");
        }
    }
}
// Path: TemperatureWarriorCode/Rele.cs