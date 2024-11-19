using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NewNoteSPRemotePurchaseTerminalIntegration.Lib.Models;
using static NewNoteSPRemotePurchaseTerminalIntegration.Lib.Enums;

namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib
{
    public class NewNoteSPRemote
    {
        #region "Constants"

        private const string _infoReceived = "Received";
        private const string _infoUnknownError = "Erro no processamento. Consulte o terminal para mais detalhes";

        private const string _okTerminalStatus = "INIT OK";
        private const string _okOpenPeriod = "PERÍODO ABERTO";
        private const string _okClosePeriod = "PERÍODO FECHADO";
        private const string _okPurchase = "000";
        private const string _okRefund = "DEVOL. EFECTUADA";

        private const string _patternReceiptOnECRTerminalIDAndDate1 = @"Ident\. TPA:\s*(\d+)\s*(\d{2}-\d{2}-\d{2})\s*(\d{2}:\d{2}:\d{2})";
        private const string _patternReceiptOnECRTerminalIDAndDate2 = @"Terminal Pagamento Automático:\s*(\d+)\s*(\d{2}-\d{2}-\d{2})\s*(\d{2}:\d{2}:\d{2})";
        private const string _dateTimeFormatOnECR = "yy-MM-dd HH:mm:ss";

        private const string _patternReceiptOnPOSTerminalID = @"[\u001c\b](\d{8})";
        private const string _patternReceiptOnPOSDate = @"(\d{8})";
        private const string _patternReceiptOnPOSTime = @"(\d{6})";
        private const string _dateTimeFormatOnPOS = "yyyyMMdd HHmmss";

        private const string _purchaseTags = "0B9F1C009A009F21009F4100";
        private const string _CaracterBreakline20Columns = "\u0001";
        private const string _CaracterBreakline40Columns = "\u0002";


        #endregion

        #region "Members"

        private readonly string serverIp;
        private readonly int port;

        #endregion

        #region "Properties"

        public string OriginalPosIdentification { get; }

        #endregion

        #region "Events"

        /// <summary>
        /// Define an event to be raised when a message is sent
        /// </summary>
        public event EventHandler<string> MessageSent;

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
        public string SendCommand(string command, string tags = "")
        {
            var message = string.Empty;

            MessageSent?.Invoke(this, command);

            using (var client = new TcpClient(serverIp, port))
            {
                using (var stream = client.GetStream())
                {
                    byte[] hexCommand = null;

                    if (string.IsNullOrEmpty(tags))
                        hexCommand = Utilities.CalculateHexLength(command);
                    else
                        hexCommand = Utilities.ConvertHexStringToByteArray(string.Concat(Utilities.CalculateHexLength(command).Select(b => b.ToString("D2"))));
                    stream.Write(hexCommand, 0, hexCommand.Length);

                    var stringCommand = Encoding.ASCII.GetBytes(command);
                    stream.Write(stringCommand, 0, stringCommand.Length);

                    if (!string.IsNullOrEmpty(tags))
                    {
                        var hexCommandLast = Utilities.ConvertHexStringToByteArray(tags);
                        stream.Write(hexCommandLast, 0, hexCommandLast.Length);
                    }

                    var buffer = new byte[1024];
                    using (var ms = new MemoryStream())
                    {
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                            ms.Write(buffer, 0, bytesRead);
                        message = Encoding.UTF8.GetString(ms.ToArray()).Substring(2);
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
            var success = message.Substring(9).StartsWith(_okTerminalStatus);
            var originalPosIdentification = success ? message.Substring(26) : string.Empty;

            return new Result { Success = success, Message = message, ExtraData = originalPosIdentification };
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

        static string InsertLineBreaks(string input, int maxLength)
        {
            for (int i = maxLength; i < input.Length; i += maxLength)
            {
                // Find the next whitespace to insert the line break
                int nextWhitespace = input.LastIndexOf(' ', i, i - (i - maxLength));
                if (nextWhitespace > 0)
                {
                    input = input.Substring(0, nextWhitespace) + Environment.NewLine + input.Substring(nextWhitespace + 1);
                    i = nextWhitespace; // Reset the loop index to continue after the break
                }
            }

            return input;
        }

        /// <summary>
        /// Purchases the specified transaction identifier and amount.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="amount">The amount.</param>
        /// <param name="originalPosIdentification">The original POS identification.</param>
        /// <param name="printReceiptOnPOS">if set to <c>true</c> [print receipt on POS].</param>
        /// <param name="originalReceiptData">The original receipt data.</param>
        public Result Purchase(string transactionId, string amount, bool printReceiptOnPOS = true,
            ReceiptWidth receiptWidth = ReceiptWidth.TWENTYCOLUMNS)
        {
            var purchaseResult = new PurchaseResult();
            var message  = SendCommand(new Purchase {
                TransactionId = transactionId,
                Amount = amount,
                PrintReceiptOnPOS = printReceiptOnPOS,
                ReceiptWidth = receiptWidth
                }.ToString(),
                _purchaseTags);
            var success = message.Substring(6, 3).Equals(_okPurchase);

            if (success)
            {
                var receiptPosIdentification = string.Empty;
                var receiptDataParsed = DateTime.Now;
                PurchaseResultReceipt receiptData = null;

                purchaseResult.TransactionId = transactionId;
                purchaseResult.Amount = amount;

                if (!printReceiptOnPOS)
                {
                    // Match Ident. TPA for terminal ID, date, and time:
                    var matchIdentTpa = Regex.Match(message, _patternReceiptOnECRTerminalIDAndDate1);
                    if (!matchIdentTpa.Success)
                        matchIdentTpa = Regex.Match(message, _patternReceiptOnECRTerminalIDAndDate2);
                    if (matchIdentTpa.Success)
                    {
                        DateTime.TryParseExact(
                            matchIdentTpa.Groups[2].Value + " " + matchIdentTpa.Groups[3].Value,
                            _dateTimeFormatOnECR,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out receiptDataParsed
                        );

                        receiptPosIdentification = matchIdentTpa.Groups[1].Value;

                        var receiptStrings = message.ToString().Split(new string[] { _CaracterBreakline20Columns }, StringSplitOptions.None);

                        if (receiptStrings.Length == 1)
                            receiptStrings = message.ToString().Split(new string[] { _CaracterBreakline40Columns }, StringSplitOptions.None);

                        if (receiptStrings.Length == 3)
                        {
                            receiptData = Utilities.BreakStringIntoChunks(
                                receiptStrings[1].Substring(1),
                                receiptStrings[2].Substring(1),
                                (int)receiptWidth);
                        }
                        else
                            receiptData = Utilities.ReceiptDataFormat(message.Substring(32));
                    }
                }
                else
                {
                    // Define regex patterns for terminal ID, date, and time
                    string terminalIdPattern = _patternReceiptOnPOSTerminalID;  // Matches 8 digits after a specific control character
                    string datePattern = _patternReceiptOnPOSDate;  // Matches 8 digits (YYYYMMDD) for date
                    string timePattern = _patternReceiptOnPOSTime;  // Matches 6 digits (HHMMSS) for time

                    // Find matches for terminal ID, date, and time
                    Match terminalIdMatch = Regex.Match(message, terminalIdPattern);

                    if (terminalIdMatch.Success)
                    {
                        receiptPosIdentification = terminalIdMatch.Groups[1].Value;

                        Match dateMatch = Regex.Match(message.Substring(terminalIdMatch.Index + terminalIdMatch.Length), datePattern);

                        if (dateMatch.Success)
                        {
                            Match timeMatch = Regex.Match(message.Substring(terminalIdMatch.Index + terminalIdMatch.Length + dateMatch.Index + dateMatch.Length), timePattern);

                            if (timeMatch.Success)
                            {
                                DateTime.TryParseExact(
                                    dateMatch.Groups[1].Value + " " + timeMatch.Groups[1].Value,
                                    _dateTimeFormatOnPOS,
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.None,
                                    out receiptDataParsed
                                );
                            }
                        }
                    }
                }

                purchaseResult.OriginalPosIdentification = receiptPosIdentification;
                purchaseResult.OriginalReceiptData = receiptDataParsed;
                purchaseResult.ReceiptData = receiptData;
            }

            var result = new Result
            {
                Success = success,
                ExtraData = purchaseResult
            };

            result.Message = result.Success ? message : ParseErrorResponse(message);

            return result;
        }

        /// <summary>
        /// The refund.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="amount">The amount.</param>
        public Result Refund(PurchaseResult purchaseResult, bool printReceiptOnPOS = true)
        {
            var message = SendCommand(new Refund {
                TransactionId = purchaseResult.TransactionId,
                Amount = purchaseResult.Amount,
                OriginalPosIdentification = purchaseResult.OriginalPosIdentification,
                OriginalReceiptData = purchaseResult.OriginalReceiptData,
                OriginalReceiptTime = purchaseResult.OriginalReceiptData,
                PrintReceiptOnPOS = printReceiptOnPOS
            }.ToString());

            var result = new Result
            {
                Success = message.Substring(9).StartsWith(_okRefund),
                ExtraData = purchaseResult
            };

            result.Message = result.Success ? message : ParseErrorResponse(message);

            return result;
        }

        /// <summary>
        /// Parses the error response.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The error response.</returns>
        private static string ParseErrorResponse(string message)
        {
            var response = message.Substring(6, 3);

            if (int.TryParse(response, out int intValue))
            {
                var enumValue = (NewNoteNegativeResponses)intValue;
                var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
                if (fieldInfo != null)
                {
                    var descriptionAttributes = (System.ComponentModel.DescriptionAttribute[])fieldInfo
                    .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);

                    if (descriptionAttributes.Length > 0)
                    {
                        return descriptionAttributes[0].Description;
                    }
                }
                else
                {
                    return _infoUnknownError;
                }
            }

            return _infoUnknownError;
        }

    }
}