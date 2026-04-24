// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using System.Collections.Concurrent;

namespace SecureMessenger.Network;

public enum SendResult
{
    Success,
    PeerDisconnected,
    SendFailed
}

/// <summary>
/// Tracks heartbeat timestamps for active TCP peer connections and raises events when peers time out.
/// </summary>
public class HeartbeatMonitor
{
    private readonly ConcurrentDictionary<string, DateTime> _lastHeartbeat = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    public event Action<string>? OnConnectionFailed;
    public event Action<string>? OnHeartbeatReceived;

    /// <summary>
    /// The interval at which heartbeats should be sent.
    /// Use this when implementing heartbeat sending in your main program.
    /// </summary>
    public TimeSpan HeartbeatInterval => _heartbeatInterval;

    /// <summary>
    /// Start the heartbeat monitoring loop.
    /// </summary>
    public void Start()
    {
        if(_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            return;

        _cancellationTokenSource = new CancellationTokenSource();
        _ = Task.Run(MonitorLoop);
    }

    /// <summary>
    /// Start monitoring a specific peer.
    /// Call this when a peer connects.
    /// </summary>
    public void StartMonitoring(string peerId)
    {
        _lastHeartbeat[peerId] = DateTime.Now;
    }

    /// <summary>
    /// Record that a heartbeat was received from a peer.
    /// Call this when you receive a heartbeat message.
    /// </summary>
    public void RecordHeartbeat(string peerId)
    {
        _lastHeartbeat[peerId] = DateTime.Now;
        OnHeartbeatReceived?.Invoke(peerId);
    }

    /// <summary>
    /// Stop monitoring a peer.
    /// Call this when a peer disconnects normally.
    /// </summary>
    public void StopMonitoring(string peerId)
    {
        _lastHeartbeat.TryRemove(peerId, out _);
    }

    /// <summary>
    /// Main monitoring loop - checks for timed out connections.
    /// </summary>
    private async Task MonitorLoop()
    {
        if (_cancellationTokenSource == null)
            return;

        CancellationToken cancellationToken = _cancellationTokenSource.Token;

        while (!cancellationToken.IsCancellationRequested)
        {
            DateTime now = DateTime.Now;

            foreach (var entry in _lastHeartbeat)
            {
                TimeSpan elapsed = now - entry.Value;

                if (elapsed > _timeout)
                {
                    // TcpPeerHandler prints clean timeout message
                    StopMonitoring(entry.Key);
                    OnConnectionFailed?.Invoke(entry.Key);
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (TaskCanceledException) { break; }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Check if a peer is still alive (received heartbeat recently).
    /// </summary>
    public bool IsAlive(string peerId)
    {
        if (_lastHeartbeat.TryGetValue(peerId, out DateTime lastSeen))
            return DateTime.Now - lastSeen < _timeout;

        return false;
    }

    /// <summary>
    /// Stop monitoring all peers.
    /// </summary>
    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
    }
}
