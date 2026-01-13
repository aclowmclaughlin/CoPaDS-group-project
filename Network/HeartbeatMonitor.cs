// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger

using System.Collections.Concurrent;
using System.Diagnostics;
using SecureMessenger.Core;

namespace SecureMessenger.Network;

/// <summary>
/// Sprint 3: Heartbeat monitoring for connection health.
/// Detects failed connections and triggers reconnection.
/// </summary>
public class HeartbeatMonitor
{
    private readonly ConcurrentDictionary<string, DateTime> _lastHeartbeat = new();
    private readonly ConcurrentDictionary<string, Stopwatch> _timers = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    public event Action<string>? OnConnectionFailed;
    public event Action<string>? OnHeartbeatReceived;

    /// <summary>
    /// Start monitoring connections
    /// </summary>
    public void Start()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _ = Task.Run(MonitorLoop);
    }

    /// <summary>
    /// Start monitoring a specific peer
    /// </summary>
    public void StartMonitoring(string peerId)
    {
        _lastHeartbeat[peerId] = DateTime.Now;
        var timer = new Stopwatch();
        timer.Start();
        _timers[peerId] = timer;
    }

    /// <summary>
    /// Record heartbeat received from peer
    /// </summary>
    public void RecordHeartbeat(string peerId)
    {
        _lastHeartbeat[peerId] = DateTime.Now;
        if (_timers.TryGetValue(peerId, out var timer))
        {
            timer.Restart();
        }
        OnHeartbeatReceived?.Invoke(peerId);
    }

    /// <summary>
    /// Stop monitoring a peer
    /// </summary>
    public void StopMonitoring(string peerId)
    {
        _lastHeartbeat.TryRemove(peerId, out _);
        if (_timers.TryRemove(peerId, out var timer))
        {
            timer.Stop();
        }
    }

    private async Task MonitorLoop()
    {
        // TODO: Check for timeout and trigger reconnection
        while (!_cancellationTokenSource!.Token.IsCancellationRequested)
        {
            var now = DateTime.Now;

            foreach (var kvp in _lastHeartbeat)
            {
                var elapsed = now - kvp.Value;
                if (elapsed > _timeout)
                {
                    Console.WriteLine($"[Heartbeat] {kvp.Key} connection timeout");
                    OnConnectionFailed?.Invoke(kvp.Key);
                    StopMonitoring(kvp.Key);
                }
            }

            await Task.Delay(1000);
        }
    }

    /// <summary>
    /// Check if peer is alive (received heartbeat recently)
    /// </summary>
    public bool IsAlive(string peerId)
    {
        if (_lastHeartbeat.TryGetValue(peerId, out var lastSeen))
        {
            return DateTime.Now - lastSeen < _timeout;
        }
        return false;
    }

    /// <summary>
    /// Stop monitoring
    /// </summary>
    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
    }
}
