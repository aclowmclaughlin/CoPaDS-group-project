// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
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
///
/// Discovery Protocol:
/// - Message format: "PEER:{peerId}:{tcpPort}"
/// - Example: "PEER:abc12345:5000"
/// - Broadcast every 5 seconds
/// - Peers timeout after 30 seconds of no broadcasts
/// </summary>
public class PeerDiscovery
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ConcurrentDictionary<string, Peer> _knownPeers = new();

    private readonly HeartbeatMonitor heartbeatMonitor;
    private readonly int _broadcastPort = 5001;
    private Task? _listenTask;
    private Task? _broadcastTask;

    public event Action<Peer> OnPeerDiscovered;

    public int TcpPort { get; private set; }
    public string LocalPeerId { get; private set; }

    private static readonly string PEER_MESSAGE_PREFIX = "PEER";


    public PeerDiscovery(string ownId, HeartbeatMonitor heartbeatMonitor, Action<Peer> onPeerDiscovered)
    {
        LocalPeerId = ownId;
        this.heartbeatMonitor = heartbeatMonitor;
        this.OnPeerDiscovered += onPeerDiscovered;
    }

    /// <summary>
    /// Start broadcasting presence and listening for other peers.
    /// </summary>
    public void Start(int tcpPort)
    {
        if(_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            return;

        TcpPort = tcpPort;
        _cancellationTokenSource = new CancellationTokenSource();

        _udpClient = new UdpClient(AddressFamily.InterNetwork);
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpClient.Client.ExclusiveAddressUse = false;
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _broadcastPort));
        _udpClient.EnableBroadcast = true;

        _listenTask = Task.Run(ListenLoop);
        _broadcastTask = Task.Run(BroadcastLoop);
    }

    /// <summary>
    /// Periodically broadcast our presence to the network.
    /// </summary>
    private async Task BroadcastLoop()
    {
        IPEndPoint[] endpoints =
        {
            new IPEndPoint(IPAddress.Broadcast, _broadcastPort),
            new IPEndPoint(IPAddress.Loopback, _broadcastPort)
        };

        CancellationToken cancellationToken = _cancellationTokenSource!.Token;

        while(!cancellationToken.IsCancellationRequested)
        {
            byte[] broadcastMessage = Encoding.UTF8.GetBytes($"{PEER_MESSAGE_PREFIX}:{LocalPeerId}:{TcpPort}");

            foreach(IPEndPoint endpoint in endpoints)
            {
                try {
                    await _udpClient!.SendAsync(broadcastMessage, endpoint, cancellationToken);
                }
                catch(SocketException) {
                    // Broadcast errors are ignored so discovery can keep trying
                }
                catch(ObjectDisposedException) {
                    return;
                }
                catch(OperationCanceledException) {
                    return;
                }
            }

            try {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch(OperationCanceledException) {
                return;
            }
        }
    }

    /// <summary>
    /// Listen for peer broadcast messages.
    /// </summary>
    private async Task ListenLoop()
    {
        CancellationToken cancellationToken = _cancellationTokenSource!.Token;
        while(!cancellationToken.IsCancellationRequested)
        {
            try {
                UdpReceiveResult result = await _udpClient!.ReceiveAsync(cancellationToken);

                string message = Encoding.UTF8.GetString(result.Buffer);

                if(message.StartsWith(PEER_MESSAGE_PREFIX))
                {
                    ProcessDiscoveryMessage(message, result.RemoteEndPoint.Address);
                }
            }
            catch(SocketException) {
                // Receive errors are ignored so discovery can keep listening
            }
            catch(ObjectDisposedException) {
                return;
            }
            catch(OperationCanceledException) {
                return;
            }
        }
    }

    /// <summary>
    /// Parse a discovery message and add/update the peer.
    /// </summary>
    private void ProcessDiscoveryMessage(string message, IPAddress senderAddress)
    {
        string[] split_message = message.Split(":");
        // check that message is long enough
        if (split_message.Length != 3) { return; }
        string peerId = split_message[1];
        int port = -1;
        // try to parse the port part of the message into an int
        if (!int.TryParse(split_message[2], out port)) { return; }
        // if the message is from us, ignore it.
        if (peerId == LocalPeerId) { return; }

        // create a new Peer object
        Peer discoveredPeer = new Peer
        {
            Id = peerId,
            Address = senderAddress,
            Port = port,
            IsConnected = false
        };
        
        // check if peer has not been seen before
        if(_knownPeers.TryAdd(peerId, discoveredPeer))
        {
            OnPeerDiscovered!.Invoke(discoveredPeer);
        }
        else // Update known peers on discovery
        {
            _knownPeers[peerId] = discoveredPeer;
        }

        // Record that we did receive a heartbeat from the peer.
        heartbeatMonitor.RecordHeartbeat(peerId);
        
    }

    /// <summary>
    /// Get list of known peers.
    /// </summary>
    public IEnumerable<Peer> GetKnownPeers()
    {
        return _knownPeers.Values.ToList();
    }

    /// <summary>
    /// Stop discovery.
    /// </summary>
    public async Task Stop()
    {
        if(_cancellationTokenSource == null)
            return;

        _cancellationTokenSource.Cancel();
        _udpClient?.Close();

        Task[] tasks = new[]
        {
            _listenTask,
            _broadcastTask
        }
        .Where(task => task != null)
        .Cast<Task>()
        .ToArray();

        try {
            await Task.WhenAll(tasks);
        }
        catch(OperationCanceledException) {
            // Discovery is shutting down normally
        }
        catch(ObjectDisposedException) {
            // Socket disposal is expected during shutdown
        }

        _udpClient?.Dispose();
        _udpClient = null;

        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;

        _listenTask = null;
        _broadcastTask = null;
    }
}