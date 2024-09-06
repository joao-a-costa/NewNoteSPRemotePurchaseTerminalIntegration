namespace NewNoteSPRemotePurchaseTerminalIntegration.Models
{
    internal class ClosePeriod
    {
        private const string _commandClosePeriod = "S00110#TRANSACTIONID#010";

        public string TransactionId { get; set; }

        override public string ToString()
        {
           return $"{_commandClosePeriod.Replace("TRANSACTIONID", TransactionId.PadLeft(4, '0'))}";
        }
    }
}
