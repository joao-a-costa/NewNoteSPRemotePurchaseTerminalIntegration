using System;
using System.Reflection;
using System.ComponentModel;
using static NewNoteSPRemotePurchaseTerminalIntegration.Lib.Enums;

namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib
{
    public static class Utilities
    {
        public static string GetEnumDescription(TerminalCommandOptions value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = (DescriptionAttribute)field.GetCustomAttribute(typeof(DescriptionAttribute));
            return attribute == null ? value.ToString() : attribute.Description;
        }

        // Function to calculate the hex length of the string
        public static byte[] CalculateHexLength(string command)
        {
            var lengthBytes = BitConverter.GetBytes((ushort)command.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);
            return lengthBytes;
        }

    }
}
