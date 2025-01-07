using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
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
        private const string _okPurchase = "000";

        private const string _patternReceiptOnECRTerminalIDAndDate1 = @"Ident\. TPA:\s*(\d+)\s*(\d{2}-\d{2}-\d{2})\s*(\d{2}:\d{2}:\d{2})";
        private const string _patternReceiptOnECRTerminalIDAndDate2 = @"Terminal Pagamento Automático:\s*(\d+)\s*(\d{2}-\d{2}-\d{2})\s*(\d{2}:\d{2}:\d{2})";
        private const string _dateTimeFormatOnECR = "yy-MM-dd HH:mm:ss";

        private const string _purchaseTags = "0B9F1C009A009F21009F4100";

        private const string _sibsKeyword = "SIBS";

        #endregion

        #region "Members"

        private readonly string serverIp;
        private readonly int port;

        #endregion

        #region "Properties"

        public string OriginalPosIdentification { get; }
        public string MerchantCopy { get; set; }
        public string ClientCopy { get; set; }

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
        public string SendCommand(string command, string tags = "", bool parseReceipts = true)
        {
            var message = string.Empty;

            MerchantCopy = string.Empty;
            ClientCopy = string.Empty;

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

                        if (parseReceipts)
                            SplitByReservedBytes(message);
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
            var message = SendCommand(new TerminalStatus().ToString(), parseReceipts: false);
            var success = message.Substring(9).StartsWith(_okTerminalStatus);
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

            var success = message.Substring(6, 3).Equals(_okPurchase);

            if (success)
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

                        receiptData = Utilities.BreakStringIntoChunks(
                            MerchantCopy,
                            ClientCopy,
                            (int)receiptWidth);
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

            var success = message.Substring(6, 3).Equals(_okPurchase);

            if (success)
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

                        receiptData = Utilities.BreakStringIntoChunks(
                            MerchantCopy,
                            ClientCopy,
                            (int)receiptWidth);
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

                        receiptData = Utilities.BreakStringIntoChunks(
                            MerchantCopy,
                            ClientCopy,
                            (int)receiptWidth);
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
            var success = message.Substring(6, 3).Equals(_okPurchase);

            if (success)
            {
                var receiptPosIdentification = string.Empty;
                var receiptDataParsed = DateTime.Now;
                PurchaseResultReceipt receiptData = null;

                //purchaseResult.TransactionId = transactionId;
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

                        receiptData = Utilities.BreakStringIntoChunks(
                            MerchantCopy,
                            ClientCopy);
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

        private List<string> SplitByReservedBytes(string input)
        {
            byte[] reservedBytes = { 0x00, 0x01, 0x02 };
            List<string> result = new List<string>();

            // Convert the input string to byte array
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);

            List<byte> currentPart = new List<byte>();

            // Iterate over the input byte array
            foreach (byte b in inputBytes)
            {
                // If the byte is one of the reserved bytes, process the current part
                if (Array.Exists(reservedBytes, reserved => reserved == b))
                {
                    // If we have any bytes collected, add them as a part
                    if (currentPart.Count > 0)
                    {
                        result.Add(Encoding.UTF8.GetString(currentPart.ToArray()));
                        currentPart.Clear();
                    }
                }
                else
                {
                    // Otherwise, add the byte to the current part
                    currentPart.Add(b);
                }
            }

            // Add the last part if there are any remaining bytes
            if (currentPart.Count > 0)
            {
                result.Add(Encoding.UTF8.GetString(currentPart.ToArray()));
            }

            // Process resultStringList as needed
            if (result?.Count >= 2)
            {
                var clientCopy = string.Empty;

                if (result?.Count >= 3)
                {
                    var sibsIndex = result[2].Substring(1).IndexOf(_sibsKeyword);
                    clientCopy = sibsIndex != -1 ? result[2].Substring(1).Substring(0, sibsIndex + _sibsKeyword.Length) : result[2].Substring(1);
                }

                MerchantCopy = result[1].Substring(1);
                ClientCopy = clientCopy;
            }

            return result;
        }

        ///// <summary>
        ///// Pa
        ///// </summary>
        ///// <param name="ms">The memory stream.</param>
        //private void ParseReceipts(byte[] ms)
        //{
        //    byte[] byteArray = ms;
        //    byte[] reservedBytes = { 0x00, 0x01, 0x02 }; // Example reserved bytes
        //    StringBuilder resultString = new StringBuilder();
        //    List<string> resultStringList = new List<string>();

        //    int byteBufferCount = 0;   // To manage partial byte sequences
        //    byte[] byteBuffer = new byte[299]; // Buffer to accumulate incoming bytes

        //    // Loop through each byte in the byteArray
        //    for (int i = 0; i < byteArray.Length; i++)
        //    {
        //        byte currentByte = byteArray[i];

        //        if (reservedBytes.Contains(currentByte)) // Handling reserved bytes (delimiters)
        //        {
        //            Debug.WriteLine($"Reserved byte encountered: {currentByte}");

        //            // Decode any pending bytes if they exist
        //            if (byteBufferCount > 0)
        //            {
        //                try
        //                {
        //                    string decodedString = Encoding.UTF8.GetString(byteBuffer, 0, byteBufferCount);
        //                    resultString.Append(decodedString);
        //                    Debug.WriteLine($"Decoded UTF-8 string: {decodedString}");
        //                }
        //                catch (Exception ex)
        //                {
        //                    Debug.WriteLine($"Error decoding UTF-8 bytes: {ex.Message}");
        //                }

        //                byteBufferCount = 0;  // Reset byte buffer after processing
        //            }

        //            // Add the decoded string to the list and reset StringBuilder
        //            if (resultString.Length > 0)
        //            {
        //                resultStringList.Add(resultString.ToString());
        //                Debug.WriteLine($"String added to list: {resultString}");
        //                resultString.Clear();
        //            }
        //        }
        //        else
        //        {
        //            // Add byte to the buffer and increment buffer count
        //            byteBuffer[byteBufferCount++] = currentByte;

        //            // Try decoding the buffer if enough bytes are available
        //            try
        //            {
        //                // Attempt to decode current buffer using UTF-8
        //                string decodedString = Encoding.UTF8.GetString(byteBuffer, 0, byteBufferCount);
        //                resultString.Append(decodedString);
        //                Debug.WriteLine($"Decoded UTF-8 and appended: {decodedString}");

        //                byteBufferCount = 0;  // Clear byte buffer after decoding
        //            }
        //            catch (Exception ex)
        //            {
        //                // If exception occurs, likely due to incomplete character
        //                Debug.WriteLine($"Invalid byte sequence or incomplete character in UTF-8: {ex.Message}");

        //                // Log the raw byte data that failed decoding
        //                Debug.WriteLine("Failed byte sequence (raw bytes): " + BitConverter.ToString(byteBuffer, 0, byteBufferCount));
        //            }

        //            // If UTF-8 fails, attempt decoding with ISO-8859-1 (Latin-1) as a fallback
        //            if (byteBufferCount > 0 && resultString.Length == 0)
        //            {
        //                try
        //                {
        //                    string decodedLatin1 = Encoding.GetEncoding("ISO-8859-1").GetString(byteBuffer, 0, byteBufferCount);
        //                    resultString.Append(decodedLatin1);
        //                    Debug.WriteLine($"Decoded ISO-8859-1 and appended: {decodedLatin1}");
        //                    byteBufferCount = 0;
        //                }
        //                catch (Exception ex)
        //                {
        //                    Debug.WriteLine($"Error decoding ISO-8859-1 bytes: {ex.Message}");
        //                }
        //            }
        //        }
        //    }

        //    // Flush any remaining bytes in the buffer (if any)
        //    if (byteBufferCount > 0)
        //    {
        //        try
        //        {
        //            string finalDecoded = Encoding.UTF8.GetString(byteBuffer, 0, byteBufferCount);
        //            resultString.Append(finalDecoded);
        //            Debug.WriteLine($"Final decoded string using UTF-8: {finalDecoded}");
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.WriteLine($"Final UTF-8 decoding error: {ex.Message}");
        //        }

        //        // If UTF-8 fails, try ISO-8859-1 for remaining buffer
        //        if (resultString.Length == 0)
        //        {
        //            try
        //            {
        //                string finalLatin1Decoded = Encoding.GetEncoding("ISO-8859-1").GetString(byteBuffer, 0, byteBufferCount);
        //                resultString.Append(finalLatin1Decoded);
        //                Debug.WriteLine($"Final decoded string using ISO-8859-1: {finalLatin1Decoded}");
        //            }
        //            catch (Exception ex)
        //            {
        //                Debug.WriteLine($"Final ISO-8859-1 decoding error: {ex.Message}");
        //            }
        //        }
        //    }

        //    // Add remaining string data to the result list
        //    if (resultString.Length > 0)
        //    {
        //        resultStringList.Add(resultString.ToString());
        //        Debug.WriteLine($"Final string added: {resultString}");
        //    }

        //    // Process resultStringList as needed
        //    if (resultStringList?.Count >= 2)
        //    {
        //        var clientCopy = string.Empty;

        //        if (resultStringList?.Count >= 3)
        //        {
        //            var sibsIndex = resultStringList[2].Substring(1).IndexOf(_sibsKeyword);
        //            clientCopy = sibsIndex != -1 ? resultStringList[2].Substring(1).Substring(0, sibsIndex + _sibsKeyword.Length) : resultStringList[2].Substring(1);
        //        }

        //        MerchantCopy = resultStringList[1].Substring(1);
        //        ClientCopy = clientCopy;
        //    }
        //}





    }
}