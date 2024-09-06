using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using NewNoteSPRemotePurchaseTerminalIntegration.Models;
using static NewNoteSPRemotePurchaseTerminalIntegration.Enums;

namespace NewNoteSPRemotePurchaseTerminalIntegration
{
    internal static class Program
    {
        #region "Constants"

        private const string _MessageTheFollowingCommandsAreAvailable = "The following commands are available:";
        private const string _MessageInvalidInput = "Invalid input";

        #endregion

        #region "Members"

        private static readonly string serverIp = "192.168.1.252";
        private static readonly int port = 15200;

        #endregion

        static void Main()
        {
            try
            {
                ListenForUserInput();
            }
            catch (Exception e) when (e is ArgumentNullException || e is SocketException)
            {
                Console.WriteLine($"{e.GetType().Name}: {e.Message}");
            }
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        #region "Terminal Operations"

        /// <summary>
        /// Sends the command to the server.
        /// </summary>
        /// <param name="command">The command to send.</param>
        private static void SendCommand(string command)
        {
            using (var client = new TcpClient(serverIp, port))
            {
                using (var stream = client.GetStream())
                {
                    var hexCommand = CalculateHexLength(command);
                    stream.Write(hexCommand, 0, hexCommand.Length);
                    var stringCommand = Encoding.ASCII.GetBytes(command);
                    Console.WriteLine($"Sent: {command}");
                    stream.Write(stringCommand, 0, stringCommand.Length);
                    var buffer = new byte[1024];
                    using (var ms = new MemoryStream())
                    {
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                            ms.Write(buffer, 0, bytesRead);
                        var responseData = Encoding.Default.GetString(ms.ToArray()).Substring(2);
                        Console.WriteLine($"Received: {responseData}");
                    }
                }
            }
        }

        /// <summary>
        /// Terminal status.
        /// </summary>
        private static void TerminalStatus() =>
            SendCommand(new TerminalStatus().ToString());

        /// <summary>
        /// Opens the period.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        private static void OpenPeriod(string transactionId) =>
            SendCommand(new OpenPeriod { TransactionId = transactionId }.ToString());

        /// <summary>
        /// Closes the period.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        private static void ClosePeriod(string transactionId) =>
            SendCommand(new ClosePeriod { TransactionId = transactionId }.ToString());

        /// <summary>
        /// Purchases the specified transaction identifier and amount.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="amount">The amount.</param>
        private static void Purchase(string transactionId, string amount) =>
            SendCommand(new Purchase { TransactionId = transactionId, Amount = amount }.ToString());

        /// <summary>
        /// The refund.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="amount">The amount.</param>
        private static void Refund(string transactionId, string amount) =>
            SendCommand(new Refund { TransactionId = transactionId, Amount = amount }.ToString());

        #endregion

        #region "Private Methods"

        // Function to calculate the hex length of the string
        private static byte[] CalculateHexLength(string command)
        {
            var lengthBytes = BitConverter.GetBytes((ushort)command.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);
            return lengthBytes;
        }

        /// <summary>
        /// Listens for user input and sends the input to the WebSocket server.
        /// </summary>
        /// <returns>The task object representing the asynchronous operation.</returns>
        private static void ListenForUserInput()
        {
            var serverIsRunning = true;

            while (serverIsRunning)
            {
                ShowListOfCommands();
                var input = Console.ReadLine()?.ToLower();

                if (int.TryParse(input, out int commandValue) && Enum.IsDefined(typeof(TerminalCommandOptions), commandValue))
                {
                    var command = (TerminalCommandOptions)commandValue;
                    switch (command)
                    {
                        case TerminalCommandOptions.SendTerminalStatusRequest:
                            TerminalStatus();
                            break;
                        case TerminalCommandOptions.SendTerminalOpenPeriod:
                            OpenPeriod("0001");
                            break;
                        case TerminalCommandOptions.SendTerminalClosePeriod:
                            ClosePeriod("0001");
                            break;
                        case TerminalCommandOptions.SendProcessPaymentRequest:
                            Purchase("0001", "00000009");
                            break;
                        case TerminalCommandOptions.SendProcessRefundRequest:
                            Refund("0001", "00000009");
                            break;
                        case TerminalCommandOptions.ShowListOfCommands:
                            ShowListOfCommands();
                            break;
                        case TerminalCommandOptions.StopTheServer:
                            serverIsRunning = false;
                            break;
                    }
                }
                else
                {
                    Console.WriteLine(_MessageInvalidInput);
                    ShowListOfCommands();
                }
            }
        }

        /// <summary>
        /// Shows the list of commands.
        /// </summary>
        private static void ShowListOfCommands()
        {
            Console.WriteLine($"\n\n{_MessageTheFollowingCommandsAreAvailable}");
            foreach (TerminalCommandOptions command in Enum.GetValues(typeof(TerminalCommandOptions)))
            {
                Console.WriteLine($"   {(int)command} - {Utilities.GetEnumDescription(command)}");
            }
            Console.WriteLine();
        }

        #endregion
    }
}