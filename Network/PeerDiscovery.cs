// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using SecureMessenger.Core;

namespace SecureMessenger.Network;

/// <summary>
/// Sprint 3: UDP-based peer discovery using broadcast.
/// Broadcasts presence and listens for other peers on the local network.
/// </summary>
public class PeerDiscovery
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ConcurrentDictionary<string, Peer> _knownPeers = new();
    private readonly int _broadcastPort = 5001;
    private Thread? _listenThread;
    private Thread? _broadcastThread;

    public event Action<Peer>? OnPeerDiscovered;
    public event Action<Peer>? OnPeerLost;

    public int TcpPort { get; private set; }
    public string LocalPeerId { get; } = Guid.NewGuid().ToString()[..8];

    /// <summary>
    /// Start broadcasting presence and listening for other peers
    /// </summary>
    public void Start(int tcpPort)
    {
        // TODO: Implement UDP broadcast discovery
        TcpPort = tcpPort;
        _cancellationTokenSource = new CancellationTokenSource();

        _udpClient = new UdpClient(_broadcastPort);
        _udpClient.EnableBroadcast = true;

        // Start listening for broadcasts
        _listenThread = new Thread(ListenLoop);
        _listenThread.Start();

        // Start broadcasting our presence
        _broadcastThread = new Thread(BroadcastLoop);
        _broadcastThread.Start();

        // Start timeout checker
        _ = Task.Run(TimeoutCheckLoop);
    }

    private void BroadcastLoop()
    {
        // TODO: Periodically broadcast our presence
        var endpoint = new IPEndPoint(IPAddress.Broadcast, _broadcastPort);

        while (!_cancellationTokenSource!.Token.IsCancellationRequested)
        {
            try
            {
                var message = $"PEER:{LocalPeerId}:{TcpPort}";
                var data = Encoding.UTF8.GetBytes(message);
                _udpClient!.Send(data, data.Length, endpoint);
            }
            catch (SocketException)
            {
                // Ignore broadcast errors
            }

            Thread.Sleep(5000); // Broadcast every 5 seconds
        }
    }

    private void ListenLoop()
    {
        // TODO: Listen for peer broadcasts
        var anyEndpoint = new IPEndPoint(IPAddress.Any, _broadcastPort);

        while (!_cancellationTokenSource!.Token.IsCancellationRequested)
        {
            try
            {
                var data = _udpClient!.Receive(ref anyEndpoint);
                var message = Encoding.UTF8.GetString(data);

                if (message.StartsWith("PEER:"))
                {
                    ProcessDiscoveryMessage(message, anyEndpoint.Address);
                }
            }
            catch (SocketException)
            {
                // Ignore receive errors
            }
        }
    }

    private void ProcessDiscoveryMessage(string message, IPAddress senderAddress)
    {
        // TODO: Parse discovery message and add peer
        var parts = message.Split(':');
        if (parts.Length >= 3)
        {
            var peerId = parts[1];
            var port = int.Parse(parts[2]);

            // Don't add ourselves
            if (peerId == LocalPeerId) return;

            var peer = new Peer
            {
                Id = peerId,
                Address = senderAddress,
                Port = port,
                LastSeen = DateTime.Now,
                IsConnected = false
            };

            if (_knownPeers.TryAdd(peerId, peer))
            {
                OnPeerDiscovered?.Invoke(peer);
            }
            else
            {
                // Update last seen
                _knownPeers[peerId].LastSeen = DateTime.Now;
            }
        }
    }

    private async Task TimeoutCheckLoop()
    {
        // TODO: Remove peers that haven't been seen recently (30 second timeout)
        while (!_cancellationTokenSource!.Token.IsCancellationRequested)
        {
            var timeout = TimeSpan.FromSeconds(30);
            var now = DateTime.Now;

            foreach (var kvp in _knownPeers)
            {
                if (now - kvp.Value.LastSeen > timeout)
                {
                    if (_knownPeers.TryRemove(kvp.Key, out var peer))
                    {
                        OnPeerLost?.Invoke(peer);
                    }
                }
            }

            await Task.Delay(5000);
        }
    }

    /// <summary>
    /// Get list of known peers
    /// </summary>
    public IEnumerable<Peer> GetKnownPeers()
    {
        return _knownPeers.Values.ToList();
    }

    /// <summary>
    /// Stop discovery
    /// </summary>
    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
        _udpClient?.Close();
        _listenThread?.Join(1000);
        _broadcastThread?.Join(1000);
    }
}
