// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SecureMessenger.Core;
using SecureMessenger.Security;

namespace SecureMessenger.Network;

/// <summary>
/// Manages P2P TCP listening, outgoing connections, key exchange, encrypted message sending,
/// receive loops, heartbeat monitoring, reconnection, and room membership state.
/// </summary>
public class TcpPeerHandler
{
    private TcpListener? _listener;

    private readonly Dictionary<string, Peer> _connections = new();
    private readonly object _connections_lock = new();

    private readonly Dictionary<string, (IPAddress Address, int Port)> _knownPeerEndpoints = new();

    public string localUserName = string.Empty;

    private CancellationTokenSource? _cancellationTokenSource;
    private Thread? _listenThread;

    private readonly Dictionary<string, List<Peer>> _rooms = new();
    private object _roomsLock = new();

    private readonly List<string> _our_rooms = new(); // Keeps a list of the rooms we are in

    private RsaEncryption ourRSA = new RsaEncryption();
    private MessageSigner ourMessageSigner;

    public event Action<Peer>? OnPeerConnected;
    public event Action<Peer>? OnPeerDisconnected;
    public event Action<Peer, Message>? OnMessageReceived;

    private readonly HeartbeatMonitor _heartbeatMonitor = new();
    private const bool EnableHeartbeatLogging = true; // Toggle to disable console spam

    private readonly ReconnectionPolicy _reconnectionPolicy;

    public int Port { get; private set; }
    public bool IsListening { get; private set; }

    /// <summary>
    /// Creates a TCP peer handler and wires heartbeat failure handling and reconnection support.
    /// </summary>
    public TcpPeerHandler()
    {
        ourMessageSigner = new MessageSigner(ourRSA.Rsa);

        _heartbeatMonitor.OnHeartbeatReceived += peerId => {
            if(EnableHeartbeatLogging)
                Console.WriteLine($"Heartbeat received from {GetPeerDisplayName(peerId)}");
        };

        _heartbeatMonitor.OnConnectionFailed += peerId => {
            if(EnableHeartbeatLogging)
                Console.WriteLine($"Heartbeat timeout for {GetPeerDisplayName(peerId)}");
            HandleConnectionFailure(peerId);
        };

        _reconnectionPolicy = new ReconnectionPolicy(this);
    }

    /// <summary>
    /// Start listening for incoming connections on the specified port.
    /// </summary>
    public void Start(int port)
    {
        Console.WriteLine($"Starting peer handler...");
        // Store the port number
        Port = port;

        // Create a new CancellationTokenSource
        _cancellationTokenSource = new CancellationTokenSource();

        // Create and start a TcpListener on IPAddress.Any and the specified port
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();

        // Set IsListening to true
        IsListening = true;

        // Create and start a new Thread running ListenLoop
        _listenThread = new Thread(ListenLoop);
        _listenThread.Start();

        // Start heartbeat monitor
        _heartbeatMonitor.Start();

        Console.WriteLine($"Peer handler started and listening on port {port}");
    }

