using System;
using System.Collections.Generic;
using System.Text;

namespace TemperatureWarriorCode.Web {
    /// <summary>
    /// Event arguments of an incoming web command.
    /// </summary>
    public class WebCommandEventArgs {


        public WebCommandEventArgs(WebCommand command) {
            Command = command;
        }

        public WebCommand Command { get; set; }
        public string ReturnString { get; set; }
    }
}
