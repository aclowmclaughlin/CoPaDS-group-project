// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger
// Group Project

using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Net;
using SecureMessenger.Core;
using SecureMessenger.Network;
using SecureMessenger.Security;
using SecureMessenger.UI;
using System.ComponentModel.DataAnnotations;


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
    // creates objects for all the items used below
    private static MessageQueue? messageQueue;
    private static readonly string localUserName = $"{Dns.GetHostName()}-{Environment.ProcessId}";

    private static TcpPeerHandler? tcpPeerHandler;
    private static ConsoleUI? consoleUI;
    private static CancellationTokenSource? cancellationTokenSource;
    private static HeartbeatMonitor? heartbeatMonitor;

    private static MessageHistory? messageHistory; 

    
    private const bool EnableHeartbeatLogging = true; // Toggle to disable console spam

    static async Task Main(string[] args)
    {
        Console.WriteLine("================================");
        Console.WriteLine("| Secure Distributed Messenger |");
        Console.WriteLine("================================");

        // 1. Create CancellationTokenSource for shutdown signaling     X
        // 2. Create MessageQueue for thread communication              X
        // 3. Create ConsoleUI for user interface                       X
        // 4. Create TcpServer for incoming connections                 X
        // 5. Create TcpClientHandler for outgoing connections          X

        cancellationTokenSource = new CancellationTokenSource();
        messageQueue = new MessageQueue();         //creates message queue guy
        consoleUI = new ConsoleUI();    // creates a console and put in the message guy
        tcpPeerHandler = new TcpPeerHandler(){localUserName = localUserName};
        messageHistory = new MessageHistory();
        heartbeatMonitor = new HeartbeatMonitor();

        // 1. TcpServer.OnPeerConnected - handle new incoming connections
        // 2. TcpServer.OnMessageReceived - handle received messages
        // 3. TcpServer.OnPeerDisconnected - handle disconnections
        // 4. TcpClientHandler events (same pattern)

        tcpPeerHandler.OnPeerConnected += HandlePeerConnected;
        tcpPeerHandler.OnMessageReceived += HandleMessageReceived;
        tcpPeerHandler.OnPeerDisconnected += peer =>
            Console.WriteLine("Disconnected peer " + peer.Id);
        

        heartbeatMonitor.OnHeartbeatReceived += peerId =>
        {
            if(EnableHeartbeatLogging)
                Console.WriteLine($"Heartbeat received from {peerId}");
        };

        heartbeatMonitor.OnConnectionFailed += peerId =>
        {
            if(EnableHeartbeatLogging)
                Console.WriteLine($"Heartbeat timeout for {peerId}");
            tcpPeerHandler?.Disconnect(peerId);
        };
        
        heartbeatMonitor.Start();

        // TODO: Start background threads
        // 1. Start a thread/task for processing incoming messages
        // 2. Start a thread/task for sending outgoing messages
        // Note: TcpServer.Start() will create its own listen thread
        List<Task> tasklist =
        [
            Task.Run(ProcessIncomingMessages),  // pcim
            Task.Run(SendOutgoingMessages),     // scom
        ];


        Console.WriteLine("Type /help for available commands");
        Console.WriteLine($"Local client name: {localUserName}");

        // Main loop - handle user input
        bool running = true;
        while(running)
        {
            var input = Console.ReadLine();
            if(string.IsNullOrEmpty(input)) continue;

            if(consoleUI == null || tcpPeerHandler == null)
            {
                Console.WriteLine("Application components are not initialized.");
                return;
            }

            var resulty = consoleUI.ParseCommand(input);
            switch(resulty.CommandType)
            {
                case CommandType.Quit:
                    running = false;
                    Console.WriteLine("Quitting program ;)");
                    break;
                case CommandType.Connect:
                    if(resulty.Args != null && resulty.Args.Length >= 3 && int.TryParse(resulty.Args[2], out int port))
                    {
                        bool connected = await tcpPeerHandler.ConnectAsync(resulty.Args[1], port);
                        if(connected)
                        {
                            //TODO
                        }
                        else
                        {
                            Console.WriteLine($"Couldn't connect to server at {resulty.Args[1]}:{port}"); // This now checks if the port can happen and if not exits nicely
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid arguments for /connect. Usage: /connect <ip> <port>");
                    }
                    break;
                case CommandType.Listen:
                    if(resulty.Args != null && resulty.Args.Length >= 2 && int.TryParse(resulty.Args[1], out int listenPort))
                    {
                        Console.WriteLine("Starting TCP Server");
                        tcpPeerHandler.Start(listenPort);
                        //TODO maybe also start the peer discovery?
                    }
                    else
                    {
                        Console.WriteLine("Invalid arguments for /listen. Usage: /listen <port>");
                    }
                    break;

                case CommandType.ListPeers:
                    tcpPeerHandler.ListPeers();
                    break;
                case CommandType.History:
                    messageHistory.ShowHistory();
                    break;
                case CommandType.Help:
                    consoleUI.ShowHelp();
                    break;
                // Room commands
                case CommandType.CreateRoom:
                    // make sure arguments are valid
                    if(resulty.Args == null 
                        || resulty.Args.Length < 2 
                        || !resulty.Args[1].StartsWith('#'))
                    {
                        Console.WriteLine("Invalid arguments for /create. Usage: /create #<room>");
                    } else
                    {
                        string room_name = resulty.Args[1];
                        // create the room
                        messageQueue!.EnqueueOutgoing(new Message
                        {
                            Type = MessageType.CreateRoom,
                            Sender = localUserName,
                            Room = room_name
                        });
                    }
                    break;
                case CommandType.JoinRoom:
                    if(resulty.Args == null 
                        || resulty.Args.Length < 2 
                        || !resulty.Args[1].StartsWith('#'))
                    {
                        Console.WriteLine("Invalid arguments for /join. Usage: /join #<room>");
                        break;
                    } else
                    {
                        string room_name = resulty.Args[1];
                        // join the room
                        messageQueue!.EnqueueOutgoing(new Message
                        {
                            Type = MessageType.JoinRoom,
                            Sender = localUserName,
                            Room = room_name
                        });
                    }
                    break;
                case CommandType.LeaveRoom:
                    if(resulty.Args == null 
                        || resulty.Args.Length < 2 
                        || !resulty.Args[1].StartsWith('#'))
                    {
                        Console.WriteLine("Invalid arguments for /leave. Usage: /leave #<room>");
                        break;
                    } else
                    {
                        string room_name = resulty.Args[1];
                        // leave the room
                        messageQueue!.EnqueueOutgoing(new Message
                        {
                            Type = MessageType.LeaveRoom,
                            Sender = localUserName,
                            Room = room_name
                        });
                    }
                    break;
                case CommandType.ListRooms:
                    var rooms = tcpPeerHandler.ListRooms();
                    if (rooms.Count() == 0)
                    {
                        Console.WriteLine("No Known Rooms");
                    } else
                    {
                        int i = 1;
                        foreach (string room in rooms)
                        {
                            Console.WriteLine($"Room {i}: {room}");
                            i++;
                        }
                    }
                    break;
                case CommandType.MessageRoom:
                    if(resulty.Args == null 
                        || resulty.Args.Length < 3
                        || !resulty.Args[1].StartsWith('#'))
                    {
                        Console.WriteLine("Invalid arguments for /msg. Usage: /msg #<room> message");
                        break;
                    } else
                    {
                        string room_name = resulty.Args[1];
                        string message = string.Join(" ", resulty.Args.Skip(2)); // Send all words after room number arg

                        List<Peer>? peers = tcpPeerHandler.GetPeersInRoom(room_name);
                        if (peers == null)
                        {
                            Console.WriteLine($"No Peers in room {room_name}");
                        } else
                        {
                            // add each one separately to the queue.
                            foreach (Peer peer in peers)
                            {
                                messageQueue!.EnqueueOutgoing(
                                    tcpPeerHandler.CreateMessage(message, 
                                    MessageType.RoomChat, room_name));
                            }
                        }
                    }
                    break;
                case CommandType.Exit:
                    Console.WriteLine("Disconnecting all client connections");
                    tcpPeerHandler?.Stop();
                    break;
                case CommandType.Unknown:
                    Console.WriteLine(resulty.Message ?? "Unknown command. Use /help.");
                    break;
                default:
                    Console.WriteLine("Use /msg #<room> message to send chat messages.");
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

        tcpPeerHandler?.Stop();
        messageQueue?.CompleteAdding();

        Task.WaitAll(tasklist);
        Console.WriteLine("Goodbye!");
    }
    private static void HandlePeerConnected(Peer peer)
    {
        Console.WriteLine($"Connected to Peer {peer}");
        //TODO add the peer to the heartbeat monitor??
    }

    private static void HandleMessageReceived(Peer peer, Message message)
    {
        // ANY MESSAGES TO DISPLAY MUST BE ADDED 
        // TO THE INCOMING QUEUE OF THE CLIENT MESSAGE QUEUE

        // Note that all messages received this way are already decoded.
        switch(message.Type) // Handle messages differently based on message type
        {
            case MessageType.RoomChat:
                //todo check if we are in the room (need to implement our_rooms)
                // first
                if ()
                {
                    messageQueue!.EnqueueIncoming(message);
                }
                break;
            case MessageType.Chat:
                messageQueue!.EnqueueIncoming(message);
                break;
            case MessageType.JoinRoom:
                tcpPeerHandler!.AddToRoom(message.Room, peer);
                break;
            case MessageType.RoomsListing:
                tcpPeerHandler!.HandleRoomsListingMessage(message, peer);
                break;
            case MessageType.CreateRoom:
                tcpPeerHandler!.CreateRoom(message.Room);
                break;
            case MessageType.LeaveRoom:
                tcpPeerHandler!.RemoveFromRoom(message.Room, peer);
                break;
            default:
                Console.WriteLine($"Got message: {message} of unrecognized type.");
                break;
        }
    }

    private static Task ProcessIncomingMessages()
    {
        while(!cancellationTokenSource!.Token.IsCancellationRequested) //checks that it's not cancelled
        {
            try
            {
                var msg = messageQueue!.DequeueIncoming(); //dequeue
                if(msg != null)
                {
                    consoleUI?.DisplayMessage(msg);
                }
            }
            catch(InvalidOperationException) { break; }
        }

        return Task.CompletedTask;
    }

    private static async Task SendOutgoingMessages()
    {
        //TODO fix this logic.
        while(!cancellationTokenSource!.Token.IsCancellationRequested)
        {
            Message? logicalMessage;
            try
            {
                logicalMessage = messageQueue!.DequeueOutgoing();
            }
            catch(InvalidOperationException) { break; }

            // Skip empty messages
            if(logicalMessage == null || tcpPeerHandler == null)
            {
                continue;
            }

            var peers = tcpPeerHandler.GetConnectedPeers().ToList();
            
            // Send differently encrypted message to each peer
            foreach(var peer in peers)
            {
                // this will only really send to the server, but the server
                // will forward to the appropriate client based off of
                // the targetPeerId fieldd
                await tcpPeerHandler.SendAsync(peer.Id, logicalMessage);
            }
        }
    }
}