    /// <summary>
    /// Main loop that accepts incoming connections.
    /// </summary>
    private void ListenLoop()
    {
        try
        {
            // Loop while cancellation is not requested
            while(!_cancellationTokenSource!.Token.IsCancellationRequested)
            {
                // Check if a connection is pending using _listener.Pending()
                if(_listener!.Pending())
                {
                    // If pending, accept the connection with AcceptTcpClient()
                    var client = _listener.AcceptTcpClient();
                    // Call HandleNewConnection with the new client
                    _ = Task.Run(() => HandleNewConnection(client));
                }
                else
                {
                    // If not pending, sleep briefly (e.g., 100ms) to avoid busy-waiting
                    Thread.Sleep(100);
                }
            }
        }
        catch (SocketException e) // Handle SocketException
        {
            Console.WriteLine($"Socket exception: {e.Message}");
        }
        catch (IOException e) // Handle IOException appropriately
        {
            Console.WriteLine($"IO exception: {e.Message}");
        }
    }

    
    /// <summary>
    /// Runs the initiating side of the RSA/AES key exchange for an outgoing connection.
    /// </summary>
    /// <param name="peer">The peer being connected to.</param>
    private async Task ExchangeKeySender(Peer peer)
    {
        // send our public key.
        byte[] publicKey = ourRSA.ExportPublicKey();
        byte[] lengthBytes = BitConverter.GetBytes(publicKey.Length);

        await peer.Stream!.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await peer.Stream.WriteAsync(publicKey, 0, publicKey.Length);
        await peer.Stream.FlushAsync();
        
        // receive their public key
        byte[] keyLengthBytes = new byte[4];
        await peer.Stream!.ReadExactlyAsync(keyLengthBytes, 0, 4);

        int keyLength = BitConverter.ToInt32(keyLengthBytes, 0);
        byte[] peerPublicKey = new byte[keyLength];
        await peer.Stream.ReadExactlyAsync(peerPublicKey, 0, keyLength);
        
        // save peer public key
        peer.PublicKey = peerPublicKey;
        Console.WriteLine($"Received initial public key ({keyLength} bytes) from {peer.Address}:{peer.Port}");
        
        // Finalize key exchange
        byte[] aesSessionKey = AesEncryption.GenerateKey();
        byte[] encryptedSessionKey = ourRSA.EncryptSessionKey(aesSessionKey, peerPublicKey);

        lengthBytes = BitConverter.GetBytes(encryptedSessionKey.Length);
        await peer.Stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await peer.Stream.WriteAsync(encryptedSessionKey, 0, encryptedSessionKey.Length);
        await peer.Stream.FlushAsync();

        peer.AesKey = aesSessionKey;
        Console.WriteLine($"Sent AES session key to {peer.Address}:{peer.Port}");
    }

    /// <summary>
    /// Runs the receiving side of the RSA/AES key exchange for an incoming connection.
    /// </summary>
    /// <param name="peer">The peer connecting to this application.</param>
    private async Task ExchangeKeyReceiver(Peer peer)
    {
        // record our public key
        byte[] ourPublicKey = ourRSA.ExportPublicKey();
        // receive their public key
        byte[] keyLengthBytes = new byte[4];
        await peer.Stream!.ReadExactlyAsync(keyLengthBytes, 0, 4);

        int keyLength = BitConverter.ToInt32(keyLengthBytes, 0);
        byte[] peerPublicKey = new byte[keyLength];
        await peer.Stream.ReadExactlyAsync(peerPublicKey, 0, keyLength);

        peer.PublicKey = peerPublicKey;
        Console.WriteLine($"Received initial public key ({keyLength} bytes) from {peer.Address}:{peer.Port}");
        
        // send our public key
        byte[] lengthBytes = BitConverter.GetBytes(ourPublicKey.Length);

        await peer.Stream!.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await peer.Stream.WriteAsync(ourPublicKey, 0, ourPublicKey.Length);
        await peer.Stream.FlushAsync();
        
        // Manage AES key exchange
        keyLengthBytes = new byte[4];
        await peer.Stream.ReadExactlyAsync(keyLengthBytes, 0, 4);

        keyLength = BitConverter.ToInt32(keyLengthBytes, 0);
        byte[] encryptedSessionKey = new byte[keyLength];
        await peer.Stream.ReadExactlyAsync(encryptedSessionKey, 0, keyLength);

        byte[] aesSessionKey = ourRSA.DecryptSessionKey(encryptedSessionKey);

        peer.AesKey = aesSessionKey;
        Console.WriteLine($"Received AES session key from {peer.Address}:{peer.Port}");
    }
    
