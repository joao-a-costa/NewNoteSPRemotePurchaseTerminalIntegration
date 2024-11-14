using System;
using static NewNoteSPRemotePurchaseTerminalIntegration.Lib.Enums;

namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib.Models
{
    internal class Purchase
    {
        private const string _commandPurchase = "C00010#TRANSACTIONID##AMOUNT#0000#PRINTRECEIPTONPOS##RECEIPTWIDTH#00";

        public string TransactionId { get; set; }
        public string Amount { get; set; }
        public bool PrintReceiptOnPOS { get; set; } = false;
        public ReceiptWidth ReceiptWidth { get; set; } = ReceiptWidth.TWENTYCOLUMNS;

        override public string ToString()
        {
            string command = _commandPurchase
                .Replace("#TRANSACTIONID#", TransactionId.PadLeft(4, '0'))
                .Replace("#AMOUNT#", Amount.PadLeft(8, '0'))
                .Replace("#PRINTRECEIPTONPOS#", Convert.ToByte(PrintReceiptOnPOS).ToString())
                .Replace("#RECEIPTWIDTH#", (ReceiptWidth == ReceiptWidth.TWENTYCOLUMNS ? 0 : 1).ToString());

            return command;
        }
    }
}
