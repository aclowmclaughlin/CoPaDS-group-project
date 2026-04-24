// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using System.Collections.Concurrent;
using SecureMessenger.Core;

namespace SecureMessenger.Network;

/// <summary>
/// Attempts to reconnect to disconnected peers using a limited exponential backoff strategy.
/// </summary>
public class ReconnectionPolicy
{
    private readonly ConcurrentDictionary<string, int> _attemptCount = new();
    private readonly TcpPeerHandler _clientHandler;

    private const int MaxAttempts = 5;
    private const int InitialDelayMs = 1000;
    private const int MaxDelayMs = 30000;

    public event Action<string, int>? OnReconnectAttempt;
    public event Action<string>? OnReconnectSuccess;
    public event Action<string>? OnReconnectFailed;

    public ReconnectionPolicy(TcpPeerHandler clientHandler)
    {
        _clientHandler = clientHandler;
    }

    /// <summary>
    /// Attempt to reconnect to a peer with exponential backoff.
    /// </summary>
    public async Task<bool> TryReconnect(Peer peer)
    {
        var peerId = peer.Id;
        _attemptCount.TryGetValue(peerId, out int attempt);
        while (attempt < MaxAttempts)
        {
            attempt++;
            _attemptCount[peerId] = attempt;
            Console.WriteLine($"{attempt} reconnection attempt to {peerId}, {MaxAttempts - attempt} attempts left.");
            OnReconnectAttempt?.Invoke(peerId, attempt);

            var delay = Math.Min(InitialDelayMs * Math.Pow(2, attempt - 1), MaxDelayMs);
            try
            {
                var connect = await _clientHandler.ConnectAsync(peer.Address!.ToString(), peer.Port);
                if (connect)
                {
                    Console.WriteLine($"Connection to {peer.Id} successful");
                    ResetAttempts(peerId);
                    OnReconnectSuccess?.Invoke(peerId);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex} \nRetry in {delay}ms");
                await Task.Delay((int) delay);
            }

        }
        Console.WriteLine($"Reconnection to {peerId} reached max attempts");
        OnReconnectFailed?.Invoke(peerId);
        return false;
    }

    /// <summary>
    /// Reset attempt count for a peer.
    /// Call this after a successful connection.
    /// </summary>
    public void ResetAttempts(string peerId)
    {
        _attemptCount.TryRemove(peerId, out _);
    }

    /// <summary>
    /// Get current attempt count for a peer.
    /// </summary>
    public int GetAttemptCount(string peerId)
    {
        return _attemptCount.TryGetValue(peerId, out int attemptCount) ? attemptCount : 0;
    }
}