    /// <summary>
    /// Connect to a peer at the specified address and port.
    /// </summary>
    public async Task<bool> ConnectAsync(string host, int port)
    {
        if(HasConnectionTo(host, port))
        {
            Console.WriteLine($"Already connected to {host}:{port}");
            return true;
        }

        try
        {
            var client = new TcpClient();

            if(host == "localhost") // Convert localhost string to IP number
                host = "127.0.0.1";

            await client.ConnectAsync(host, port);
            
            // Core\Peer.cs
            var peer = new Peer 
            {
                Client = client,
                Stream = client.GetStream(),
                Address = IPAddress.Parse(host), // thank you System.Net
                Port = port,
                IsConnected = true
            };

            // perform key exchange
            await ExchangeKeySender(peer);

            lock(_connections_lock) { _connections[peer.Id] = peer; };

            OnPeerConnected?.Invoke(peer);

            await SendEncryptedMessageAsync(peer, CreateRoomsListingMessage());

            _ = Task.Run(() => ReceiveLoop(peer));
            _ = Task.Run(() => HeartbeatLoop(peer));

            _heartbeatMonitor.StartMonitoring(peer.Id);
            
            return true;
        }
        
        catch(Exception exception) when (
            exception is SocketException ||
            exception is IOException ||
            exception is System.Security.Cryptography.CryptographicException ||
            exception is InvalidOperationException)
        {
            Console.WriteLine($"Connection to {host}:{port} failed: {exception.Message}");
            return false;
        }
    }


