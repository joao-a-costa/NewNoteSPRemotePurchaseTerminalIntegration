namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib.Models
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
