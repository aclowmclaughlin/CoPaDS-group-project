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
/// Handles incoming and outgoing TCP connections from and to other peers.
/// </summary>
public class TcpPeerHandler
{
    private TcpListener? _listener;
    private readonly Dictionary<string, Peer> _connections = new();

    public string localUserName = string.Empty;

    private readonly object _connections_lock = new();
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

    public int Port { get; private set; }
    public bool IsListening { get; private set; }

    public TcpPeerHandler()
    {
        ourMessageSigner = new MessageSigner(ourRSA.Rsa);
    }

    /// <summary>
    /// Start listening for incoming connections on the specified port.
    /// </summary>
    public void Start(int port)
    {
        Console.WriteLine($"Starting Peer Handler...");
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

        // Print a message indicating the server is listening
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
                    HandleNewConnection(client);
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
    /// Performs the key exchange from the sender (ConnectAsync caller) side.
    /// </summary>
    /// <param name="peer">What Peer we are exchanging keys with</param>
    private async Task ExchangeKeySender(Peer peer)
    {
        KeyExchange keyExchange = new();
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
        keyExchange.ReceivePublicKey(peerPublicKey);
        Console.WriteLine($"Received initial public key ({keyLength} bytes) from {peer.Address}:{peer.Port}");
        
        // wait to receive their private key.
        keyLengthBytes = new byte[4];
        await peer.Stream!.ReadExactlyAsync(keyLengthBytes, 0, 4);

        keyLength = BitConverter.ToInt32(keyLengthBytes, 0);
        byte[] peerAesKey = new byte[keyLength];
        await peer.Stream.ReadExactlyAsync(peerAesKey, 0, keyLength);

        // save aes key
        keyExchange.ReceiveEncryptedSessionKey(peerAesKey);
        keyExchange.Complete();
        peer.AesKey = peerAesKey;
        Console.WriteLine($"Received private AES key ({keyLength} bytes) from {peer.Address}:{peer.Port}");
    }

    /// <summary>
    /// Performs the key exchange from the receiver (HandleNewConnection caller) side.
    /// </summary>
    /// <param name="peer">What Peer we are exchanging keys with</param>
    private async Task ExchangeKeyReceiver(Peer peer)
    {
        KeyExchange keyExchange = new();
        // record our public key
        var ourPublicKey = keyExchange.GetPublicKey();
        // receive their public key
        byte[] keyLengthBytes = new byte[4];
        await peer.Stream!.ReadExactlyAsync(keyLengthBytes, 0, 4);

        int keyLength = BitConverter.ToInt32(keyLengthBytes, 0);
        byte[] peerPublicKey = new byte[keyLength];
        await peer.Stream.ReadExactlyAsync(peerPublicKey, 0, keyLength);

        peer.PublicKey = peerPublicKey;
        keyExchange.ReceivePublicKey(peerPublicKey);
        Console.WriteLine($"Received initial public key ({keyLength} bytes) from {peer.Address}:{peer.Port}");
        
        // send our public key
        byte[] lengthBytes = BitConverter.GetBytes(ourPublicKey.Length);

        await peer.Stream!.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await peer.Stream.WriteAsync(ourPublicKey, 0, ourPublicKey.Length);
        await peer.Stream.FlushAsync();
        // send a private key.
        byte[] aesSessionKey = keyExchange.CreateEncryptedSessionKey();
        
        lengthBytes = BitConverter.GetBytes(aesSessionKey.Length);
        await peer.Stream!.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await peer.Stream.WriteAsync(aesSessionKey, 0, aesSessionKey.Length);
        await peer.Stream.FlushAsync();
        // save private key.
        peer.AesKey = aesSessionKey;
        keyExchange.Complete();
        Console.WriteLine($"Sent AES key to {peer.Address}:{peer.Port}");
    }
    
    /// <summary>
    /// Connect to a peer at the specified address and port.
    /// </summary>
    public async Task<bool> ConnectAsync(string host, int port)
    {
        //TODO: add error other handling? (cringe)
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

            _ = Task.Run(() => ReceiveLoop(peer));
            
            return true;
        }
        
        catch (SocketException SE) 
        {
            Console.WriteLine($"Error: {SE.Message}");
            return false;
        }
    }


