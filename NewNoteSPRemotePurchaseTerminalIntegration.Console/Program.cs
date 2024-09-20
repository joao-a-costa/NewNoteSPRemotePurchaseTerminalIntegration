using System;
using System.Net.Sockets;
using NewNoteSPRemotePurchaseTerminalIntegration.Lib;
using static NewNoteSPRemotePurchaseTerminalIntegration.Lib.Enums;

namespace NewNoteSPRemotePurchaseTerminalIntegration.Console
{
    internal static class Program
    {
        #region "Constants"

        private const string _MessageTheFollowingCommandsAreAvailable = "The following commands are available:";
        private const string _MessageInvalidInput = "Invalid input";

        #endregion

        #region "Members"

        private static readonly string serverIp = "192.168.40.175";
        private static readonly int port = 15200;

        private static readonly NewNoteSPRemote newNoteSPRemote = new NewNoteSPRemote(serverIp, port);

        #endregion

        static void Main()
        {
            try
            {
                ListenForUserInput();
            }
            catch (Exception e) when (e is ArgumentNullException || e is SocketException)
            {
                System.Console.WriteLine($"{e.GetType().Name}: {e.Message}");
            }
            System.Console.WriteLine("Press any key to exit...");
            System.Console.ReadKey();
        }        

        #region "Private Methods"

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
                var input = System.Console.ReadLine()?.ToLower();

                if (int.TryParse(input, out int commandValue) && Enum.IsDefined(typeof(TerminalCommandOptions), commandValue))
                {
                    var command = (TerminalCommandOptions)commandValue;
                    switch (command)
                    {
                        case TerminalCommandOptions.SendTerminalStatusRequest:
                            newNoteSPRemote.TerminalStatus();
                            break;
                        case TerminalCommandOptions.SendTerminalOpenPeriod:
                            newNoteSPRemote.OpenPeriod("0001");
                            break;
                        case TerminalCommandOptions.SendTerminalClosePeriod:
                            newNoteSPRemote.ClosePeriod("0001");
                            break;
                        case TerminalCommandOptions.SendProcessPaymentRequest:
                            newNoteSPRemote.Purchase("0001", "00000360", DateTime.Now);
                            break;
                        case TerminalCommandOptions.SendProcessRefundRequest:
                            newNoteSPRemote.Refund(new Lib.Models.PurchaseResult());
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
                    System.Console.WriteLine(_MessageInvalidInput);
                    ShowListOfCommands();
                }
            }
        }

        /// <summary>
        /// Shows the list of commands.
        /// </summary>
        private static void ShowListOfCommands()
        {
            System.Console.WriteLine($"\n\n{_MessageTheFollowingCommandsAreAvailable}");
            foreach (TerminalCommandOptions command in Enum.GetValues(typeof(TerminalCommandOptions)))
            {
                System.Console.WriteLine($"   {(int)command} - {Utilities.GetEnumDescription(command)}");
            }
            System.Console.WriteLine();
        }

        #endregion
    }
}