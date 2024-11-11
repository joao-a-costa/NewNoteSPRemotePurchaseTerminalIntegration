using System;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;
using System.ComponentModel;
using static NewNoteSPRemotePurchaseTerminalIntegration.Lib.Enums;
using NewNoteSPRemotePurchaseTerminalIntegration.Lib.Models;

namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib
{
    public static class Utilities
    {
        private const string _ReceiptStringMerchantCopy = "CÓPIA COMERCIANTE";
        private const string _ReceiptStringClientCopy = "CÓPIA CLIENTE";
        private const string _ReceiptStringMerchantCopyNoAccents = "COPIA COMERCIANTE";
        private const string _ReceiptStringClientCopyNoAccents = "COPIA CLIENTE";

        /// <summary>
        /// Gets the description of the enum value.
        /// </summary>
        /// <param name="value">The enum value to get the description of.</param>
        /// <returns>The description of the enum value.</returns>
        public static string GetEnumDescription(TerminalCommandOptions value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = (DescriptionAttribute)field.GetCustomAttribute(typeof(DescriptionAttribute));
            return attribute == null ? value.ToString() : attribute.Description;
        }

        /// <summary>
        /// Calculates the length of the command in hex.
        /// </summary>
        /// <param name="command">The command to calculate the length of.</param>
        /// <returns>The length of the command in hex.</returns>
        public static byte[] CalculateHexLength(string command)
        {
            var lengthBytes = BitConverter.GetBytes((ushort)command.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);
            return lengthBytes;
        }

        /// <summary>
        /// Converts a byte array to a hex string.
        /// </summary>
        /// <param name="hex">The byte array to convert.</param>
        /// <returns>The hex string.</returns>
        /// <exception cref="ArgumentException">Thrown when the hex string length is not even.</exception>
        public static byte[] ConvertHexStringToByteArray(string hex)
        {
            // Ensure the string length is even
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string length must be even.");

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        /// Converts the receipt to a more readable format
        /// </summary>
        /// <param name="receiptData">The receipt to format</param>
        /// <returns>The formatted receipt</returns>
        public static PurchaseResultReceipt ReceiptDataFormat(string receiptData)
        {
            var merchantCopy = string.Empty;
            var clientCopy = string.Empty;

            var receiptDataFormatted = receiptData?
                .Replace("             ", Environment.NewLine)
                .Replace("      ", Environment.NewLine)
                .Replace("TC:", Environment.NewLine + "TC:")
                .Replace("Id.Estab:", Environment.NewLine + "Id.Estab:")
                .Replace("Per:", Environment.NewLine + "Per:")
                .Replace("AUT:", Environment.NewLine + "AUT:")
                .Replace("Mg", Environment.NewLine + "Mg")
                .Replace("COMPRA\r\n   ", "COMPRA         ")
                                    ;
            receiptDataFormatted = Regex.Replace(receiptDataFormatted,
                @"(\d{2}-\d{2}-\d{2})", Environment.NewLine + "$1");

            receiptDataFormatted = receiptDataFormatted.Replace($"€", string.Empty);

            string[] receipts = receiptDataFormatted?.Split(new[] { _ReceiptStringMerchantCopy,
                _ReceiptStringClientCopy },
                StringSplitOptions.None);

            if (receipts.Length > 1)
            {
                merchantCopy = receipts[0] + _ReceiptStringMerchantCopyNoAccents;
                clientCopy = receipts[1]?.Substring(3) + _ReceiptStringClientCopyNoAccents;
            }

            return new PurchaseResultReceipt
            {
                MerchantCopy = merchantCopy,
                ClientCopy = clientCopy
            };
        }

        /// <summary>
        /// Breaks the string into chunks.
        /// </summary>
        /// <param name="merchantCopy">The merchant copy.</param>
        /// <param name="clientCopy">The client copy.</param>
        /// <param name="chunkSize">The size of the chunks.</param>
        /// <returns>The purchase result receipt.</returns>
        public static PurchaseResultReceipt BreakStringIntoChunks(string merchantCopy,
            string clientCopy, int chunkSize)
        {
            var merchantCopyResult = new StringBuilder();

            for (int i = 0; i < merchantCopy.Length; i += chunkSize)
            {
                int length = Math.Min(chunkSize, merchantCopy.Length - i);
                merchantCopyResult.AppendLine(merchantCopy.Substring(i, length));
            }

            var clientCopyCopyResult = new StringBuilder();

            for (int i = 0; i < clientCopy.Length; i += chunkSize)
            {
                int length = Math.Min(chunkSize, clientCopy.Length - i);
                clientCopyCopyResult.AppendLine(clientCopy.Substring(i, length));
            }

            return new PurchaseResultReceipt
            {
                MerchantCopy = merchantCopyResult.ToString(),
                ClientCopy = clientCopyCopyResult.ToString()
            };
        }
    }
}
