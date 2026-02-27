// Team 7: Rue Clow-McLaughli, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger
// Group Project

using System.Diagnostics;
using System.Globalization;
using System.Net;
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

    // creates objects for all the items used below
     private static MessageQueue? serverMessageQueue;
     private static MessageQueue? clientMessageQueue;
     private static TcpServer? tcpServer;
     private static TcpClientHandler? tcpClientHandler;
     private static ConsoleUI? consoleUI;
     private static CancellationTokenSource? cancellationTokenSource;

     //private static MessageHistory? messageHistory;   <--not implemented, will use later 

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
        serverMessageQueue = new MessageQueue();         //creates message queue guy
        clientMessageQueue = new MessageQueue();
        consoleUI = new ConsoleUI();    // creates a console and put in the message guy
        tcpServer = new TcpServer();                  // TCP Server 
        tcpClientHandler = new TcpClientHandler();           //TCP client handler
        //messageHistory = new MessageHistory();

        // 1. TcpServer.OnPeerConnected - handle new incoming connections
        // 2. TcpServer.OnMessageReceived - handle received messages
        // 3. TcpServer.OnPeerDisconnected - handle disconnections
        // 4. TcpClientHandler events (same pattern)

        tcpServer.OnPeerConnected += HandlePeerConnected;
        tcpServer.OnMessageReceived += HandleServerMessageReceived;
        tcpServer.OnPeerDisconnected += peer =>
            Console.WriteLine("Disconnected peer " + peer.Id);
        
        tcpClientHandler.OnConnected+= HandlePeerConnected;
        tcpClientHandler.OnMessageReceived+= HandleClientMessageReceived;
        tcpClientHandler.OnDisconnected += peer =>
            Console.WriteLine("disconnected ;)");


        // TODO: Start background threads
        // 1. Start a thread/task for processing incoming messages
        // 2. Start a thread/task for sending outgoing messages
        // Note: TcpServer.Start() will create its own listen thread
        List<Task> tasklist = new List<Task>();
        
        var pcim = Task.Run(ProcessClientIncomingMessages);
        var scom = Task.Run(SendClientOutgoingMessages);
        var psim = Task.Run(ProcessServerIncomingMessages);
        var ssom = Task.Run(SendServerOutgoingMessages);


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
            switch (resulty.CommandType)
            {
                case CommandType.Quit:
                    running = false;
                    Console.WriteLine("Quitting program ;)");
                    break;

                case CommandType.Connect:
                    if (resulty.Args != null && resulty.Args.Length >= 2 && int.TryParse(resulty.Args[2], out int port))
                    {
                        peery = port;
                        await tcpClientHandler.ConnectAsync(resulty.Args[1], port);
                        Console.WriteLine("Connecting " + peery);
                    }
                    else
                    {
                        Console.WriteLine("Invalid arguments for /connect. Usage: /connect <ip> <port>");
                    }
                    break;

                case CommandType.Listen:
                    if (resulty.Args != null && resulty.Args.Length >= 1 && int.TryParse(resulty.Args[1], out int listenPort))
                    {
                        Console.WriteLine("Starting TCP Server");
                        tcpServer.Start(listenPort);
                    }
                    else
                    {
                        Console.WriteLine("Invalid arguments for /listen. Usage: /listen <port>");
                    }
                    break;

                case CommandType.ListPeers:
                    Console.WriteLine("List peers not implemented yet");
                    break;
                case CommandType.History:
                    Console.WriteLine("History isn't implemented yet");
                    break;
                case CommandType.Help:
                    consoleUI.ShowHelp();
                    break;
                case CommandType.Exit:
                    break;
                    
                case CommandType.Unknown:
                    clientMessageQueue!.EnqueueOutgoing(
                        new Message
                        {Content = input, Sender = Dns.GetHostName()+Environment.ProcessId.ToString()});
                    
                    break;

                default:
                    clientMessageQueue!.EnqueueOutgoing(
                        new Message
                        {Content = input, Sender = Dns.GetHostName()+Environment.ProcessId.ToString()});
                    
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
        tcpClientHandler.DisconnectAll();        
        clientMessageQueue?.CompleteAdding();
        serverMessageQueue?.CompleteAdding();

        Task.WaitAll(tasklist);
        Console.WriteLine("Goodbye!");
    }

    // TODO: Add helper methods as needed
    // Examples:
    // - ProcessIncomingMessages() - background task to process received messages
    // - SendOutgoingMessages() - background task to send queued messages
    // - HandlePeerConnected(Peer peer) - event handler for new connections
    // - HandleMessageReceived(Peer peer, Message message) - event handler for messages

    private static async Task ProcessServerIncomingMessages()
    {
        while (!cancellationTokenSource!.Token.IsCancellationRequested){ //checks that it's not cancelled
            var msg = serverMessageQueue!.DequeueIncoming(); //deque
            if (msg != null)
                {
                    Console.WriteLine($"Server Received Message: {msg.ToString()}");
                    // consoleUI?.DisplayMessage(msg);
                }
            }
    }

    private static async Task SendServerOutgoingMessages()
    {
        while (!cancellationTokenSource!.Token.IsCancellationRequested){  //not cancelled
            var msg = serverMessageQueue!.DequeueOutgoing(); //deque
            if (msg != null && tcpServer != null)
            {
                await tcpServer.BroadcastAsync(msg);
                //sends msg, deques and broadcasts
            }
        }
    }

    private static async Task ProcessClientIncomingMessages()
    {
        while (!cancellationTokenSource!.Token.IsCancellationRequested){ //checks that it's not cancelled
            var msg = clientMessageQueue!.DequeueIncoming(); //deque
            if (msg != null)
                {
                    consoleUI?.DisplayMessage(msg);
                }
            }
    }

    private static async Task SendClientOutgoingMessages()
    {
        while (!cancellationTokenSource!.Token.IsCancellationRequested){  //not cancelled
            var msg = clientMessageQueue!.DequeueOutgoing(); //deque
            if (msg != null && tcpClientHandler != null)
            {
                await tcpClientHandler.BroadcastAsync(msg);
                //sends msg, deques and broadcasts
            }
        }
    }


    private static void HandlePeerConnected(Peer peer)
    {
        Console.WriteLine($"Connected to {peer.Id} *Transformer noises*");
        //just happens when a new peer is connected
    }

    private static void HandleServerMessageReceived(Peer peer, Message message)
    {
        serverMessageQueue!.EnqueueIncoming(message);
        serverMessageQueue!.EnqueueOutgoing(message);
    }

    private static void HandleClientMessageReceived(Peer peer, Message message)
    {
        clientMessageQueue!.EnqueueIncoming(message);
    }
}
