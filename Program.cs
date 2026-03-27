// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger
// Group Project

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
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

    private static readonly ConcurrentDictionary<string, AesEncryption> peerAesEncryptions = new();
    private static readonly ConcurrentDictionary<string, byte[]> peerPublicKeys = new();


    private static readonly ConcurrentDictionary<string, KeyExchange> peerKeyExchanges = new();

    private static readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> pendingKeyExchanges = new();

    private static readonly ConcurrentDictionary<string, TaskCompletionSource<Dictionary<string, byte[]>>> pendingRoomPeerLists = new();

    private static readonly string localUserName = $"{Dns.GetHostName()}-{Environment.ProcessId}";

    // lowkey idk if we need these since we're mostly modifying concurrent dicts
    private static readonly object _peerEncryptionLock = new();

    private static readonly object _peerKeyExchangeLock = new();

    public static int peery = 0;


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

        tcpServer.OnPeerConnected += HandleServerPeerConnected;
        tcpServer.OnMessageReceived += HandleServerMessageReceived;
        tcpServer.OnPeerDisconnected += peer =>
            Console.WriteLine("Disconnected peer " + peer.Id);
        
        tcpClientHandler.OnConnected+= HandleClientPeerConnected;
        tcpClientHandler.OnMessageReceived+= HandleClientMessageReceived;
        tcpClientHandler.OnDisconnected += peer =>
            Console.WriteLine("disconnected ;)");


        // TODO: Start background threads
        // 1. Start a thread/task for processing incoming messages
        // 2. Start a thread/task for sending outgoing messages
        // Note: TcpServer.Start() will create its own listen thread
        List<Task> tasklist = new List<Task>();
        
        tasklist.Add(Task.Run(ProcessClientIncomingMessages));  // pcim
        tasklist.Add(Task.Run(SendClientOutgoingMessages));     // scom
        tasklist.Add(Task.Run(ProcessServerIncomingMessages));  // psim
        tasklist.Add(Task.Run(SendServerOutgoingMessages));     // ssom


        Console.WriteLine("Type /help for available commands");

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
            //    - Exit: Disconnect peers
            //    - Not a command: Send as a message to peers


            var input = Console.ReadLine();
            if (string.IsNullOrEmpty(input)) continue;

            if (consoleUI == null || tcpClientHandler == null || tcpServer == null)
            {
                Console.WriteLine("Application components are not initialized.");
                return;
            }

            var resulty = consoleUI.ParseCommand(input);
            switch (resulty.CommandType)
            {
                case CommandType.Quit:
                    running = false;
                    Console.WriteLine("Quitting program ;)");
                    break;
                case CommandType.Connect:
                    if (resulty.Args != null && resulty.Args.Length >= 3 && int.TryParse(resulty.Args[2], out int port))
                    {
                        peery = port;

                        bool connected = await tcpClientHandler.ConnectAsync(resulty.Args[1], port);
                        if (connected)
                        {
                            Console.WriteLine($"Connected to peer {peery}");
                        }
                        else
                        {
                            Console.WriteLine($"Couldn't connect to peer {peery}" + " :( ");  //This now checks if the port can happen and if not exits nicely
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid arguments for /connect. Usage: /connect <ip> <port>");
                    }
                    break;
                case CommandType.Listen:
                    if (resulty.Args != null && resulty.Args.Length >= 2 && int.TryParse(resulty.Args[1], out int listenPort))
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
                    clientMessageQueue!.EnqueueOutgoing(new Message
                    {
                        Type = MessageType.ListPeers,
                        Sender = localUserName
                    });
                    break;
                case CommandType.History:
                    Console.WriteLine("History isn't implemented yet");
                    break;
                case CommandType.Help:
                    consoleUI.ShowHelp();
                    break;
                // Room commands
                case CommandType.CreateRoom:
                    // make sure arguments are valid
                    if (resulty.Args == null 
                        || resulty.Args.Length < 2 
                        || !resulty.Args[1].StartsWith('#'))
                    {
                        Console.WriteLine("Invalid arguments for /create. Usage: /create #<room>");
                    } else
                    {
                        string room_name = resulty.Args[1];
                        // create the room
                        clientMessageQueue!.EnqueueOutgoing(new Message
                        {
                            Type = MessageType.CreateRoom,
                            Sender = localUserName,
                            Room = room_name
                        });
                    }
                    break;
                case CommandType.JoinRoom:
                    if (resulty.Args == null 
                        || resulty.Args.Length < 2 
                        || !resulty.Args[1].StartsWith('#'))
                    {
                        Console.WriteLine("Invalid arguments for /join. Usage: /join #<room>");
                        break;
                    } else
                    {
                        string room_name = resulty.Args[1];
                        // join the room
                        clientMessageQueue!.EnqueueOutgoing(new Message
                        {
                            Type = MessageType.JoinRoom,
                            Sender = localUserName,
                            Room = room_name
                        });
                    }
                    break;
                case CommandType.LeaveRoom:
                    if (resulty.Args == null 
                        || resulty.Args.Length < 2 
                        || !resulty.Args[1].StartsWith('#'))
                    {
                        Console.WriteLine("Invalid arguments for /leave. Usage: /leave #<room>");
                        break;
                    } else
                    {
                        string room_name = resulty.Args[1];
                        // leave the room
                        clientMessageQueue!.EnqueueOutgoing(new Message
                        {
                            Type = MessageType.LeaveRoom,
                            Sender = localUserName,
                            Room = room_name
                        });
                    }
                    break;
                case CommandType.ListRooms:
                    clientMessageQueue!.EnqueueOutgoing(new Message
                    {
                        Type = MessageType.ListRooms,
                        Sender = localUserName
                    });
                    break;
                case CommandType.MessageRoom:
                    if (resulty.Args == null 
                        || resulty.Args.Length < 3
                        || !resulty.Args[1].StartsWith('#'))
                    {
                        Console.WriteLine("Invalid arguments for /msg. Usage: /msg #<room> message");
                        break;
                    } else
                    {
                        string room_name = resulty.Args[1];
                        string message = string.Join(" ", resulty.Args.Skip(2)); // Send all words after room number arg
                        var error = await SendMessageToRoom(room_name, message);

                        if (error != null)
                            Console.WriteLine(error);
                    }
                    break;
                case CommandType.Exit:
                    Console.WriteLine("Disconnecting all client connections");
                    tcpClientHandler?.DisconnectAll();
                    break;
                    
                case CommandType.Unknown:
                    Console.WriteLine(resulty.Message ?? "Unknown command. Use /help.");
                    break;

                default:
                    clientMessageQueue!.EnqueueOutgoing(
                        new Message
                        {Content = input, Sender = localUserName});
                    
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
        tcpClientHandler?.DisconnectAll();        
        clientMessageQueue?.CompleteAdding();
        serverMessageQueue?.CompleteAdding();

        Task.WaitAll(tasklist);
        Console.WriteLine("Goodbye!");
    }

    private static void HandleServerMessageReceived(Peer peer, Message message)
    {
        if(!string.IsNullOrWhiteSpace(message.Sender) && string.IsNullOrWhiteSpace(peer.Name))
            peer.Name = message.Sender;

        // ANY MESSAGES TO DISPLAY MUST BE ADDED TO THE INCOMING QUEUE
        // OF THE SERVER MESSAGE QUEUE
        switch (message.Type)
        {
            case MessageType.RoomChat:
            case MessageType.PublicKey:
            case MessageType.SessionKey:
            case MessageType.Chat:
            {
                string forwardingName = message.TargetPeerID;
                Peer? destPeer = tcpServer!.GetPeerByName(forwardingName);

                if (destPeer == null)
                {
                    Console.WriteLine($"Got a message destined for client {forwardingName}, but no connected client with that name exists.");
                    break;
                }

                serverMessageQueue!.EnqueueOutgoing(message);
                break;
            }
            // server commands!
            case MessageType.ListPeers:
                {
                    var peersList = tcpServer!.GetConnectedPeers()
                        .Where(connectedPeer => !string.IsNullOrWhiteSpace(connectedPeer.Name))
                        .Select(connectedPeer => connectedPeer.Name)
                        .Distinct()
                        .ToList();

                    Console.WriteLine($"[server] Handling ListPeers for {peer.Name}. Found: {string.Join(", ", peersList)}");

                    Message responseMessage = new Message
                    {
                        Type = MessageType.ListPeersReply,
                        Sender = "SERVER",
                        TargetPeerID = peer.Name,
                        Content = string.Join(",", peersList)
                    };

                    serverMessageQueue!.EnqueueOutgoing(responseMessage);
                }                
                break;
            case MessageType.ListRooms:
                {
                    // the list rooms message has contents that are just a list of rooms in the form:
                    // room_id1,room_id2,room_id3
                    var rooms_list = tcpServer!.ListRooms();
                    string rooms_list_str = string.Join(",", rooms_list);
                    Message response_message = new Message
                    {
                        Type = MessageType.ListRoomsReply,
                        Sender = "SERVER",
                        TargetPeerID = peer.Name,
                        Content = rooms_list_str
                    };
                    serverMessageQueue!.EnqueueOutgoing(response_message);
                }
                break;
            case MessageType.CreateRoom:
                {
                    string room_name = message.Room;
                    bool alreadyExists = tcpServer!.CreateRoom(room_name);

                    Message responseMessage = new Message
                    {
                        Type = MessageType.ServerNotice,
                        Sender = "SERVER",
                        TargetPeerID = peer.Name,
                        Content = alreadyExists
                            ? $"Room {room_name} already exists."
                            : $"Room {room_name} created."
                    };

                    serverMessageQueue!.EnqueueOutgoing(responseMessage);
                }
                break;
            case MessageType.LeaveRoom:
                {
                    string room_name = message.Room;
                    bool removed = tcpServer!.RemoveFromRoom(room_name, peer);

                    Message responseMessage = new Message
                    {
                        Type = MessageType.ServerNotice,
                        Sender = "SERVER",
                        TargetPeerID = peer.Name,
                        Content = removed
                            ? $"Left room {room_name}."
                            : $"Room {room_name} does not exist."
                    };

                    serverMessageQueue!.EnqueueOutgoing(responseMessage);
                }
                break;
            case MessageType.ListPeersInRoom:
                {
                    // the listPeersInRoom message has contents that are
                    // peer_id:PublicKey,peer_id:PublicKey,...
                    string room_name = message.Room;
                    var peers_list = tcpServer!.GetPeersInRoom(room_name);
                    if (peers_list == null)
                    {
                        Message emptyRoomResponse = new Message
                        {
                            Type = MessageType.ListPeersInRoomReply,
                            Sender = "SERVER",
                            TargetPeerID = peer.Name,
                            Room = room_name,
                            Content = string.Empty
                        };

                        serverMessageQueue!.EnqueueOutgoing(emptyRoomResponse);
                        break;
                    }
                    List<string> peerEntries = new();
                    foreach(Peer otherPeer in peers_list)
                    {
                        if(string.IsNullOrWhiteSpace(otherPeer.Name) || otherPeer.PublicKey == null)
                            continue;

                        peerEntries.Add($"{otherPeer.Name}:{Convert.ToBase64String(otherPeer.PublicKey)}");
                    }
                    Message response_message = new Message
                    {
                        Type = MessageType.ListPeersInRoomReply,
                        Sender = "SERVER",
                        TargetPeerID = peer.Name,
                        Room = room_name,
                        Content = string.Join(",", peerEntries)
                    };

                    serverMessageQueue!.EnqueueOutgoing(response_message);
                }
                break;
            case MessageType.JoinRoom:
                {
                    string roomName = message.Room;
                    bool joined = tcpServer!.AddToRoom(roomName, peer);

                    Console.WriteLine($"[server] JoinRoom from {peer.Name} for {roomName}. Success={joined}");

                    Message responseMessage = new Message
                    {
                        Type = MessageType.ServerNotice,
                        Sender = "SERVER",
                        TargetPeerID = peer.Name,
                        Content = joined
                            ? $"Joined room {roomName}."
                            : $"Room {roomName} does not exist."
                    };

                    serverMessageQueue!.EnqueueOutgoing(responseMessage);
                }
                break;
        }
    }

    private static void HandleServerPeerConnected(Peer peer)
    {
        Console.WriteLine($"[server] Peer {peer.Id} connected from {peer.Address}:{peer.Port}");
    }

    private static void HandleClientPeerConnected(Peer peer)
    {
        Console.WriteLine($"Connected to Server at {peer.Address}, {peer.Port}");
    }

    private static void HandleClientMessageReceived(Peer peer, Message message)
    {
        //TODO fix this
        // ANY MESSAGES TO DISPLAY MUST BE ADDED TO THE INCOMING QUEUE OF THE CLIENT MESSAGE QUEUE
        if (!string.IsNullOrWhiteSpace(message.TargetPeerID) && message.TargetPeerID != localUserName)
        {
            // This message was not for us!! Ignore it.
            return;
        }
        switch(message.Type) // Handle messages differently based on message type
        {
            case MessageType.PublicKey:
                HandlePublicKeyMessage(peer, message);
                break;

            case MessageType.SessionKey:
                HandleSessionKeyMessage(peer, message);
                break;

            case MessageType.RoomChat:
            case MessageType.Chat:
                HandleEncryptedChatMessage(peer, message, false); //set to false for testing purposes
                break;
            
            case MessageType.ListRoomsReply:
                {
                    string[] room_names = message.Content
                        .Split(",", StringSplitOptions.RemoveEmptyEntries);

                    string display = room_names.Length == 0
                        ? "(no rooms)"
                        : string.Join(", ", room_names);

                    clientMessageQueue!.EnqueueIncoming(new Message
                    {
                        Type = MessageType.ServerNotice,
                        Sender = "SERVER",
                        TargetPeerID = localUserName,
                        Content = $"Rooms: {display}"
                    });
                    break;
                }
            case MessageType.ListPeersReply:
                {
                    Console.WriteLine($"[client] Received ListPeersReply: '{message.Content}'");

                    string[] peer_names = message.Content
                        .Split(",", StringSplitOptions.RemoveEmptyEntries);

                    string display = peer_names.Length == 0
                        ? "(no peers)"
                        : string.Join(", ", peer_names);

                    clientMessageQueue!.EnqueueIncoming(new Message
                    {
                        Type = MessageType.ServerNotice,
                        Sender = "SERVER",
                        TargetPeerID = localUserName,
                        Content = $"Peers: {display}"
                    });
                    break;
                }
            case MessageType.ListPeersInRoomReply:
                {
                    string room_name = message.Room;
                    Dictionary<string, byte[]> peerKeys = new();

                    if(!string.IsNullOrWhiteSpace(message.Content))
                    {
                        string[] peersAndKeys = message.Content.Split(",", StringSplitOptions.RemoveEmptyEntries);

                        foreach(string peerAndKey in peersAndKeys)
                        {
                            string[] split = peerAndKey.Split(":", 2);
                            if(split.Length != 2)
                            {
                                continue;
                            }

                            string name = split[0];
                            byte[] keyBytes = Convert.FromBase64String(split[1]);
                            peerKeys[name] = keyBytes;
                        }
                    }

                    if(pendingRoomPeerLists.TryRemove(room_name, out var pendingRoomPeerList))
                        pendingRoomPeerList.TrySetResult(peerKeys);

                    break;
                }

            case MessageType.ServerNotice:
                clientMessageQueue!.EnqueueIncoming(message);
                break;

            case MessageType.CreateRoom:
            case MessageType.ListRooms:
            case MessageType.LeaveRoom:
            case MessageType.ListPeers:
            case MessageType.ListPeersInRoom:
                //server messages- don't do anything.
                Console.WriteLine("Got message intended for a server.");
                break;
            default:
                clientMessageQueue!.EnqueueIncoming(message);
                break;
        }
    }


    private static Task ProcessServerIncomingMessages()
    {
        while (!cancellationTokenSource!.Token.IsCancellationRequested) //checks that it's not cancelled
        {
            try
            {
                var msg = serverMessageQueue!.DequeueIncoming(); //deque
                if (msg != null)
                {
                    Console.WriteLine($"[server] Received {msg.Type} from {msg.Sender} (encrypted={msg.EncryptedContent != null}, bytes={msg.EncryptedContent?.Length ?? 0})");
                    // consoleUI?.DisplayMessage(msg);
                }
            }
            catch (InvalidOperationException) { break; }
        }

        return Task.CompletedTask;
    }

    private static async Task SendServerOutgoingMessages()
    {
        while (!cancellationTokenSource!.Token.IsCancellationRequested)
        {
            Message? msg;
            try
            {
                msg = serverMessageQueue!.DequeueOutgoing();
            }
            catch (InvalidOperationException)
            {
                break;
            }

            if (msg == null || tcpServer == null)
                continue;

            if (string.IsNullOrWhiteSpace(msg.TargetPeerID))
                continue;

            Peer? destinationPeer = tcpServer.GetPeerByName(msg.TargetPeerID);
            if (destinationPeer == null)
            {
                Console.WriteLine($"Unable to deliver message to {msg.TargetPeerID}");
                continue;
            }

            await tcpServer.SendToPeerAsync(destinationPeer, msg);
        }
    }

    private static Task ProcessClientIncomingMessages()
    {
        while (!cancellationTokenSource!.Token.IsCancellationRequested) //checks that it's not cancelled
        {
            try
            {
                var msg = clientMessageQueue!.DequeueIncoming(); //dequeue
                if (msg != null)
                {
                    consoleUI?.DisplayMessage(msg);
                }
            }
            catch (InvalidOperationException) { break; }
        }

        return Task.CompletedTask;
    }

    private static async Task SendClientOutgoingMessages()
    {
        while(!cancellationTokenSource!.Token.IsCancellationRequested)
        {
            Message? logicalMessage;
            try
            {
                logicalMessage = clientMessageQueue!.DequeueOutgoing();
            }
            catch (InvalidOperationException) { break; }

            // Skip empty messages
            if(logicalMessage == null || tcpClientHandler == null)
            {
                continue;
            }

            var peers = tcpClientHandler.GetConnectedPeers().ToList();
            
            // Send differently encrypted message to each peer
            foreach(var peer in peers)
            {
                // this will only really send to the server, but the server
                // will forward to the appropriate client based off of
                // the targetPeerId fieldd
                await tcpClientHandler.SendAsync(peer.Id, logicalMessage);
            }
        }
    }


    private static async Task<string?> SendMessageToRoom(string room_name, string message)
    {
        var waiter = new TaskCompletionSource<Dictionary<string, byte[]>>(TaskCreationOptions.RunContinuationsAsynchronously);

        pendingRoomPeerLists[room_name] = waiter;

        clientMessageQueue!.EnqueueOutgoing(new Message
        {
            Type = MessageType.ListPeersInRoom,
            Sender = localUserName,
            Room = room_name
        });

        Dictionary<string, byte[]> peerKeys;
        try
        {
            peerKeys = await waiter.Task;
        }
        finally
        {
            pendingRoomPeerLists.TryRemove(room_name, out _);
        }

        if(peerKeys.Count == 0)
            return $"Room {room_name} is empty or does not exist.";

        bool sentToAnyone = false;

        foreach(var pair in peerKeys)
        {
            string otherClient = pair.Key;
            byte[] publicKey = pair.Value;

            if(otherClient == localUserName)
            {
                continue;
            }

            peerPublicKeys[otherClient] = publicKey;

            bool connected = await CreateAESConnectionWithClient(otherClient);
            if(!connected)
                return $"Failed to establish secure session with {otherClient}.";

            await SendMessageToClient(otherClient, message, room_name);
            sentToAnyone = true;
        }

        if(!sentToAnyone)
            return $"No other peers are in {room_name}.";

        return null;
    }

    private static async Task<bool> SendMessageToClient(string client_name, string message, string room)
    {
        // check if we are already connected to the client (they exist in the connection dictionary)
        if (!peerAesEncryptions.TryGetValue(client_name, out _))
        {
            // if we are not, connect to the client
            bool connected = await CreateAESConnectionWithClient(client_name);
            if (!connected)
            {
                return false;
            }
        }
        // send the message to the specific client
        Message plainMessage = new Message
        {
            Type=MessageType.RoomChat,
            Sender=localUserName,
            TargetPeerID=client_name,
            Room = room,
            Content = message,
        };

        Message encrypedMessage = CreateEncryptedChatMessage(client_name, plainMessage);
        clientMessageQueue!.EnqueueOutgoing(encrypedMessage);
        return true;
    }

    private static async Task<bool> CreateAESConnectionWithClient(string client_name)
    {
        if(peerAesEncryptions.ContainsKey(client_name))
            return true;

        if(!peerPublicKeys.TryGetValue(client_name, out var peerPublicKey))
        {
            Console.WriteLine($"No public key for client {client_name}");
            return false;
        }

        KeyExchange keyExchange = new();
        keyExchange.ReceivePublicKey(peerPublicKey);
        peerKeyExchanges[client_name] = keyExchange;

        var taskCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        pendingKeyExchanges[client_name] = taskCompletion;

        Message message = new()
        {
            Type = MessageType.PublicKey,
            Sender = localUserName,
            TargetPeerID = client_name,
            PublicKey = keyExchange.GetPublicKey()
        };

        clientMessageQueue!.EnqueueOutgoing(message);
        Console.WriteLine($"Sent public key to {client_name}, waiting for AES key.");

        return await taskCompletion.Task;
    }

    // private static async Task SendToPeerAsync(Peer peer, Message message)
    // {
    //     if(tcpClientHandler != null && tcpClientHandler.GetConnectedPeers().Any(p => p.Id == peer.Id))
    //     {
    //         await tcpClientHandler.SendAsync(peer.Id, message);
    //         return;
    //     }

    //     if (tcpServer != null)
    //     {
    //         await tcpServer.SendToPeerAsync(peer, message);
    //     }
    // }


    /// <summary>
    /// Processes a received public key message, stores the peer's public key, 
    /// generates an AES session key, and sends the encrypted session key back.
    /// </summary>
    private static void HandlePublicKeyMessage(Peer peer, Message message)
    {
        if(message.PublicKey == null || string.IsNullOrWhiteSpace(message.Sender))
            return;

        string remoteClient = message.Sender;
        peerPublicKeys[remoteClient] = message.PublicKey;

        var keyExchange = peerKeyExchanges.GetOrAdd(remoteClient, _ => new KeyExchange());
        keyExchange.ReceivePublicKey(message.PublicKey);

        if(peerAesEncryptions.ContainsKey(remoteClient))
            return;

        byte[] encryptedSessionKey = keyExchange.CreateEncryptedSessionKey();

        if(keyExchange.SessionKey == null)
        {
            Console.WriteLine($"Failed to create session key for {remoteClient}");
            return;
        }

        peerAesEncryptions[remoteClient] = new AesEncryption(keyExchange.SessionKey);

        var sessionKeyMessage = new Message
        {
            Type = MessageType.SessionKey,
            Sender = localUserName,
            TargetPeerID = remoteClient,
            EncryptedSessionKey = encryptedSessionKey
        };

        clientMessageQueue!.EnqueueOutgoing(sessionKeyMessage);
        keyExchange.Complete();

        Console.WriteLine($"Created AES session for {remoteClient}");
    }

    /// <summary>
    /// Processes an encrypted session key message, decrypts the AES session key, and stores the resulting encryption 
    /// session for the peer.
    /// </summary>
    private static void HandleSessionKeyMessage(Peer peer, Message message)
    {
        if(message.EncryptedSessionKey == null || string.IsNullOrWhiteSpace(message.Sender))
            return;

        string remoteClient = message.Sender;

        if(!pendingKeyExchanges.ContainsKey(remoteClient))
        {
            Console.WriteLine($"Unexpected session key from {remoteClient}");
            return;
        }

        if(!peerKeyExchanges.TryGetValue(remoteClient, out var keyExchange))
        {
            Console.WriteLine($"No key exchange state found for {remoteClient}");

            if (pendingKeyExchanges.TryRemove(remoteClient, out var failedNoState))
                failedNoState.TrySetResult(false);

            return;
        }

        keyExchange.ReceiveEncryptedSessionKey(message.EncryptedSessionKey);

        if(keyExchange.SessionKey == null)
        {
            Console.WriteLine($"Failed to establish session key for {remoteClient}");

            if (pendingKeyExchanges.TryRemove(remoteClient, out var failed))
                failed.TrySetResult(false);

            return;
        }

        peerAesEncryptions[remoteClient] = new AesEncryption(keyExchange.SessionKey);
        Console.WriteLine($"Session key established with {remoteClient}");

        if(pendingKeyExchanges.TryRemove(remoteClient, out var pending))
            pending.TrySetResult(true);
    }

    /// <summary>
    /// Handles an encrypted chat message by relaying it when received on the server side or decrypting and verifying 
    /// it when received on the client side.
    /// </summary>
    private static void HandleEncryptedChatMessage(Peer peer, Message message, bool isServerSide)
    {
        //TODO fix this
        if(isServerSide)
        {
            serverMessageQueue!.EnqueueIncoming(message);
            serverMessageQueue!.EnqueueOutgoing(message);
            return;
        }

        if (TryDecryptAndVerify(message.Sender, message, out Message? decryptedMessage) && decryptedMessage != null)
        {
            clientMessageQueue!.EnqueueIncoming(decryptedMessage);
        }
    }

    /// <summary>
    /// Creates an encrypted chat message for a specific peer by encrypting the plaintext content with that peer's AES
    /// session and signing the ciphertext.
    /// </summary>
    private static Message CreateEncryptedChatMessage(string client_name, Message logicalMessage)
    {
        AesEncryption? aes;
        KeyExchange? keyExchange;

        // get aes encryptor
        peerAesEncryptions.TryGetValue(client_name, out aes);
        // get keyexchange
        peerKeyExchanges.TryGetValue(client_name, out keyExchange);

        if (keyExchange == null || aes == null)
        {
            throw new InvalidOperationException($"No key exchange state/aes encryption found for peer {client_name}");
        }

        // Encrypt, sign, and return given message using peer's AES session key
        byte[] encryptedBytes = aes.Encrypt(logicalMessage.Content);
        byte[] signature = keyExchange.Signer.SignData(encryptedBytes); // Use keyExchange for peer to sign data

        return new Message
        {
            Type                = logicalMessage.Type,
            Sender              = logicalMessage.Sender,
            TargetPeerID        = logicalMessage.TargetPeerID,
            Room                = logicalMessage.Room,
            EncryptedContent    = encryptedBytes,
            Signature           = signature,
            Timestamp           = logicalMessage.Timestamp
        };
    }

    /// <summary>
    /// Attempts to verify the signature of an encrypted message, decrypt its content, and reconstruct the original 
    /// plaintext chat message.
    /// </summary>
    private static bool TryDecryptAndVerify(string client_name, Message message, out Message? decryptedMessage)
    {
        decryptedMessage = null;
        
        KeyExchange? keyExchange;
        peerKeyExchanges.TryGetValue(client_name, out keyExchange);

        if(message.EncryptedContent == null || message.Signature == null)
        {
            Console.WriteLine("Missing encrypted content or signature");
            return false;
        }

        if (keyExchange == null)
        {
            Console.WriteLine($"No key exchange state found for peer {client_name}");
            return false;
        }

        byte[]? peer_public_key;
        peerPublicKeys.TryGetValue(client_name, out peer_public_key);

        if (peer_public_key == null)
        {
            Console.WriteLine($"No Peer Public Key Found for peer {client_name}");
            return false;
        }

        bool valid = keyExchange.Signer.VerifyData(message.EncryptedContent, message.Signature, peer_public_key!);
        if (!valid)
        {
            Console.WriteLine("Signature verification failed");
            return false;
        }

        // Decrypt 
        AesEncryption aes;
        if(!peerAesEncryptions.TryGetValue(client_name, out aes!))
        {
            Console.WriteLine("No AES session found for peer");
            return false;
        }

        string plaintext = aes.Decrypt(message.EncryptedContent);

        decryptedMessage = new Message
        {
            Type            = message.Type,
            Sender          = message.Sender,
            TargetPeerID    = message.TargetPeerID,
            Room            = message.Room,
            Content         = plaintext,
            Timestamp       = message.Timestamp
        };

        return true;
    }
}
