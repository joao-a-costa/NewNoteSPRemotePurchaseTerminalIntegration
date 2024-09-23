using System;

namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib.Models
{
    internal class Purchase
    {
        private const string _commandPurchase = "C00010#TRANSACTIONID##AMOUNT#0000#PRINTRECEIPTONPOS#000";

        public string TransactionId { get; set; }
        public string Amount { get; set; }
        public bool PrintReceiptOnPOS { get; set; } = false;

        override public string ToString()
        {
            string command = _commandPurchase
                .Replace("#TRANSACTIONID#", TransactionId.PadLeft(4, '0'))
                .Replace("#AMOUNT#", Amount.PadLeft(8, '0'))
                .Replace("#PRINTRECEIPTONPOS#", Convert.ToByte(PrintReceiptOnPOS).ToString());

            return command;
        }
    }
}
