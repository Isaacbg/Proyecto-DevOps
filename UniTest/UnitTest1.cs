using Moq;
using TemperatureWarriorCode;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Meadow.Hardware;

namespace UnitTest
{
    [TestClass]
    public class ReleTests
    {
        [TestMethod]
        public async Task TurnOn_ShouldSetRelayToTrue()
        {
            // Declaramos las variables necesarias para el test
            var mockOutputPort = new Mock<IDigitalOutputPort>();
            var rele = new Rele(mockOutputPort.Object);

            // Ponemos el rele en on
            await rele.TurnOn();

            // Comprobamos que el rele esta en on
            mockOutputPort.VerifySet(port => port.State = true, Times.Once());
        }

        [TestMethod]
        public async Task TurnOff_ShouldSetRelayToFalse()
        {
            // Declaramos las variables necesarias para el test
            var mockOutputPort = new Mock<IDigitalOutputPort>();
            var rele = new Rele(mockOutputPort.Object);

            // Ponemos el rele en off
            await rele.TurnOff();

            // Comprobamos que el rele esta en off
            mockOutputPort.VerifySet(port => port.State = false, Times.Once());
        }

        [TestMethod]
        public async Task Invert_ShouldToggleRelayState()
        {
            // Declaramos las variables necesarias para el test
            var mockOutputPort = new Mock<IDigitalOutputPort>();
            mockOutputPort.SetupProperty(port => port.State); // Esto permite que el mock tenga un estado
            var rele = new Rele(mockOutputPort.Object);

            // Asume que el rele esta en off
            mockOutputPort.Object.State = false;

            // Invierte la polaridad del rele
            await rele.Invert();

            // Comprueba que el rele esta en on
            Assert.IsTrue(mockOutputPort.Object.State);

            // Vuelve a invertir la polaridad del rele
            await rele.Invert();

            // Comprueba que el rele esta en off
            Assert.IsFalse(mockOutputPort.Object.State);
        }
    }

}


