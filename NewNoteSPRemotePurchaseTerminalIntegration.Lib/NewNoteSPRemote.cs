﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using NewNoteSPRemotePurchaseTerminalIntegration.Lib.Models;

namespace NewNoteSPRemotePurchaseTerminalIntegration.Lib
{
    public class NewNoteSPRemote
    {
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
        public void SendCommand(string command)
        {
            using (var client = new TcpClient(serverIp, port))
            {
                using (var stream = client.GetStream())
                {
                    var hexCommand = Utilities.CalculateHexLength(command);
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
        public void TerminalStatus() =>
            SendCommand(new TerminalStatus().ToString());

        /// <summary>
        /// Opens the period.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        public void OpenPeriod(string transactionId) =>
            SendCommand(new OpenPeriod { TransactionId = transactionId }.ToString());

        /// <summary>
        /// Closes the period.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        public void ClosePeriod(string transactionId) =>
            SendCommand(new ClosePeriod { TransactionId = transactionId }.ToString());

        /// <summary>
        /// Purchases the specified transaction identifier and amount.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="amount">The amount.</param>
        public void Purchase(string transactionId, string amount) =>
            SendCommand(new Purchase { TransactionId = transactionId, Amount = amount }.ToString());

        /// <summary>
        /// The refund.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="amount">The amount.</param>
        public void Refund(string transactionId, string amount) =>
            SendCommand(new Refund { TransactionId = transactionId, Amount = amount }.ToString());
    }
}