    /// <summary>
    /// Handles an incoming TCP client by completing key exchange, registering the peer,
    /// and starting receive and heartbeat loops.
    /// </summary>
    /// <param name="client">The accepted TCP client.</param>
    private async Task HandleNewConnection(TcpClient client)
    {
        Peer? peer = null;
        bool connectionAccepted = false;

        try {
            // A peer object is created before the handshake so failures can still be cleaned up
            peer = new Peer
            {
                Client = client,
                Stream = client.GetStream(),
                Address = ((IPEndPoint)client.Client.RemoteEndPoint!).Address,
                Port = ((IPEndPoint)client.Client.RemoteEndPoint!).Port,
                IsConnected = true
            };

            // The incoming side receives the initiator key data and completes AES setup
            await ExchangeKeyReceiver(peer);

            // The new peer is only registered after the handshake succeeds
            lock(_connections_lock)
            {
                bool alreadyConnected = _connections.Values.Any(existingPeer =>
                    existingPeer.Address != null &&
                    peer.Address != null &&
                    existingPeer.Address.Equals(peer.Address) &&
                    existingPeer.Port == peer.Port &&
                    existingPeer.IsConnected);

                if(alreadyConnected)
                {
                    Console.WriteLine($"Duplicate incoming connection from {peer.Address}:{peer.Port}; closing new connection.");
                    return;
                }

                _connections[peer.Id] = peer;
                connectionAccepted = true;
            }

            // Connection callbacks and background loops start only after registration
            OnPeerConnected?.Invoke(peer);

            await SendEncryptedMessageAsync(peer, CreateRoomsListingMessage());

            _ = Task.Run(() => ReceiveLoop(peer));
            _ = Task.Run(() => HeartbeatLoop(peer));

            _heartbeatMonitor.StartMonitoring(peer.Id);
        }
        catch(Exception exception) when (
            exception is EndOfStreamException ||
            exception is IOException ||
            exception is ObjectDisposedException ||
            exception is SocketException ||
            exception is System.Security.Cryptography.CryptographicException ||
            exception is InvalidOperationException) {
            string endpoint = "unknown endpoint";

            try {
                // The endpoint is used only for a useful diagnostic message
                endpoint = client.Client.RemoteEndPoint?.ToString() ?? endpoint;
            }
            catch(ObjectDisposedException) {
                // The socket may already be disposed after a failed handshake
            }

            Console.WriteLine($"Incoming connection from {endpoint} failed during handshake: {exception.Message}");
        }
        finally {
            if(!connectionAccepted)
            {
                // Failed or duplicate handshakes are closed without taking down the app
                if(peer != null)
                {
                    peer.IsConnected = false;
                    peer.Stream?.Dispose();
                    peer.Client?.Dispose();
                }
                else
                {
                    client.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Receive loop for a specific peer - reads messages until disconnection.
    /// </summary>
    private async Task ReceiveLoop(Peer peer)
    {
        try
        {
            if (peer.Stream == null)
            {
                Console.WriteLine($"Peer {peer.Id} has no stream.");
                return;
            }

            // Create a StreamReader from the peer's stream
            using var reader = new StreamReader(peer.Stream);

            // Loop while peer is connected and cancellation not requested
            while(peer.IsConnected && !_cancellationTokenSource!.Token.IsCancellationRequested)
            {
                var length_str = await reader.ReadLineAsync(); // need to wait until input
                if (length_str == null) break;

                int length = int.Parse(length_str);
                int chars_read = 0;
                char[] serialized_msg = new char[length];
                while(chars_read < length)
                {
                    int new_chars = await reader.ReadAsync(serialized_msg, chars_read, length-chars_read);
                    if (new_chars == 0)
                    {
                        // the stream has been closed ;-;
                        break;
                    }
                    chars_read += new_chars;
                }
                Message? message = JsonSerializer.Deserialize<Message>(serialized_msg);
                if (message == null)
                {
                    // deserialization failed, cry or smthn.
                    Console.WriteLine("Received Message but couldn't deserialize ;-;");
                    continue;
                }

                if(!string.IsNullOrWhiteSpace(message.Sender) && string.IsNullOrWhiteSpace(peer.Name))
                {
                    peer.Name = message.Sender;
                    ApplyDiscoveredEndpoint(peer);
                }

                // Heartbeat update
                if(message.Type == MessageType.Heartbeat)
                {
                    _heartbeatMonitor.RecordHeartbeat(peer.Id);
                    continue;
                }
                
                Message? decryptedMessage;
                bool messageVerified = peer.TryVerifyAndDecrypt(message, out decryptedMessage);

                if (!messageVerified)
                {
                    Console.WriteLine($"Failed to verify message from peer: {peer}");
                } else
                {
                    OnMessageReceived?.Invoke(peer, decryptedMessage!);
                }
            }
        }
        catch (IOException IOE) when (peer.IsConnected){
            Console.WriteLine($"Connection lost: {IOE.Message}");
        }
        
        catch (ObjectDisposedException)
        {
            //we chillin
        }
        finally
        {
            DisconnectPeer(peer);
        }
    }

    /// <summary>
    /// Sends periodic heartbeat messages to a connected peer.
    /// </summary>
    /// <param name="peer">The peer that should receive heartbeat messages.</param>
    private async Task HeartbeatLoop(Peer peer)
    {
        while (peer.IsConnected && !_cancellationTokenSource!.Token.IsCancellationRequested)
        {
            try
            {
                var heartbeat = CreateMessage("", MessageType.Heartbeat);
                await SendAsync(peer, heartbeat);
            }
            catch (ObjectDisposedException)
            {
                // whatever whatever dwbi
            }

            await Task.Delay(_heartbeatMonitor.HeartbeatInterval);
        }
    }

    /// <summary>
    /// Helper method for creating a Message object
    /// </summary>
    public Message CreateMessage(string msg, MessageType type=MessageType.Chat, string? room_name = null)
    {
        return new Message(){
            Sender= localUserName,
            Content = msg, 
            Type = type, 
            Room = room_name==null? string.Empty : room_name
        };
    }

    /// <summary>
    /// Creates a message containing the list of rooms this peer has joined.
    /// </summary>
    /// <returns>A rooms listing message.</returns>
    public Message CreateRoomsListingMessage()
    {
        List<string> roomsSnapshot;

        lock(_roomsLock)
        {
            roomsSnapshot = _our_rooms.ToList();
        }

        return CreateMessage(string.Join(",", roomsSnapshot), MessageType.RoomsListing);
    }

    /// <summary>
    /// Updates known room membership using a received rooms listing message.
    /// </summary>
    /// <param name="roomsListingMessage">The received rooms listing message.</param>
    /// <param name="senderPeer">The peer that sent the listing.</param>
    /// <returns>True if the message was handled as a rooms listing; otherwise false.</returns>
    public bool HandleRoomsListingMessage(Message roomsListingMessage, Peer senderPeer)
    {
        if(roomsListingMessage.Type != MessageType.RoomsListing)
        {
            return false;
        }

        if(string.IsNullOrWhiteSpace(roomsListingMessage.Content))
        {
            return true;
        }

        string[] roomNames = roomsListingMessage.Content
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach(string roomName in roomNames)
        {
            CreateRoom(roomName);
            AddToRoom(roomName, senderPeer);
        }

        return true;
    }

    /// <summary>
    /// Adds this local peer to a room, creating the room locally if needed.
    /// </summary>
    /// <param name="roomName">The room to join.</param>
    /// <returns>True when the local room state is updated.</returns>
    public bool JoinLocalRoom(string roomName)
    {
        CreateRoom(roomName);

        lock(_roomsLock)
        {
            if(!_our_rooms.Contains(roomName))
                _our_rooms.Add(roomName);
        }

        return true;
    }

    /// <summary>
    /// Removes this local peer from a room.
    /// </summary>
    /// <param name="roomName">The room to leave.</param>
    /// <returns>True when the local room state is updated.</returns>
    public bool LeaveLocalRoom(string roomName)
    {
        lock(_roomsLock)
        {
            _our_rooms.Remove(roomName);
        }

        return true;
    }

    /// <summary>
    /// Checks whether this local peer has joined a room.
    /// </summary>
    /// <param name="roomName">The room to check.</param>
    /// <returns>True if this peer is in the room; otherwise false.</returns>
    public bool IsInLocalRoom(string roomName)
    {
        lock(_roomsLock)
        {
            return _our_rooms.Contains(roomName);
        }
    }
  
    /// <summary>
    /// Sends a message to everybody in the specified room.
    /// Returns True if all messages were sent to the room,
    /// False if either the room doesn't exist, or if the 
    /// operation was cancelled. 
    /// The return value probably doesn't matter in 99% of circumstances
    /// and can be generally ignored.
    /// </summary>
    /// <param name="roomName">The name of the room</param>
    /// <param name="message">The message to send</param>
    /// <returns>If the message was sent to everyone in the room.</returns>
    public async Task<bool> SendToRoom(string roomName, Message message)
    {
        // get the peers in the room
        List<Peer>? peersInRoom = GetPeersInRoom(roomName);
        if (peersInRoom == null) return false;
        // for each peer, send a message to them specifically.
        foreach (Peer peer in peersInRoom!)
        {
            // constantly check the cancellation token in case it get cancelled in the middle of the loop.
            if (_cancellationTokenSource != null && _cancellationTokenSource.IsCancellationRequested)
            {
                return false;
            }
            SendResult result = await SendEncryptedMessageAsync(peer, message);

            if(result != SendResult.Success)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Send a message to specific peer
    /// </summary>
    public async Task<SendResult> SendAsync(Peer peer, Message msg)
    {
        if(peer.Stream == null || !peer.IsConnected)
        {
            return SendResult.PeerDisconnected;
        }

        await peer.SendSemaphore.WaitAsync();

        try {
            using var writer = new StreamWriter(peer.Stream, leaveOpen: true);
            string serializedMessage = JsonSerializer.Serialize(msg);
            string totalMessage = serializedMessage.Length + "\n" + serializedMessage;

            await writer.WriteAsync(totalMessage);
            await writer.FlushAsync();

            return SendResult.Success;
        }
        catch(IOException) {
            return SendResult.SendFailed;
        }
        catch(ObjectDisposedException) {
            return SendResult.PeerDisconnected;
        }
        catch(SocketException) {
            return SendResult.SendFailed;
        }
        finally {
            peer.SendSemaphore.Release();
        }
    }

    /// <summary>
    /// Encrypts and signs the provided message 
    /// then sends it to the specified peer.
    /// </summary>
    public async Task<SendResult> SendEncryptedMessageAsync(Peer peer, Message msg)
    {
        Message encryptedMsg = peer.CreateEncryptedMessage(msg);
        Message signedMessage = SignEncryptedMessage(encryptedMsg);

        SendResult result = await SendAsync(peer, signedMessage);

        if(result == SendResult.Success)
            return result;

        Console.WriteLine($"Initial send to {peer} failed. Retrying once.");

        result = await SendAsync(peer, signedMessage);

        if(result != SendResult.Success)
            DisconnectPeer(peer);

        return result;
    }

    /// <summary>
    /// Signs an encrypted message using this peer's RSA signing key.
    /// </summary>
    /// <param name="unsignedMessage">The encrypted message to sign.</param>
    /// <returns>A signed encrypted message.</returns>
    public Message SignEncryptedMessage(Message unsignedMessage)
    {
        byte[] signature = ourMessageSigner.SignData(unsignedMessage.EncryptedContent!);
        
        return new Message
        {
            Type                = unsignedMessage.Type,
            Sender              = unsignedMessage.Sender,
            Room                = unsignedMessage.Room,
            EncryptedContent    = unsignedMessage.EncryptedContent,
            Signature           = signature,
            Timestamp           = unsignedMessage.Timestamp
        };
    }

    /// <summary>
    /// Disconnects a peer by ID if the peer is currently connected.
    /// </summary>
    /// <param name="peerId">The ID of the peer to disconnect.</param>
    public void Disconnect(string peerId)
    {
        lock (_connections_lock)
        {
            Peer? peer;
            if (_connections.TryGetValue(peerId, out peer))
            {
                DisconnectPeer(peer);
            } else
            {
                Console.WriteLine($"Tried to disconnect {peerId} but it does not exist.");
            }
        }
    }

    /// <summary>
    /// Prints all currently connected peers to the console.
    /// </summary>
    public void ListPeers()
    {
        List<Peer> peers_list;
        lock (_connections_lock)
        {
            peers_list = _connections.Values.ToList();
        }
        if (peers_list == null || peers_list.Count == 0)
        {
            Console.WriteLine("No connected peers.");
            return;
        }
        int i = 0;
        foreach(Peer peer in peers_list)
        {
            Console.WriteLine($"Connected Peer [{i}]: {peer}");
            i++;
        }
    }

    /// <summary>
    /// Clean up a disconnected peer.
    /// </summary>
    private void DisconnectPeer(Peer peer)
    {
        lock(_connections_lock)
        {
            if(!peer.IsConnected && !_connections.ContainsKey(peer.Id))
                return;

            peer.IsConnected = false;
            _connections.Remove(peer.Id);
        }

        _heartbeatMonitor.StopMonitoring(peer.Id);

        // Dispose the peer's Client and Stream
        peer.Client?.Dispose();
        peer.Stream?.Dispose();

        // Remove the peer from all rooms
        lock (_roomsLock)
        {
            foreach (var roomEntry in _rooms)
                roomEntry.Value.Remove(peer);
        }

        // Invoke OnPeerDisconnected event
        OnPeerDisconnected?.Invoke(peer);
    }

    /// <summary>
    /// Handles heartbeat timeout by disconnecting the peer and starting reconnection attempts.
    /// </summary>
    /// <param name="peerId">The ID of the failed peer.</param>
    private void HandleConnectionFailure(string peerId)
    {
        Peer? peer;
        lock(_connections_lock)
        {
            _connections.TryGetValue(peerId, out peer);
        }

        if (peer == null) { return; }
        DisconnectPeer(peer);

        Console.WriteLine($"Peer {peerId} fully disconnected (RIP)");

        // attempt reconnection?
        _ = Task.Run(() => _reconnectionPolicy.TryReconnect(peer));
    }

    /// <summary>
    /// Stops listening, disconnects peers, and shuts down heartbeat monitoring.
    /// </summary>
    public void Stop()
    {
        // Cancel the cancellation token
        _cancellationTokenSource?.Cancel();

        // Stop the listener
        _listener?.Stop();

        // Set IsListening to false
        IsListening = false;
        
        // Disconnect all connected peers (with proper locking)
        foreach(Peer peer in GetConnectedPeers())
        {
            DisconnectPeer(peer);
        }
        
        // Wait for the listen thread to finish (with timeout)
        _listenThread?.Join(1000);

        // Stop the heartbeat monitor
        _heartbeatMonitor.Stop();
    }

    /// <summary>
    /// Get a list of currently connected peers.
    /// </summary>
    public IEnumerable<Peer> GetConnectedPeers()
    {
        lock (_connections_lock)
        {
            return _connections.Values.ToList();
        }
    }

    /// <summary>
    /// Gets a readable display name for a peer ID for logging.
    /// </summary>
    /// <param name="peerId">The internal peer ID.</param>
    /// <returns>The peer name, endpoint, or ID.</returns>
    private string GetPeerDisplayName(string peerId)
    {
        lock(_connections_lock)
        {
            if(_connections.TryGetValue(peerId, out Peer? peer))
            {
                if(!string.IsNullOrWhiteSpace(peer.Name))
                {
                    return peer.Name;
                }

                return $"{peer.Address}:{peer.Port}";
            }
        }

        return peerId;
    }

    /// <summary>
    /// Gets a connected peer by the index shown in the /peers command.
    /// </summary>
    /// <param name="peerIndex">The peer index shown by /peers.</param>
    /// <returns>The matching peer, or null if the index is invalid.</returns>
    public Peer? GetPeerByIndex(int peerIndex)
    {
        lock(_connections_lock)
        {
            List<Peer> peers = _connections.Values.ToList();

            if(peerIndex < 0 || peerIndex >= peers.Count)
                return null;

            return peers[peerIndex];
        }
    }

    /// <summary>
    /// Checks whether a connected peer with the given name already exists.
    /// </summary>
    /// <param name="peerName">The peer name to search for.</param>
    /// <returns>True if a matching connected peer exists; otherwise false.</returns>
    public bool HasConnectionWithName(string peerName)
    {
        lock(_connections_lock)
        {
            return _connections.Values.Any(peer =>
                peer.IsConnected &&
                string.Equals(peer.Name, peerName, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Records the listening endpoint discovered for a peer and updates any matching active connection.
    /// </summary>
    /// <param name="peerName">The discovered peer name.</param>
    /// <param name="address">The peer IP address.</param>
    /// <param name="port">The peer TCP listening port.</param>
    public void RecordDiscoveredEndpoint(string peerName, IPAddress address, int port)
    {
        lock(_connections_lock)
        {
            _knownPeerEndpoints[peerName] = (address, port);

            foreach(Peer peer in _connections.Values)
            {
                if(string.Equals(peer.Name, peerName, StringComparison.Ordinal))
                {
                    peer.Address = address;
                    peer.Port = port;
                }
            }
        }
    }

    /// <summary>
    /// Applies a previously discovered listening endpoint to a connected peer when its name becomes known.
    /// </summary>
    /// <param name="peer">The peer to update.</param>
    private void ApplyDiscoveredEndpoint(Peer peer)
    {
        if(string.IsNullOrWhiteSpace(peer.Name))
            return;

        lock(_connections_lock)
        {
            if(_knownPeerEndpoints.TryGetValue(peer.Name, out var endpoint))
            {
                peer.Address = endpoint.Address;
                peer.Port = endpoint.Port;
            }
        }
    }

    /// <summary>
    /// Checks whether a connection already exists for the given host and port.
    /// </summary>
    /// <param name="host">The peer host or IP address.</param>
    /// <param name="port">The peer TCP port.</param>
    /// <returns>True if a matching active connection exists; otherwise false.</returns>
    public bool HasConnectionTo(string host, int port)
    {
        IPAddress? targetAddress = null;

        if(host == "localhost")
            host = "127.0.0.1";

        if(!IPAddress.TryParse(host, out targetAddress))
        {
            try
            {
                IPAddress[] addresses = Dns.GetHostAddresses(host);
                targetAddress = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork);
            }
            catch(SocketException)
            {
                return false;
            }
        }

        if(targetAddress == null)
            return false;

        lock(_connections_lock)
        {
            return _connections.Values.Any(peer =>
                peer.Address != null &&
                peer.Address.Equals(targetAddress) &&
                peer.Port == port &&
                peer.IsConnected);
        }
    }

    /// <summary>
    /// Gets the names of all rooms known by this peer.
    /// </summary>
    /// <returns>The known room names.</returns>
    public IEnumerable<string> ListRooms()
    {
        lock(_roomsLock)
        {
            return _rooms.Keys;
        }
    }

    /// <summary>
    /// Gets the rooms associated with a connected peer.
    /// </summary>
    /// <param name="peer">The peer to check.</param>
    /// <returns>The rooms that contain the peer.</returns>
    public IEnumerable<string> GetRoomsForPeer(Peer peer)
    {
        lock (_roomsLock)
        {
            return _rooms
                .Where(roomEntry => roomEntry.Value.Contains(peer))
                .Select(roomEntry => roomEntry.Key)
                .ToList();
        }
    }

    /// <summary>
    /// Gets the connected peers known to be in a room.
    /// </summary>
    /// <param name="room_name">The room name.</param>
    /// <returns>A list of peers in the room, or null if the room is unknown.</returns>
    public List<Peer>? GetPeersInRoom(string room_name)
    {
        lock(_roomsLock)
        {
            if(_rooms.TryGetValue(room_name, out var peersInRoom))
                return peersInRoom.ToList();
        }

        return null; // Returns null if no peers in room
    }

    /// <summary>
    /// Creates a room locally if it does not already exist.
    /// </summary>
    /// <param name="room_name">The room name.</param>
    /// <returns>True if the room already existed; otherwise false.</returns>
    public bool CreateRoom(string room_name)
    {
        // returns true if the room existed already, 
        // false if the room did not exist (and was thus added)
        lock(_roomsLock)
        {
            if(_rooms.TryGetValue(room_name, out _)) return true;
            _rooms.Add(room_name, new List<Peer>());
        }
        return false;
    }

    /// <summary>
    /// Adds a peer to a known room.
    /// </summary>
    /// <param name="room_name">The room name.</param>
    /// <param name="peer">The peer to add.</param>
    /// <returns>True if the peer is in the room; otherwise false.</returns>
    public bool AddToRoom(string room_name, Peer peer)
    {
        // returns false if the room doesn't exist,
        // true if the room was updated with the peer (or already had the peer)
        lock(_roomsLock)
        {
            List<Peer>? currentPeersInRoom;

            if(!_rooms.TryGetValue(room_name, out currentPeersInRoom))
                return false;

            if(currentPeersInRoom!.Contains(peer))
                return true;

            currentPeersInRoom.Add(peer);
            _rooms[room_name] = currentPeersInRoom;
        }
        return true;
    }

    /// <summary>
    /// Removes a peer from a room if present.
    /// </summary>
    /// <param name="room_name">The room name.</param>
    /// <param name="peer">The peer to remove.</param>
    /// <returns>True if the room exists and removal is complete; otherwise false.</returns>
    public bool RemoveFromRoom(string room_name, Peer peer)
    {
        // returns false if the room doesn't exist,
        // true if the peer was removed (or didn't exist in the room)
        lock(_roomsLock)
        {
            List<Peer>? currentPeersInRoom;
            if(!_rooms.TryGetValue(room_name, out currentPeersInRoom))
                return false;

            // remove from room
            if(currentPeersInRoom!.Contains(peer)) 
            {
                currentPeersInRoom.Remove(peer);
                _rooms[room_name] = currentPeersInRoom;
                return true;
            }
        }
        return true;
    }

}
