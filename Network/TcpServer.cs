// Team 7: Rue Clow-McLaughli, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using SecureMessenger.Core;

namespace SecureMessenger.Network;

/// <summary>
/// TCP server that listens for incoming peer connections.
/// Each peer runs both a server (to accept connections) and client (to initiate connections).
/// </summary>
public class TcpServer
{
    private TcpListener? _listener;
    private readonly List<Peer> _connectedPeers = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Thread? _listenThread;

    public event Action<Peer>? OnPeerConnected;
    public event Action<Peer>? OnPeerDisconnected;
    public event Action<Peer, Message>? OnMessageReceived;

    public int Port { get; private set; }
    public bool IsListening { get; private set; }

    /// <summary>
    /// Start listening for incoming connections on the specified port.
    /// </summary>
    public void Start(int port)
    {
        Console.WriteLine($"Starting server...");
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
        Console.WriteLine($"Server started and listening on port {port}");
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
    /// Handle a new incoming connection by creating a Peer and starting its receive thread.
    /// </summary>
    private void HandleNewConnection(TcpClient client)
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

        // Add the peer to _connectedPeers (with proper locking)
        lock(_connectedPeers)
        {
            _connectedPeers.Add(peer);
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
            // Create a StreamReader from the peer's stream
            using var reader = new StreamReader(peer.Stream!);

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
                }

                OnMessageReceived?.Invoke(peer, message);
            }
        }
        catch(IOException e) // Handle IOException (connection lost)
        {
            Console.WriteLine($"Connection Lost - IO exception: {e.Message}");
        }
        finally // In finally block, call DisconnectPeer
        {
            DisconnectPeer(peer);
        }
    }

    /// <summary>
    /// Broadcast a message to all connected peers.
    /// </summary>
    public async Task BroadcastAsync(Message msg)
    {
        List<Peer> allPeers;
        lock (_connectedPeers)
        {
            allPeers = _connectedPeers;
        }

        foreach (Peer peer in allPeers)
        {
            StreamWriter stream = new StreamWriter(peer.Stream, leaveOpen: true);
            string serialized_msg = JsonSerializer.Serialize(msg);
            string total_msg = serialized_msg.Length.ToString() + '\n'+ serialized_msg;
            await stream.WriteAsync(total_msg); // this also needs to be await, but gives "Cannot await 'void'" error
            await stream.FlushAsync();
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
        lock(_connectedPeers)
        {
            _connectedPeers.Remove(peer);
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
        lock(_connectedPeers)
        {
            foreach(Peer peer in _connectedPeers.ToList())
            {
                DisconnectPeer(peer);
            }
        }
        
        // Wait for the listen thread to finish (with timeout)
        _listenThread?.Join(1000);
    }

    /// <summary>
    /// Get a list of currently connected peers.
    /// </summary>
    public IEnumerable<Peer> GetConnectedPeers()
    {
        lock (_connectedPeers)
        {
            return _connectedPeers.ToList();
        }
    }
}
