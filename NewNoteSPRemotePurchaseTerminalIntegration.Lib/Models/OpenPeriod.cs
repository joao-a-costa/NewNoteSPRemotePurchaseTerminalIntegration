namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib.Models
{
    internal class OpenPeriod
    {
        private const string _commandOpenPeriod = "S00010#TRANSACTIONID#010";

        public string TransactionId { get; set; }

        override public string ToString()
        {
           return $"{_commandOpenPeriod.Replace("#TRANSACTIONID#", TransactionId.PadLeft(4, '0'))}";
        }
    }
}
