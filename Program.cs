// Team 7: Rue Clow-McLaughli, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger
// Group Project

using System.Diagnostics;
using System.Globalization;
using System.Net.Quic;
using System.Net.Sockets;
using Microsoft.VisualBasic;
using SecureMessenger.Core;
using SecureMessenger.Network;
using SecureMessenger.Security;
using SecureMessenger.UI;

namespace SecureMessenger;

/// <summary>
/// Main entry point for the Secure Distributed Messenger.
///
/// Architecture Overview:
/// This application uses multiple threads to handle concurrent operations:
///
/// 1. Main Thread (UI Thread)
///    - Reads user input from console
///    - Parses commands using ConsoleUI
///    - Dispatches commands to appropriate handlers
///
/// 2. Listen Thread (Server)
///    - Runs TcpServer to accept incoming connections
///    - Each accepted connection spawns a receive thread
///
/// 3. Receive Thread(s)
///    - One per connected peer
///    - Reads messages from network
///    - Enqueues to incoming message queue
///
/// 4. Send Thread
///    - Dequeues from outgoing message queue
///    - Sends messages to connected peers
///
/// 5. Process Thread (Optional)
///    - Dequeues from incoming message queue
///    - Displays messages to user
///    - Handles decryption and verification
///
/// Thread Communication:
/// - Use MessageQueue for thread-safe message passing
/// - Use CancellationToken for graceful shutdown
/// - Use events for peer connection/disconnection notifications
///
/// Sprint Progression:
/// - Sprint 1: Basic threading and networking (connect, send, receive)
/// - Sprint 2: Add encryption (key exchange, AES encryption, signing)
/// - Sprint 3: Add resilience (peer discovery, heartbeat, reconnection)
/// </summary>
class Program
{
    // Examples:
     private static MessageQueue? messageQueue;
     private static TcpServer? tcpServer;
     private static TcpClientHandler? tcpClientHandler;
     private static ConsoleUI? consoleUI;
     private static CancellationTokenSource? cancellationTokenSource;

    public static int peery;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Secure Distributed Messenger");
        Console.WriteLine("============================");

        // 1. Create CancellationTokenSource for shutdown signaling     X
        // 2. Create MessageQueue for thread communication              X
        // 3. Create ConsoleUI for user interface                       X
        // 4. Create TcpServer for incoming connections                 X
        // 5. Create TcpClientHandler for outgoing connections          X

        cancellationTokenSource = new CancellationTokenSource();
        messageQueue = new MessageQueue();         //creates message queue guy
        consoleUI = new ConsoleUI();    // creates a console and put in the message guy
        tcpServer = new TcpServer();                  // TCP Server 
        tcpClientHandler = new TcpClientHandler();           //TCP client handler

        // 1. TcpServer.OnPeerConnected - handle new incoming connections
        // 2. TcpServer.OnMessageReceived - handle received messages
        // 3. TcpServer.OnPeerDisconnected - handle disconnections
        // 4. TcpClientHandler events (same pattern)

        tcpServer.OnPeerConnected += HandlePeerConnected;
        tcpServer.OnMessageReceived += HandleMessageRecived;
        tcpServer.OnPeerDisconnected += peer =>
            Console.WriteLine("Disconnected");

        
        tcpClientHandler.OnConnected+= HandlePeerConnected;
        tcpClientHandler.OnMessageReceived+= HandleMessageRecived;
        tcpClientHandler.OnDisconnected += peer =>
            Console.WriteLine("disconnected ;)");


        // TODO: Start background threads
        // 1. Start a thread/task for processing incoming messages
        // 2. Start a thread/task for sending outgoing messages
        // Note: TcpServer.Start() will create its own listen thread

        _ = Task.Run(ProcessIncomingMessages);
        _ = Task.Run(SendOutgoingMessages);

        Console.WriteLine("Type /help for available commands");
        Console.WriteLine();

