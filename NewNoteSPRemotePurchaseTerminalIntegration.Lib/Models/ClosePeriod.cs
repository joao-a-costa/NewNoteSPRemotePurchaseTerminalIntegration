﻿using System;
using static NewNoteSPRemotePurchaseTerminalIntegration.Lib.Enums;

namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib.Models
{
    internal class ClosePeriod
    {
        private const string _commandClosePeriod = "S00110#TRANSACTIONID##USESUPERVISORCARD##PRINTRECEIPTONPOS##RECEIPTWIDTH#";

        public string TransactionId { get; set; }
        public bool UseSupervisorCard { get; set; } = false;
        public bool PrintReceiptOnPOS { get; set; } = false;
        public ReceiptWidth ReceiptWidth { get; set; } = ReceiptWidth.TWENTYCOLUMNS;

        override public string ToString()
        {
           return _commandClosePeriod
                .Replace("#TRANSACTIONID#", TransactionId.PadLeft(4, '0'))
                .Replace("#USESUPERVISORCARD#", Convert.ToByte(!UseSupervisorCard).ToString())
                .Replace("#PRINTRECEIPTONPOS#", Convert.ToByte(PrintReceiptOnPOS).ToString())
                .Replace("#RECEIPTWIDTH#", (ReceiptWidth == ReceiptWidth.TWENTYCOLUMNS ? 0 : 1).ToString());
        }
    }
}
