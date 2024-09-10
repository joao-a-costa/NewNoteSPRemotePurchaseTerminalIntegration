namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib.Models
{
    internal class Purchase
    {
        private const string _commandPurchase = "C00010#TRANSACTIONID##AMOUNT#00000000";

        public string TransactionId { get; set; }
        public string Amount { get; set; }

        override public string ToString()
        {
           return $"{_commandPurchase.Replace("#TRANSACTIONID#", TransactionId.PadLeft(4, '0')).Replace("#AMOUNT#", Amount.PadLeft(8, '0'))}";
        }
    }
}
