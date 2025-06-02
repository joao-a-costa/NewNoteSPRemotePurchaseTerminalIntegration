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
        private const string _infoUnknownError = "Erro no processamento. Consulte o terminal para mais detalhes. Message: #MESSAGE#";

        private const string _okTerminalStatus = "INIT OK";
        private const string _okPurchase = "000";

        private const string _patternReceiptOnECRTerminalIDAndDate1 = @"Ident\. TPA:\s*(\d+)\s*(\d{2}-\d{2}-\d{2})\s*(\d{2}:\d{2}:\d{2})";
        private const string _patternReceiptOnECRTerminalIDAndDate2 = @"Terminal Pagamento Automático:\s*(\d+)\s*(\d{2}-\d{2}-\d{2})\s*(\d{2}:\d{2}:\d{2})";
        private const string _dateTimeFormatOnECR = "yy-MM-dd HH:mm:ss";

        private const string _purchaseTags = "0B9F1C009A009F21009F4100";

        private const string _infoErrorParsingMessage = "An error occurred while parsing the response: {Message}";

        #endregion

        #region "Members"

        private readonly string serverIp;
        private readonly int port;
        private readonly NLog.Logger logger;

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

        public NewNoteSPRemote(string serverIp, int port, NLog.Logger logger)
        {
            this.serverIp = serverIp;
            this.port = port;
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        /// <summary>
        /// Sends the command to the server.
        /// </summary>
        /// <param name="command">The command to send.</param>
        /// <summary>
        /// Sends the command to the server.
        /// </summary>
        /// <param name="command">The command to send.</param>
        public string SendCommand(string command, string tags = "")
        {
            var message = string.Empty;

            try
            {
                MessageSent?.Invoke(this, command);
                logger.Info("Sending command: {Command}", command);

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
                        }

                        logger.Info("{InfoReceived}: {Message}", _infoReceived, message);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while sending command: {Command}", command);
            }

            return message;
        }

        /// <summary>
        /// Terminal status.
        /// </summary>
        public Result TerminalStatus()
        {
            var message = SendCommand(new TerminalStatus().ToString());
            var success = !string.IsNullOrEmpty(message) && message.Substring(9).StartsWith(_okTerminalStatus);
            var originalPosIdentification = success ? message.Substring(26) : string.Empty;

            return new Result { Success = success, Message = message, ExtraData = originalPosIdentification };
        }

        /// <summary>
        /// Opens the period.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        public Result OpenPeriod(string transactionId, bool useSupervisorCard = false, bool printReceiptOnPOS = true,
            ReceiptWidth receiptWidth = ReceiptWidth.TWENTYCOLUMNS)
        {
            var purchaseResult = new PurchaseResult();
            var message = SendCommand(new OpenPeriod {
                TransactionId = transactionId,
                UseSupervisorCard = useSupervisorCard,
                PrintReceiptOnPOS = printReceiptOnPOS,
                ReceiptWidth = receiptWidth
            }.ToString());

            var success = !string.IsNullOrEmpty(message) && message.Substring(6, 3).Equals(_okPurchase);

            if (success)
            {
                try
                {
                    var receiptPosIdentification = string.Empty;
                    var receiptDataParsed = DateTime.Now;
                    PurchaseResultReceipt receiptData = null;

                    purchaseResult.TransactionId = transactionId;
                    //purchaseResult.Amount = amount;

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

                            var merchantReceipt = message.Substring(29 + 2);

                            receiptData = Utilities.BreakStringIntoChunks(
                                merchantReceipt,
                                string.Empty,
                                (int)receiptWidth);
                        }
                    }

                    purchaseResult.OriginalPosIdentification = receiptPosIdentification;
                    purchaseResult.OriginalReceiptData = receiptDataParsed;
                    purchaseResult.ReceiptData = receiptData;
                }
                catch(Exception ex)
                {
                    logger.Error(ex, _infoErrorParsingMessage, message);
                }
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
        /// Closes the period.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        public Result ClosePeriod(string transactionId, bool useSupervisorCard = false, bool printReceiptOnPOS = true,
            ReceiptWidth receiptWidth = ReceiptWidth.TWENTYCOLUMNS)
        {
            var purchaseResult = new PurchaseResult();
            var message = SendCommand(new ClosePeriod {
                TransactionId = transactionId,
                UseSupervisorCard = useSupervisorCard,
                PrintReceiptOnPOS = printReceiptOnPOS,
                ReceiptWidth = receiptWidth
            }.ToString());

            var success = !string.IsNullOrEmpty(message) && message.Substring(6, 3).Equals(_okPurchase);

            if (success)
            {
                try
                {
                    var receiptPosIdentification = string.Empty;
                    var receiptDataParsed = DateTime.Now;
                    PurchaseResultReceipt receiptData = null;

                    purchaseResult.TransactionId = transactionId;
                    //purchaseResult.Amount = amount;

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

                            var merchantReceipt = message.Substring(29 + 2);

                            receiptData = Utilities.BreakStringIntoChunks(
                                merchantReceipt,
                                string.Empty,
                                (int)receiptWidth);
                        }
                    }

                    purchaseResult.OriginalPosIdentification = receiptPosIdentification;
                    purchaseResult.OriginalReceiptData = receiptDataParsed;
                    purchaseResult.ReceiptData = receiptData;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, _infoErrorParsingMessage, message);
                }
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
            var success = !string.IsNullOrEmpty(message) && message.Substring(6, 3).Equals(_okPurchase);

            if (success)
            {
                try
                {
                    var receiptPosIdentification = string.Empty;
                    var receiptDataParsed = DateTime.Now;
                    PurchaseResultReceipt receiptData = null;

                    purchaseResult.TransactionId = transactionId;
                    purchaseResult.Amount = amount;

                    if (!printReceiptOnPOS)
                        ParsePurchaseResponse(receiptWidth, message, ref receiptPosIdentification, ref receiptDataParsed, ref receiptData);

                    purchaseResult.OriginalPosIdentification = receiptPosIdentification;
                    purchaseResult.OriginalReceiptData = receiptDataParsed;
                    purchaseResult.ReceiptData = receiptData;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, _infoErrorParsingMessage, message);
                }
            }

            var result = new Result
            {
                Success = success,
                ExtraData = purchaseResult
            };

            NewNotePositiveResponses.TryGetValue(message.Substring(9, 16), out string messageDescription);

            result.Message = result.Success ? message : ParseErrorResponse(message);
            result.MessageDescription = messageDescription;

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
            var success = !string.IsNullOrEmpty(message) && message.Substring(6, 3).Equals(_okPurchase);

            if (success)
            {
                try
                {
                    var receiptPosIdentification = string.Empty;
                    var receiptDataParsed = DateTime.Now;
                    PurchaseResultReceipt receiptData = null;

                    //purchaseResult.TransactionId = transactionId;
                    //purchaseResult.Amount = amount;

                    if (!printReceiptOnPOS)
                    {
                        ParseRefundMessage(message, ref receiptPosIdentification, ref receiptDataParsed, ref receiptData);
                    }

                    purchaseResult.OriginalPosIdentification = receiptPosIdentification;
                    purchaseResult.OriginalReceiptData = receiptDataParsed;
                    purchaseResult.ReceiptData = receiptData;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, _infoErrorParsingMessage, message);
                }
            }

            var result = new Result
            {
                Success = success,
                ExtraData = purchaseResult
            };

            NewNotePositiveResponses.TryGetValue(message.Substring(9, 16), out string messageDescription);

            result.Message = result.Success ? message : ParseErrorResponse(message);
            result.MessageDescription = messageDescription;

            return result;
        }

        /// <summary>
        /// Parses the purchase response from the terminal.
        /// </summary>
        /// <param name="receiptWidth">The width of the receipt.</param>
        /// <param name="message">The message received from the terminal.</param>
        /// <param name="receiptPosIdentification">The receipt position identification.</param>
        /// <param name="receiptDataParsed">The date and time when the receipt data was parsed.</param>
        /// <param name="receiptData">The parsed receipt data.</param>
        public static void ParsePurchaseResponse(ReceiptWidth receiptWidth, string message, ref string receiptPosIdentification, ref DateTime receiptDataParsed, ref PurchaseResultReceipt receiptData)
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

                var clientReceipt = string.Empty;
                string[] clientReceiptSplitted = null;

                var receiptStrings = message.Substring(31).Split(new[] { (char)0x01 }, StringSplitOptions.None);

                if (receiptStrings.Length == 1)
                    receiptStrings = message.Substring(31).Split(new[] { (char)0x00 }, StringSplitOptions.None);

                if (receiptStrings.Length >= 2)
                {
                    clientReceipt = receiptStrings[1].Substring(1);
                    clientReceiptSplitted = clientReceipt.Split(new[] { (char)0x00 }, StringSplitOptions.None);
                }

                receiptData = Utilities.BreakStringIntoChunks(
                    receiptStrings[0].Substring(1),
                    clientReceiptSplitted?.Length == 2 ? clientReceiptSplitted[0] : clientReceipt,
                    (int)receiptWidth
                );
            }
        }

        /// <summary>
        /// Parses the refund message from the terminal.
        /// </summary>
        /// <param name="message">The message received from the terminal.</param>
        /// <param name="receiptPosIdentification">The receipt position identification.</param>
        /// <param name="receiptDataParsed">The date and time when the receipt data was parsed.</param>
        /// <param name="receiptData">The parsed receipt data.</param>
        private static void ParseRefundMessage(string message, ref string receiptPosIdentification, ref DateTime receiptDataParsed, ref PurchaseResultReceipt receiptData)
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

                var clientReceipt = string.Empty;
                string[] clientReceiptSplitted = null;

                var receiptStrings = message.Substring(31).Split(new[] { (char)0x01 }, StringSplitOptions.None);

                if (receiptStrings.Length == 1)
                    receiptStrings = message.Substring(31).Split(new[] { (char)0x00 }, StringSplitOptions.None);

                if (receiptStrings.Length >= 2)
                {
                    clientReceipt = receiptStrings[1].Substring(1);
                    clientReceiptSplitted = clientReceipt.Split(new[] { (char)0x00 }, StringSplitOptions.None);
                }

                receiptData = Utilities.BreakStringIntoChunks(
                    receiptStrings[0],
                    clientReceiptSplitted.Length >= 2 ? clientReceiptSplitted[0] : clientReceipt
                );
            }
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
                    return _infoUnknownError.Replace($"#MESSAGE#", message);
                }
            }

            return _infoUnknownError.Replace($"#MESSAGE#", message);
        }

    }
}