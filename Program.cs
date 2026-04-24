// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger
// Group Project

using System.Net;
using System.Net.Sockets;
using SecureMessenger.Core;
using SecureMessenger.Network;
using SecureMessenger.UI;


namespace SecureMessenger;

/// <summary>
/// Coordinates application startup, service initialization, command handling,
/// background message processing, automatic listening, and graceful shutdown.
/// </summary>
class Program
{
    // creates objects for all the items used below
    private static MessageQueue? messageQueue;
    private static readonly string localUserName = $"{Dns.GetHostName()}-{Environment.ProcessId}";

    private static TcpPeerHandler? tcpPeerHandler;
    private static ConsoleUI? consoleUI;
    private static CancellationTokenSource? cancellationTokenSource;

    private static MessageHistory? messageHistory; 

    private static PeerDiscovery? peerDiscovery;

    /// <summary>
    /// Initializes the application, starts background tasks, starts peer discovery/listening,
    /// and runs the main console command loop.
    /// </summary>
    static async Task Main(string[] args)
    {
        Console.WriteLine("================================");
        Console.WriteLine("| Secure Distributed Messenger |");
        Console.WriteLine("================================");

        cancellationTokenSource = new CancellationTokenSource();
        messageQueue = new MessageQueue();          // creates message queue guy
        consoleUI = new ConsoleUI();                // creates a console and put in the message guy
        tcpPeerHandler = new TcpPeerHandler(){localUserName = localUserName};
        messageHistory = new MessageHistory();

        tcpPeerHandler.OnPeerConnected += HandlePeerConnected;
        tcpPeerHandler.OnMessageReceived += HandleMessageReceived;
        tcpPeerHandler.OnPeerDisconnected += peer =>
            Console.WriteLine("Disconnected peer " + peer.Id);

        peerDiscovery = new PeerDiscovery(localUserName, async discoveredPeer =>
        {
            if(tcpPeerHandler == null || discoveredPeer.Address == null)
                return;

            tcpPeerHandler.RecordDiscoveredEndpoint(
                discoveredPeer.Id,
                discoveredPeer.Address,
                discoveredPeer.Port
            );

            if(tcpPeerHandler.HasConnectionWithName(discoveredPeer.Id))
                return;

            string discoveredHost = discoveredPeer.Address.ToString();

            if(tcpPeerHandler.HasConnectionTo(discoveredHost, discoveredPeer.Port))
                return;

            // Only initiate connection from one side to avoid duplicate
            // Smaller ID initiates connection
            if(string.Compare(localUserName, discoveredPeer.Id, StringComparison.Ordinal) > 0)
            {
                Console.WriteLine($"[Discovery] Found peer {discoveredPeer.Id}, waiting for it to connect to us.");
                return;
            }

            Console.WriteLine($"[Discovery] Found peer {discoveredPeer.Id} at {discoveredPeer.Address}:{discoveredPeer.Port}");

            bool connected = await tcpPeerHandler.ConnectAsync(
                discoveredHost,
                discoveredPeer.Port
            );

            // Display successful auto-connects for demo visibility
            if(connected)
                Console.WriteLine($"[Discovery] Auto-connected to {discoveredPeer.Id}");
        });

        // Start background tasks for incoming display and outgoing network sends
        List<Task> tasklist =
        [
            Task.Run(ProcessIncomingMessages),  // pcim
            Task.Run(SendOutgoingMessages),     // scom
        ];


        Console.WriteLine("Type /help for available commands");
        Console.WriteLine($"Local client name: {localUserName}");

        // Assign port to listen on automatically
        int startupPort;
        if(args.Length >= 1 && int.TryParse(args[0], out int requestedPort)) {
            startupPort = requestedPort;
        }
        else {
            startupPort = FindAvailableTcpPort();
        }

        StartListening(startupPort);

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

            // Process non-command input as a message to be sent
            if(!resulty.IsCommand)
            {
                messageQueue!.EnqueueOutgoing(new Message
                {
                    Type = MessageType.Chat,
                    Sender = localUserName,
                    Content = resulty.Message ?? string.Empty
                });
                continue;
            }

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
                            Console.WriteLine($"Connected to peer at {resulty.Args[1]}:{port}");
                        }
                        else
                        {
                            Console.WriteLine($"Couldn't connect to peer at {resulty.Args[1]}:{port}");
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
                        StartListening(listenPort);
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
                    if(resulty.Args != null &&
                        resulty.Args.Length >= 2 &&
                        string.Equals(resulty.Args[1], "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        messageHistory?.Clear();
                        Console.WriteLine("Message history cleared.");
                    }
                    else
                    {
                        messageHistory?.ShowHistory();
                    }
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
                        tcpPeerHandler.JoinLocalRoom(room_name);
                        messageQueue!.EnqueueOutgoing(new Message
                        {
                            Type = MessageType.CreateRoom,
                            Sender = localUserName,
                            Room = room_name,
                            Content = $"{localUserName} created {room_name}"
                        });
                        Console.WriteLine($"Created and joined room {room_name}");
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
                        tcpPeerHandler.JoinLocalRoom(room_name);
                        messageQueue!.EnqueueOutgoing(new Message
                        {
                            Type = MessageType.JoinRoom,
                            Sender = localUserName,
                            Room = room_name,
                            Content = $"{localUserName} joined {room_name}"
                        });
                        Console.WriteLine($"Joined room {room_name}");
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
                        tcpPeerHandler.LeaveLocalRoom(room_name);
                        messageQueue!.EnqueueOutgoing(new Message
                        {
                            Type = MessageType.LeaveRoom,
                            Sender = localUserName,
                            Room = room_name,
                            Content = $"{localUserName} left {room_name}"
                        });
                        Console.WriteLine($"Left room {room_name}");
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

                        if(!tcpPeerHandler.IsInLocalRoom(room_name))
                        {
                            Console.WriteLine($"You are not in room {room_name}. Join it before sending.");
                            break;
                        }

                        List<Peer>? peers = tcpPeerHandler.GetPeersInRoom(room_name);

                        if(peers == null || peers.Count == 0)
                        {
                            Console.WriteLine($"No connected peers are known in room {room_name}.");
                            break;
                        }

                        messageQueue!.EnqueueOutgoing(
                            tcpPeerHandler.CreateMessage(
                                message,
                                MessageType.RoomChat,
                                room_name
                            )
                        );
                    }
                    break;
                case CommandType.Unknown:
                    Console.WriteLine(resulty.Message ?? "Unknown command. Use /help.");
                    break;
                default:
                    Console.WriteLine("Use /msg #<room> message to send chat messages.");
                    break;
            }
        }

        // Shut down background work, network connections, discovery, and message queues
        cancellationTokenSource!.Cancel();

        tcpPeerHandler?.Stop();
        if(peerDiscovery != null)
            await peerDiscovery.Stop();
        messageQueue?.CompleteAdding();

        Task.WaitAll(tasklist);
        Console.WriteLine("Goodbye!");
    }

    /// <summary>
    /// Finds the first available TCP port for local listening, skipping the UDP discovery port.
    /// </summary>
    /// <param name="startingPort">The first port to check.</param>
    /// <returns>An available TCP port number.</returns>
    private static int FindAvailableTcpPort(int startingPort = 5000)
    {
        int port = startingPort;

        while(port < 6000)
        {
            if(port == 5001)
            {
                port++;
                continue;
            }

            try {
                using TcpListener testListener = new TcpListener(IPAddress.Any, port);
                testListener.Start();
                return port;
            }
            catch(SocketException) {
                port++;
            }
        }

        throw new InvalidOperationException("No available TCP port found between 5000 and 5999.");
    }

    /// <summary>
    /// Starts TCP listening and peer discovery on the requested port if the app is not already listening.
    /// </summary>
    /// <param name="listenPort">The TCP port to listen on.</param>
    private static void StartListening(int listenPort)
    {
        if(tcpPeerHandler == null)
        {
            Console.WriteLine("TCP peer handler is not initialized.");
            return;
        }

        if(listenPort == 5001)
        {
            Console.WriteLine("Port 5001 is reserved for UDP peer discovery. Use a TCP port like 5000, 5002, or 5004.");
            return;
        }

        if(tcpPeerHandler.IsListening)
        {
            Console.WriteLine($"Already listening on port {tcpPeerHandler.Port}");
            return;
        }

        try {
            Console.WriteLine($"Starting TCP server on port {listenPort}");
            tcpPeerHandler.Start(listenPort);
            peerDiscovery?.Start(listenPort);
            Console.WriteLine($"Peer discovery started for TCP port {listenPort}");
        }
        catch(SocketException exception) when (exception.SocketErrorCode == SocketError.AddressAlreadyInUse) {
            Console.WriteLine($"Port {listenPort} is already in use. Try another port, such as {listenPort + 1} or {listenPort + 2}.");
        }
        catch(SocketException exception) {
            Console.WriteLine($"Could not start listener on port {listenPort}: {exception.Message}");
        }
    }

    /// <summary>
    /// Handles the event raised when a peer connection is established.
    /// </summary>
    /// <param name="peer">The connected peer.</param>
    private static void HandlePeerConnected(Peer peer)
    {
        Console.WriteLine($"Connected to Peer {peer}");
    }

    /// <summary>
    /// Handles decrypted incoming messages and routes them to room handling, display, or history.
    /// </summary>
    /// <param name="peer">The peer that sent the message.</param>
    /// <param name="message">The decrypted message received from the peer.</param>
    private static void HandleMessageReceived(Peer peer, Message message)
    {
        // ANY MESSAGES TO DISPLAY MUST BE ADDED 
        // TO THE INCOMING QUEUE OF THE CLIENT MESSAGE QUEUE

        // Note that all messages received this way are already decoded.
        switch(message.Type) // Handle messages differently based on message type
        {
            case MessageType.RoomChat:
                if(tcpPeerHandler!.IsInLocalRoom(message.Room))
                {
                    messageQueue!.EnqueueIncoming(message);
                    messageHistory?.SaveMessage(message);
                }
                break;
            case MessageType.Chat:
                messageQueue!.EnqueueIncoming(message);
                messageHistory?.SaveMessage(message);
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

    /// <summary>
    /// Processes messages from the incoming queue and displays them to the console.
    /// </summary>
    /// <returns>A completed task when processing stops.</returns>
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

    /// <summary>
    /// Processes messages from the outgoing queue and sends them to connected peers.
    /// </summary>
    /// <returns>A task representing the outgoing message processing loop.</returns>
    private static async Task SendOutgoingMessages()
    {
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

            bool sentToAtLeastOnePeer = false;

            List<Task<SendResult>> sendTasks = peers.Select(peer => tcpPeerHandler.SendEncryptedMessageAsync(peer, logicalMessage)).ToList();

            SendResult[] results = await Task.WhenAll(sendTasks);

            for(int index = 0; index < peers.Count; index++)
            {
                if(results[index] == SendResult.Success)
                {
                    sentToAtLeastOnePeer = true;
                }
                else
                {
                    Console.WriteLine($"Failed to send message to {peers[index]}.");
                }
            }

            if(sentToAtLeastOnePeer)
                messageHistory?.SaveMessage(logicalMessage);
        }
    }
}
