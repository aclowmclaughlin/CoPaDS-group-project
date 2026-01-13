// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger
// Group Project

using SecureMessenger.Core;
using SecureMessenger.Network;
using SecureMessenger.Security;
using SecureMessenger.UI;

namespace SecureMessenger;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Secure Distributed Messenger");
        Console.WriteLine("============================");

        // TODO: Initialize components
        var messageQueue = new MessageQueue();
        var consoleUI = new ConsoleUI(messageQueue);
        var tcpServer = new TcpServer();
        var tcpClientHandler = new TcpClientHandler();

        // TODO: Start the application
        // - Start listening thread
        // - Start receive thread
        // - Start send thread
        // - Handle user input

        Console.WriteLine("Type /help for available commands");

        // Main loop - handle user input
        bool running = true;
        while (running)
        {
            var input = Console.ReadLine();
            if (string.IsNullOrEmpty(input)) continue;

            // TODO: Process commands
            switch (input.ToLower())
            {
                case "/quit":
                case "/exit":
                    running = false;
                    break;
                case "/help":
                    consoleUI.ShowHelp();
                    break;
                default:
                    // TODO: Handle other commands and messages
                    break;
            }
        }

        Console.WriteLine("Goodbye!");
    }
}
