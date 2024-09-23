using System;
using System.Reflection;
using System.ComponentModel;
using static NewNoteSPRemotePurchaseTerminalIntegration.Lib.Enums;

namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib
{
    public static class Utilities
    {
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
    }
}
