// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
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

    private static readonly RsaEncryption rsaEncryption = new();
    private static readonly MessageSigner messageSigner = new(rsaEncryption.Rsa);
    private static readonly Dictionary<string, AesEncryption> peerEncryption = new();
    private static readonly object peerEncryptionLock = new();
    private static string localUserName = $"{Dns.GetHostName()}-{Environment.ProcessId}";
    
    public static int peery = 0;

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
        
        tasklist.Add(Task.Run(ProcessClientIncomingMessages));  // pcim
        tasklist.Add(Task.Run(SendClientOutgoingMessages));     // scom
        tasklist.Add(Task.Run(ProcessServerIncomingMessages));  // psim
        tasklist.Add(Task.Run(SendServerOutgoingMessages));     // ssom


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
            //    - Exit: Disconnect peers
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
                    Console.WriteLine("List peers not implemented yet");
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
                        //TODO complete
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
                        //TODO complete
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
                        //TODO complete
                    }
                    break;
                case CommandType.ListRooms:
                    if (resulty.Args != null) {
                        string room_name = resulty.Args[1];
                        // list rooms
                        //TODO complete
                    }
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
                        string message = resulty.Args[2];
                        // message the specified room
                        //TODO complete
                    }
                    break;
                case CommandType.Exit:
                    if (peery != 0)
                    {
                        Console.WriteLine("Disconnecting everything" );
                        tcpClientHandler.DisconnectAll();
                        // this nukes all connections
                    }
                    else
                    {
                        Console.WriteLine("no peer to disconnect silly");
                    }
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

    private static Task ProcessServerIncomingMessages()
    {
        while (!cancellationTokenSource!.Token.IsCancellationRequested) //checks that it's not cancelled
        {
            var msg = serverMessageQueue!.DequeueIncoming(); //deque
            if (msg != null)
            {
                Console.WriteLine($"Server Received Message: {msg.ToString()}");
                // consoleUI?.DisplayMessage(msg);
            }
        }

        return Task.CompletedTask;
    }

    private static async Task SendServerOutgoingMessages()
    {
        while(!cancellationTokenSource!.Token.IsCancellationRequested)
        {
            Message? msg = serverMessageQueue!.DequeueOutgoing();
            if (msg == null || tcpServer == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(msg.TargetPeerID))
            {
                Peer? targetPeer = tcpServer.GetConnectedPeers().FirstOrDefault(peer => peer.Id == msg.TargetPeerID);

                if (targetPeer != null) // Send specifically to single peer
                {
                    await tcpServer.SendToPeerAsync(targetPeer, msg);
                }
                else
                {
                    Console.WriteLine($"Target peer not found: {msg.TargetPeerID}");
                }
            }
            else // Otherwise, broadcast to all connected
            {
                await tcpServer.BroadcastAsync(msg);
            }
        }
    }

    private static Task ProcessClientIncomingMessages()
    {
        while (!cancellationTokenSource!.Token.IsCancellationRequested) //checks that it's not cancelled
        {
            var msg = clientMessageQueue!.DequeueIncoming(); //deque
            if (msg != null)
            {
                consoleUI?.DisplayMessage(msg);
            }
        }

        return Task.CompletedTask;
    }

    private static async Task SendClientOutgoingMessages()
    {
        while(!cancellationTokenSource!.Token.IsCancellationRequested)
        {
            Message? logicalMessage = clientMessageQueue!.DequeueOutgoing();

            // Skip empty messages
            if(logicalMessage == null || tcpClientHandler == null)
            {
                continue;
            }

            var peers = tcpClientHandler.GetConnectedPeers().ToList();
            
            // Send differently encrypted message to each peer
            foreach(var peer in peers)
            {
                // Ensure session already exists
                bool hasSession;
                lock(peerEncryptionLock)
                {
                    hasSession = peerEncryption.ContainsKey(peer.Id);
                }

                if(!hasSession)
                {
                    Console.WriteLine($"No AES session established with {peer.Id}; skipping");
                    continue;
                }

                // Encrypt using peer's session key and send message
                Message encryptedCopy = CreateEncryptedChatMessage(peer, logicalMessage);
                await tcpClientHandler.SendAsync(peer.Id, encryptedCopy);
            }
        }
    }


    private static void HandlePeerConnected(Peer peer)
    {
        Console.WriteLine($"Connected to {peer.Id} *Transformer noises*");

        var publicKeyMessage = new Message
        {
            Type = MessageType.PublicKey,
            Sender = localUserName,
            TargetPeerID = peer.Id,
            PublicKey = rsaEncryption.ExportPublicKey()
        };

        // Send RSA public key to peer immediately when new connection is made        
        _ = SendToPeerAsync(peer, publicKeyMessage);
    }

    private static async Task SendToPeerAsync(Peer peer, Message message)
    {
        if(tcpClientHandler != null && tcpClientHandler.GetConnectedPeers().Any(p => p.Id == peer.Id))
        {
            await tcpClientHandler.SendAsync(peer.Id, message);
            return;
        }

        if (tcpServer != null)
        {
            await tcpServer.SendToPeerAsync(peer, message);
        }
    }

    private static void HandleServerMessageReceived(Peer peer, Message message)
    {
        HandleIncomingMessage(peer, message, isServerSide: true);
    }

    private static void HandleClientMessageReceived(Peer peer, Message message)
    {
        HandleIncomingMessage(peer, message, isServerSide: false);
    }

    /// <summary>
    /// Call helper method based on incoming message type to determine how to handle it
    /// </summary>
    private static void HandleIncomingMessage(Peer peer, Message message, bool isServerSide)
    {
        switch(message.Type) // Handle messages differently based on message type
        {
            case MessageType.PublicKey:
                HandlePublicKeyMessage(peer, message, generateSessionKey: !isServerSide);
                break;

            case MessageType.SessionKey:
                HandleSessionKeyMessage(peer, message);
                break;

            case MessageType.Chat:
                HandleEncryptedChatMessage(peer, message, isServerSide);
                break;

            default:
                if(isServerSide)
                {
                    serverMessageQueue!.EnqueueIncoming(message);
                    serverMessageQueue!.EnqueueOutgoing(message);
                }
                else
                {
                    clientMessageQueue!.EnqueueIncoming(message);
                }
                break;
        }
    }

    /// <summary>
    /// Processes a received public key message, stores the peer's public key, generates an AES session key if needed, 
    /// and sends the encrypted session key back.
    /// </summary>
    private static void HandlePublicKeyMessage(Peer peer, Message message, bool generateSessionKey)
    {
        if(message.PublicKey == null)
            return;

        // Receive public key for peer
        peer.PublicKey = message.PublicKey;
        Console.WriteLine($"Received public key from {peer.Id}");

        if(!generateSessionKey || peer.AesKey != null)
            return;

        // Create new AES key if doesn't already exist and send it to peer
        byte[] sessionKey = AesEncryption.GenerateKey();
        var aes = new AesEncryption(sessionKey);
        peer.AesKey = sessionKey;

        lock(peerEncryptionLock)
        {
            peerEncryption[peer.Id] = aes; // Locally store new AES key
        }

        // Encrypt and send generated AES key
        byte[] encryptedSessionKey = rsaEncryption.EncryptSessionKey(sessionKey, peer.PublicKey);

        var sessionKeyMessage = new Message
        {
            Type                = MessageType.SessionKey,
            Sender              = localUserName,
            TargetPeerID        = peer.Id,
            EncryptedSessionKey = encryptedSessionKey
        };

        _ = SendToPeerAsync(peer, sessionKeyMessage);
    }

    /// <summary>
    /// Processes an encrypted session key message, decrypts the AES session key, and stores the resulting encryption 
    /// session for the peer.
    /// </summary>
    private static void HandleSessionKeyMessage(Peer peer, Message message)
    {
        if(message.EncryptedSessionKey == null)
        {
            return;
        }

        // Receive AES session key and decrypt it
        byte[] sessionKey = rsaEncryption.DecryptSessionKey(message.EncryptedSessionKey);
        peer.AesKey = sessionKey;

        lock(peerEncryptionLock)
        {
            peerEncryption[peer.Id] = new AesEncryption(sessionKey); // Store peer's session key locally
        }

        Console.WriteLine($"Session key established with {peer.Id}");
    }

    /// <summary>
    /// Handles an encrypted chat message by relaying it when received on the server side or decrypting and verifying 
    /// it when received on the client side.
    /// </summary>
    private static void HandleEncryptedChatMessage(Peer peer, Message message, bool isServerSide)
    {
        if(isServerSide)
        {
            serverMessageQueue!.EnqueueIncoming(message);
            serverMessageQueue!.EnqueueOutgoing(message);
            return;
        }

        if(TryDecryptAndVerify(peer, message, out Message? decryptedMessage) && decryptedMessage != null)
        {
            clientMessageQueue!.EnqueueIncoming(decryptedMessage);
        }
    }

    /// <summary>
    /// Creates an encrypted chat message for a specific peer by encrypting the plaintext content with that peer's AES
    /// session and signing the ciphertext.
    /// </summary>
    private static Message CreateEncryptedChatMessage(Peer peer, Message logicalMessage)
    {
        AesEncryption aes;
        lock(peerEncryptionLock)
        {
            aes = peerEncryption[peer.Id];
        }

        // Encrypt, sign, and return given message using peer's AES session key
        byte[] encryptedBytes = aes.Encrypt(logicalMessage.Content);
        byte[] signature = messageSigner.SignData(encryptedBytes);

        return new Message
        {
            Type                = MessageType.Chat,
            Sender              = logicalMessage.Sender,
            TargetPeerID        = peer.Id,
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
    private static bool TryDecryptAndVerify(Peer peer, Message message, out Message? decryptedMessage)
    {
        decryptedMessage = null;

        if(message.EncryptedContent == null || message.Signature == null || peer.PublicKey == null)
        {
            Console.WriteLine("Missing encrypted content, signature, or public key");
            return false;
        }

        // Validate signature of message using peer's public key
        bool valid = messageSigner.VerifyData(message.EncryptedContent, message.Signature, peer.PublicKey);
        if(!valid)
        {
            Console.WriteLine("Signature verification failed");
            return false;
        }

        // Decrypt 
        AesEncryption aes;
        lock(peerEncryptionLock)
        {
            if(!peerEncryption.TryGetValue(peer.Id, out aes!))
            {
                Console.WriteLine("No AES session found for peer");
                return false;
            }
        }

        string plaintext = aes.Decrypt(message.EncryptedContent);

        decryptedMessage = new Message
        {
            Type = MessageType.Chat,
            Sender = message.Sender,
            TargetPeerID = message.TargetPeerID,
            Room = message.Room,
            Content = plaintext,
            Timestamp = message.Timestamp
        };

        return true;
    }
}
