namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib.Models
{
    internal class Refund
    {
        private const string _commandRefund = "C00210#TRANSACTIONID##AMOUNT#00000000";

        public string TransactionId { get; set; }
        public string Amount { get; set; }

        override public string ToString()
        {
           return $"{_commandRefund.Replace("TRANSACTIONID", TransactionId.PadLeft(4, '0')).Replace("AMOUNT", Amount.PadLeft(8, '0'))}";
        }
    }
}