        // Main loop - handle user input
        bool running = true;
        while (running)
        {
            // TODO: Implement the main input loop
            // 1. Read a line from the console                      X
            // 2. Skip empty input                                  X
            // 3. Parse the input using ConsoleUI.ParseCommand()    X
            // 4. Handle the command based on CommandType:          X
            //    - Connect: Call TcpClientHandler.ConnectAsync()   X
            //    - Listen: Call TcpServer.Start()                  X
            //    - ListPeers: Display connected peers
            //    - History: Show message history
            //    - Quit: Set running = false                       X
            //    - Not a command: Send as a message to peers

            var input = Console.ReadLine();
            if (string.IsNullOrEmpty(input)) continue;

            var resulty = consoleUI.ParseCommand(input);
            // Temporary basic command handling - replace with full implementation
            switch (resulty.CommandType)
            {
                case CommandType.Quit:
                    running = false;
                    break;

                case CommandType.Connect:
                    peery = int.Parse(resulty.Args[1]);
                    tcpClientHandler.ConnectAsync(resulty.Args[0], int.Parse(resulty.Args[1]));
                    break;

                case CommandType.Listen:
                    tcpServer.Start(int.Parse(resulty.Args[0]));
                    break;

                case CommandType.ListPeers: //these two have no info on them, I cannot find any other ref to these names
                case CommandType.History:
                case CommandType.Help:
                    consoleUI.ShowHelp();
                    break;

                case CommandType.Unknown:
                    Console.WriteLine("Command does not exist. ");
                    break;

                default:
                    messageQueue!.EnqueueOutgoing(
                        new Message
                        {Content = input});
                    
                    break;
            }
        }

        // TODO: Implement graceful shutdown
        // 1. Cancel the CancellationTokenSource
        // 2. Stop the TcpServer
        // 3. Disconnect all clients
        // 4. Complete the MessageQueue
        // 5. Wait for background threads to finish

        cancellationTokenSource!.Cancel();

        tcpServer?.Stop();
        tcpClientHandler.Disconnect(peery.ToString());        //not implemented in file, is this smth we need still?
        messageQueue?.CompleteAdding();


        Console.WriteLine("Goodbye!");
    }

    /// <summary>
    /// Display help information.
    /// This is a temporary implementation - integrate with ConsoleUI.ShowHelp()
    /// </summary>
    private static void ShowHelp()
    {
        Console.WriteLine("\nAvailable Commands:");
        Console.WriteLine("  /connect <ip> <port>  - Connect to a peer");
        Console.WriteLine("  /listen <port>        - Start listening for connections");
        Console.WriteLine("  /peers                - List connected peers");
        Console.WriteLine("  /history              - View message history");
        Console.WriteLine("  /quit                 - Exit the application");
        Console.WriteLine();
    }

    // TODO: Add helper methods as needed
    // Examples:
    // - ProcessIncomingMessages() - background task to process received messages
    // - SendOutgoingMessages() - background task to send queued messages
    // - HandlePeerConnected(Peer peer) - event handler for new connections
    // - HandleMessageReceived(Peer peer, Message message) - event handler for messages

    private static void ProcessIncomingMessages()
    {
        while (!cancellationTokenSource!.Token.IsCancellationRequested){
            var msg = messageQueue!.DequeueIncoming();

            if (msg != null)
                {
                    Console.WriteLine($"[{msg.Id}] {msg.Content}");
                }
            }
    }

    private static void SendOutgoingMessages()
    {
        while (!cancellationTokenSource!.Token.IsCancellationRequested){ 
            var msg = messageQueue!.DequeueOutgoing();

            if (msg != null)
            {
                //send her out
            }
        }
    }

    private static void HandlePeerConnected(Peer peer)
    {
        Console.WriteLine("Connected to" + peer.Id + "*Trasformer noises*");
    }

    private static void HandleMessageRecived(Peer peer, Message message)
    {
        messageQueue!.EnqueueIncoming(message); //oh yeah enque that message
    }
}