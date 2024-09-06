using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewNoteSPRemotePurchaseTerminalIntegration.Models
{
    internal class TerminalStatus
    {
        private const string _commandTerminalStatus = "M00110G1";

        override public string ToString()
        {
            return _commandTerminalStatus;
        }
    }
}
