using System.Reflection;
using System.ComponentModel;
using static NewNoteSPRemotePurchaseTerminalIntegration.Enums;

namespace NewNoteSPRemotePurchaseTerminalIntegration
{
    public static class Utilities
    {
        public static string GetEnumDescription(TerminalCommandOptions value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = (DescriptionAttribute)field.GetCustomAttribute(typeof(DescriptionAttribute));
            return attribute == null ? value.ToString() : attribute.Description;
        }
    }
}
