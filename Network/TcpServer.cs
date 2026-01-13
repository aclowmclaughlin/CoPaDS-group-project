// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger

using System.Net;
using System.Net.Sockets;
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
    /// Start listening for incoming connections on the specified port
    /// </summary>
    public void Start(int port)
    {
        // TODO: Implement server startup
        Port = port;
        _cancellationTokenSource = new CancellationTokenSource();

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        IsListening = true;

        // Start listen thread
        _listenThread = new Thread(ListenLoop);
        _listenThread.Start();

        Console.WriteLine($"Listening on port {port}...");
    }

    private void ListenLoop()
    {
        // TODO: Accept incoming connections in a loop
        try
        {
            while (!_cancellationTokenSource!.Token.IsCancellationRequested)
            {
                if (_listener!.Pending())
                {
                    var client = _listener.AcceptTcpClient();
                    HandleNewConnection(client);
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Socket error: {ex.Message}");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"IO error: {ex.Message}");
        }
    }

    private void HandleNewConnection(TcpClient client)
    {
        // TODO: Create peer and start receive thread
        var peer = new Peer
        {
            Client = client,
            Stream = client.GetStream(),
            Address = ((IPEndPoint)client.Client.RemoteEndPoint!).Address,
            Port = ((IPEndPoint)client.Client.RemoteEndPoint!).Port,
            IsConnected = true
        };

        lock (_connectedPeers)
        {
            _connectedPeers.Add(peer);
        }

        OnPeerConnected?.Invoke(peer);

        // Start receive thread for this peer
        var receiveThread = new Thread(() => ReceiveLoop(peer));
        receiveThread.Start();
    }

    private void ReceiveLoop(Peer peer)
    {
        // TODO: Receive messages from peer
        try
        {
            using var reader = new StreamReader(peer.Stream!);
            while (peer.IsConnected && !_cancellationTokenSource!.Token.IsCancellationRequested)
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    // Connection closed
                    break;
                }

                var message = new Message
                {
                    Sender = peer.Name,
                    Content = line,
                    Timestamp = DateTime.Now
                };

                OnMessageReceived?.Invoke(peer, message);
            }
        }
        catch (IOException)
        {
            // Connection lost
        }
        finally
        {
            DisconnectPeer(peer);
        }
    }

    private void DisconnectPeer(Peer peer)
    {
        peer.IsConnected = false;
        peer.Client?.Dispose();
        peer.Stream?.Dispose();

        lock (_connectedPeers)
        {
            _connectedPeers.Remove(peer);
        }

        OnPeerDisconnected?.Invoke(peer);
    }

    /// <summary>
    /// Stop the server
    /// </summary>
    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
        _listener?.Stop();
        IsListening = false;

        // Close all connections
        lock (_connectedPeers)
        {
            foreach (var peer in _connectedPeers.ToList())
            {
                DisconnectPeer(peer);
            }
        }

        _listenThread?.Join(1000);
    }

    public IEnumerable<Peer> GetConnectedPeers()
    {
        lock (_connectedPeers)
        {
            return _connectedPeers.ToList();
        }
    }
}
