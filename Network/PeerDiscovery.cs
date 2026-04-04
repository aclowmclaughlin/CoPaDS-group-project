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
        TcpPort = tcpPort;
        _cancellationTokenSource = new CancellationTokenSource();
        _udpClient = new UdpClient(_broadcastPort)
        {
            EnableBroadcast = true
        };
        _listenTask = ListenLoop();
        _broadcastTask = BroadcastLoop();
    }

    /// <summary>
    /// Periodically broadcast our presence to the network.
    /// </summary>
    private async Task BroadcastLoop()
    {
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, _broadcastPort);
        CancellationToken cancellationToken = _cancellationTokenSource!.Token;
        byte[] broadcastMessage = Encoding.UTF8.GetBytes($"PEER_MESSAGE_PREFIX:{LocalPeerId}:{TcpPort}");
        while(!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _udpClient!.SendAsync(broadcastMessage, endPoint, cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            } 
            catch (SocketException) {} 
            catch(TaskCanceledException) {} 
            catch (OperationCanceledException) {}
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
            try
            {
                // wait for message received
                var result = await _udpClient!.ReceiveAsync(cancellationToken);

                string message = Encoding.UTF8.GetString(result.Buffer);
                if (message.StartsWith(PEER_MESSAGE_PREFIX))
                {
                    ProcessDiscoveryMessage(message, result.RemoteEndPoint.Address);
                }
            } 
            catch(SocketException) {} 
            catch(OperationCanceledException) {}
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
        Peer this_peer = new Peer(){Port=port, Id=peerId};
        
        // check if peer has not been seen before
        if(!_knownPeers.ContainsKey(peerId))
        {
            _knownPeers[peerId] = this_peer;
            OnPeerDiscovered!.Invoke(this_peer);
        }

        // Always do these things:
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
        _cancellationTokenSource!.Cancel();
        await _listenTask!;
        await _broadcastTask!;
        _udpClient!.Close();
    }
}