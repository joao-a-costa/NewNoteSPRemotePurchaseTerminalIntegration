using System.ComponentModel;

namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib
{
    public class Enums
    {
        public enum TerminalCommandOptions
        {
            [Description("Send terminal status request")]
            SendTerminalStatusRequest = 1,
            [Description("Send terminal open period request")]
            SendTerminalOpenPeriod = 2,
            [Description("Send terminal close period request")]
            SendTerminalClosePeriod = 3,
            [Description("Send terminal purchase request")]
            SendProcessPaymentRequest = 4,
            [Description("Send terminal refund request")]
            SendProcessRefundRequest = 5,
            [Description("Show list of commands")]
            ShowListOfCommands = 9998,
            [Description("Stop listening")]
            StopTheServer = 9999
        }
    }
}
