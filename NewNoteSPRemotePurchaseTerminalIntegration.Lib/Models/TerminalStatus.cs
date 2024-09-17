namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib.Models
{
    internal class TerminalStatus
    {
        private const string _commandTerminalStatus = "M00110PZ";

        override public string ToString()
        {
            return _commandTerminalStatus;
        }
    }
}
