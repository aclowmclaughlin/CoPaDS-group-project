// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger

using System.Net;
using System.Net.Sockets;
using SecureMessenger.Core;

namespace SecureMessenger.Network;

/// <summary>
/// Handles outgoing TCP connections to other peers.
/// </summary>
public class TcpClientHandler
{
    private readonly Dictionary<string, Peer> _connections = new();
    private readonly object _lock = new();

    public event Action<Peer>? OnConnected;
    public event Action<Peer>? OnDisconnected;
    public event Action<Peer, Message>? OnMessageReceived;

    /// <summary>
    /// Connect to a peer at the specified address and port
    /// </summary>
    public async Task<bool> ConnectAsync(string host, int port)
    {
        // TODO: Implement connection to peer
        try
        {
            var client = new TcpClient();
            await client.ConnectAsync(host, port);

            var peer = new Peer
            {
                Client = client,
                Stream = client.GetStream(),
                Address = IPAddress.Parse(host),
                Port = port,
                IsConnected = true
            };

            lock (_lock)
            {
                _connections[peer.Id] = peer;
            }

            OnConnected?.Invoke(peer);

            // Start receive thread
            _ = Task.Run(() => ReceiveLoop(peer));

            Console.WriteLine($"Connected to {host}:{port}");
            return true;
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
            return false;
        }
    }

    private async Task ReceiveLoop(Peer peer)
    {
        // TODO: Receive messages from peer
        try
        {
            using var reader = new StreamReader(peer.Stream!);
            while (peer.IsConnected)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

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
            Disconnect(peer.Id);
        }
    }

    /// <summary>
    /// Send a message to a specific peer
    /// </summary>
    public async Task SendAsync(string peerId, string message)
    {
        // TODO: Send message to peer
        Peer? peer;
        lock (_lock)
        {
            _connections.TryGetValue(peerId, out peer);
        }

        if (peer?.Stream != null && peer.IsConnected)
        {
            using var writer = new StreamWriter(peer.Stream, leaveOpen: true);
            await writer.WriteLineAsync(message);
            await writer.FlushAsync();
        }
    }

    /// <summary>
    /// Broadcast a message to all connected peers
    /// </summary>
    public async Task BroadcastAsync(string message)
    {
        List<Peer> peers;
        lock (_lock)
        {
            peers = _connections.Values.ToList();
        }

        foreach (var peer in peers)
        {
            await SendAsync(peer.Id, message);
        }
    }

    /// <summary>
    /// Disconnect from a peer
    /// </summary>
    public void Disconnect(string peerId)
    {
        Peer? peer;
        lock (_lock)
        {
            if (_connections.TryGetValue(peerId, out peer))
            {
                _connections.Remove(peerId);
            }
        }

        if (peer != null)
        {
            peer.IsConnected = false;
            peer.Client?.Dispose();
            peer.Stream?.Dispose();
            OnDisconnected?.Invoke(peer);
        }
    }

    public IEnumerable<Peer> GetConnectedPeers()
    {
        lock (_lock)
        {
            return _connections.Values.ToList();
        }
    }
}