    /// <summary>
    /// Handle a new incoming connection by creating a Peer and starting its receive thread.
    /// </summary>
    private async void HandleNewConnection(TcpClient client)
    {
        // Create a new Peer object with:
        // - Client = the TcpClient
        // - Stream = client.GetStream()
        // - Address = extracted from client.Client.RemoteEndPoint
        // - Port = extracted from client.Client.RemoteEndPoint
        // - IsConnected = true
        var peer = new Peer {
            Client = client,
            Stream = client.GetStream(),
            Address = ((IPEndPoint)client.Client.RemoteEndPoint!).Address,
            Port = ((IPEndPoint)client.Client.RemoteEndPoint!).Port,
            IsConnected = true
        };

        await ExchangeKeyReceiver(peer);

        // Add the peer to _connectedPeers (with proper locking)
        lock(_connections_lock)
        {
            _connections.Add(peer.Id, peer);
        }

        // Invoke OnPeerConnected event
        OnPeerConnected?.Invoke(peer);

        // Create and start a new Thread running ReceiveLoop for this peer
        var receiveThread = new Thread(async () => await ReceiveLoop(peer));
        receiveThread.Start();
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

                if (!string.IsNullOrWhiteSpace(message.Sender) && string.IsNullOrWhiteSpace(peer.Name))
                    peer.Name = message.Sender;
                
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

    public Message CreateRoomsListingMessage()
    {
        //TODO implement this- should create some string representation
        // of the _our_rooms variable
    }

    public bool HandleRoomsListingMessage(Message roomsListingMessage, Peer senderPeer)
    {
        //TODO implement this- should unpack the string representation
        // of the Room Listing Message, and add the sender Peer to all the
        // rooms it says it is in.
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
            await SendEncryptedMessageAsync(peer, message);
        }
        return true;
    }

    // /// <summary>
    // /// Broadcast a message to all connected peers.
    // /// </summary>
    public async Task BroadcastAsync(Message msg)
    {
        List<Peer> allPeers;
        lock (_connections_lock)
        {
            allPeers = _connections.Values.ToList();
        }

        foreach (Peer peer in allPeers)
        {
            await SendAsync(peer, msg);
        }
    }

    /// <summary>
    /// Send a message to specific peer
    /// </summary>
    public async Task SendAsync(Peer peer, Message msg)
    {
        if (peer.Stream == null || !peer.IsConnected)
        {
            return;
        }

        using var writer = new StreamWriter(peer.Stream, leaveOpen: true);
        string serializedMessage = JsonSerializer.Serialize(msg);
        string total_msg = serializedMessage.Length + "\n" + serializedMessage;

        await writer.WriteAsync(total_msg);
        await writer.FlushAsync();
    }

    /// <summary>
    /// Encrypts and signs the provided message 
    /// then sends it to the specified peer.
    /// </summary>
    public async Task SendEncryptedMessageAsync(Peer peer, Message msg)
    {
        Message encryptedMsg = peer.CreateEncryptedMessage(msg);
        Message signedMessage = this.SignEncryptedMessage(encryptedMsg);
        await this.SendAsync(peer, signedMessage);
    }

    /// <summary>
    /// Signs an encrypted message
    /// </summary>
    /// <param name="unsignedMessage">The unsigned message</param>
    /// <returns>The new signed message</returns>
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
    /// Signs an unencrypted message (I don't think this is actually useful)
    /// </summary>
    /// <param name="unsignedMessage">The unsigned, unencrypted message</param>
    /// <returns>The new signed message</returns>
    public Message SignUnencryptedMessage(Message unsignedMessage)
    {
        byte[] signature = ourMessageSigner.SignData(Encoding.UTF8.GetBytes(unsignedMessage.Content));
        
        return new Message
        {
            Type                = unsignedMessage.Type,
            Sender              = unsignedMessage.Sender,
            Room                = unsignedMessage.Room,
            Content             = unsignedMessage.Content,
            Signature           = signature,
            Timestamp           = unsignedMessage.Timestamp
        };
    }

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

    public void ListPeers()
    {
        List<Peer> peers_list;
        lock (_connections_lock)
        {
            peers_list = _connections.Values.ToList();
        }
        if (peers_list == null || peers_list.Count == 0)
        {
            Console.WriteLine("No Known Peers.");
            return;
        }
        int i = 0;
        foreach(Peer peer in peers_list)
        {
            Console.WriteLine($"Peer [{i}]: {peer}");
            i++;
        }
    }

    /// <summary>
    /// Clean up a disconnected peer.
    /// </summary>
    private void DisconnectPeer(Peer peer)
    {
        // Set peer.IsConnected to false
        peer.IsConnected = false;
        // Dispose the peer's Client and Stream
        peer.Client?.Dispose();
        peer.Stream?.Dispose();

        // Remove the peer from _connectedPeers (with proper locking)
        lock(_connections_lock)
        {
            _connections.Remove(peer.Id);
        }

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
    /// Stop the server and close all connections.
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
    }

    /// <summary>
    /// Get a list of currently connected peers.
    /// </summary>
    public IEnumerable<Peer> GetConnectedPeers()
    {
        lock (_connections)
        {
            return _connections.Values.ToList();
        }
    }

    public IEnumerable<string> ListRooms()
    {
        lock(_roomsLock)
        {
            return _rooms.Keys;
        }
    }

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

    public Peer? GetPeerByName(string name)
    {
        Peer? peer = null;
        lock(_connections)
        {
            bool exists = _connections.TryGetValue(name, out peer);
        }
        return peer;
    }

    public List<Peer>? GetPeersInRoom(string room_name)
    {
        lock(_roomsLock)
        {
            if(_rooms.TryGetValue(room_name, out var peersInRoom))
                return peersInRoom.ToList();
        }

        return null; // Returns null if no peers in room
    }

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
