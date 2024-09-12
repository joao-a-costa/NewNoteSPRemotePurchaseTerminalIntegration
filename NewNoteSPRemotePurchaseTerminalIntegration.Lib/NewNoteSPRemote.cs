using System;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using NewNoteSPRemotePurchaseTerminalIntegration.Lib.Models;

namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib
{
    public class NewNoteSPRemote
    {
        #region "Constants"

        private const string _infoSent = "Sent";
        private const string _infoReceived = "Received";

        private const string _okTerminalStatus = "INIT OK";
        private const string _okOpenPeriod = "PERÍODO ABERTO";
        private const string _okClosePeriod = "PERÍODO FECHADO";
        private const string _okPurchase = "PAGAM. EFECTUADO";
        private const string _okRefund = "DEVOL. EFECTUADA";

        private const string _patternIdentTpa = @"Ident\. TPA:\s*(\d+)(\d{4}-\d{2}-\d{2})\s*(\d{2}:\d{2}:\d{2})";
        private const string _dateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        #endregion

        #region "Members"

        private readonly string serverIp;
        private readonly int port;

        #endregion

        #region "Constructors"

        public NewNoteSPRemote(string serverIp, int port)
        {
            this.serverIp = serverIp;
            this.port = port;
        }

        #endregion

        /// <summary>
        /// Sends the command to the server.
        /// </summary>
        /// <param name="command">The command to send.</param>
        public string SendCommand(string command)
        {
            var message = string.Empty;

            using (var client = new TcpClient(serverIp, port))
            {
                using (var stream = client.GetStream())
                {
                    var hexCommand = Utilities.CalculateHexLength(command);
                    stream.Write(hexCommand, 0, hexCommand.Length);
                    var stringCommand = Encoding.ASCII.GetBytes(command);
                    Console.WriteLine($"{_infoSent}: {command}");
                    stream.Write(stringCommand, 0, stringCommand.Length);
                    var buffer = new byte[1024];
                    using (var ms = new MemoryStream())
                    {
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                            ms.Write(buffer, 0, bytesRead);
                        message = Encoding.Default.GetString(ms.ToArray()).Substring(2);
                        Console.WriteLine($"{_infoReceived}: {message}");
                    }
                }
            }

            return message;
        }

        /// <summary>
        /// Terminal status.
        /// </summary>
        public Result TerminalStatus()
        {
            var message = SendCommand(new TerminalStatus().ToString());

            return new Result { Success = message.Substring(9).StartsWith(_okTerminalStatus), Message = message };
        }

        /// <summary>
        /// Opens the period.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        public Result OpenPeriod(string transactionId)
        {
            var message = SendCommand(new OpenPeriod { TransactionId = transactionId }.ToString());

            return new Result { Success = message.Substring(10).StartsWith(_okOpenPeriod), Message = message };
        }

        /// <summary>
        /// Closes the period.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        public Result ClosePeriod(string transactionId)
        {
            var message = SendCommand(new ClosePeriod { TransactionId = transactionId }.ToString());

            return new Result { Success = message.Substring(9).StartsWith(_okClosePeriod), Message = message };
        }

        /// <summary>
        /// Purchases the specified transaction identifier and amount.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="amount">The amount.</param>
        public Result Purchase(string transactionId, string amount)
        {
            var purchaseResult = new PurchaseResult();
            var message  = SendCommand(new Purchase { TransactionId = transactionId, Amount = amount }.ToString());

            if (message.Substring(9).StartsWith(_okPurchase))
            {                
                purchaseResult.TransactionId = transactionId;
                purchaseResult.Amount = amount;

                // Match Ident. TPA for terminal ID, date, and time:
                var matchIdentTpa = Regex.Match(message, _patternIdentTpa);
                if (matchIdentTpa.Success)
                {
                    purchaseResult.OriginalPosIdentification = matchIdentTpa.Groups[1].Value;

                    DateTime.TryParseExact(
                        matchIdentTpa.Groups[2].Value + " " + matchIdentTpa.Groups[3].Value,
                        _dateTimeFormat,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime originalReceiptData
                    );

                    purchaseResult.OriginalReceiptData = originalReceiptData;
                }
            }

            return new Result {
                Success = message.Substring(9).StartsWith(_okPurchase),
                Message = message,
                ExtraData = purchaseResult
            };
        }

        /// <summary>
        /// The refund.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="amount">The amount.</param>
        public Result Refund(PurchaseResult purchaseResult)
        {
            var message = SendCommand(new Refund {
                TransactionId = purchaseResult.TransactionId,
                Amount = purchaseResult.Amount,
                OriginalPosIdentification = purchaseResult.OriginalPosIdentification,
                OriginalReceiptData = purchaseResult.OriginalReceiptData,
                OriginalReceiptTime = purchaseResult.OriginalReceiptData
            }.ToString());

            return new Result { Success = message.Substring(9).StartsWith(_okRefund), Message = message };
        }
    }
}