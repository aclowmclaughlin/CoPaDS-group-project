// Team 7: Rue Clow-McLaughli, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger
// check MessageQueue, TcpServer


using System.Data;
using System.Linq.Expressions;
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
    /// Connect to a peer at the specified address and port.
    /// </summary>
    public async Task<bool> ConnectAsync(string host, int port)
    {
        //TODO: add error other handling? (cringe)
        try
        {
            var client = new TcpClient();
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

            lock(_lock) { _connections[peer.Id] = peer; };

            OnConnected?.Invoke(peer);

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
    /// Receive loop for a connected peer - reads messages until disconnection.
    /// </summary>
    private async Task ReceiveLoop(Peer peer)
    {
        try
        {
            StreamReader? stream = new StreamReader(peer.Stream); // possible null, fix later
            while (peer.IsConnected) {
                var line = await stream.ReadLineAsync(); // need to wait until input
                if (line == null) break;

                // core/message.cs
                var message = new Message
                {
                    Sender = peer.Name,
                    Content = line,
                    Timestamp = DateTime.UtcNow
                };

                OnMessageReceived?.Invoke(peer, message);
            }
        
        }

        catch (IOException IOE){
            Console.WriteLine($"Connection lost: {IOE.Message}");
        }
        
        finally
        {
            Disconnect(peer.Id);
        }
    }

    /// <summary>
    /// Send a message to a specific peer.
    /// </summary>
    public async Task SendAsync(string peerId, string message)
    {  
        Peer? peer;
        bool found = false;
        lock (_lock)
        {
            // if (_connections.ContainsKey(peerId)) { peer = _connections[peerId]; };
            found = _connections.TryGetValue(peerId, out peer);
        }

        if (found && peer?.Stream != null && peer.IsConnected == true)
        {
            StreamWriter stream = new StreamWriter(peer.Stream, leaveOpen: true);
            stream.Write(message); // this also needs to be await, but gives "Cannot await 'void'" error
            await stream.FlushAsync();
        }
    }

    /// <summary>
    /// Broadcast a message to all connected peers.
    /// </summary>
    public async Task BroadcastAsync(string message)
    {
        List<Peer> allPeers;
        lock (_lock)
        {
            allPeers = _connections.Values.ToList();
        }

        foreach (Peer p in allPeers)
        {
            await SendAsync(p.Id, message);
        }
        
    }

    /// <summary>
    /// Disconnect from a peer.
    /// </summary>
    public void Disconnect(string peerId)
    {  
        Peer? temp;
        lock (_lock)
        {
            _connections.TryGetValue(peerId, out temp);
            if (temp != null)
            {
                _connections.Remove(peerId);
                temp.IsConnected = false;
                temp.Client?.Dispose();
                temp.Stream?.Dispose();
                OnDisconnected?.Invoke(temp);
            }
        }
    }

    /// <summary>
    /// Get all currently connected peers.
    /// Remember to use proper locking when accessing _connections.
    /// </summary>
    public IEnumerable<Peer> GetConnectedPeers()
    {
        lock (_lock)
        {
            return _connections.Values.ToList();
        }
    }
}
