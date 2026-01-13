// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger

using System.Collections.Concurrent;
using SecureMessenger.Core;

namespace SecureMessenger.Network;

/// <summary>
/// Sprint 3: Automatic reconnection with exponential backoff.
/// </summary>
public class ReconnectionPolicy
{
    private readonly ConcurrentDictionary<string, int> _attemptCount = new();
    private readonly TcpClientHandler _clientHandler;

    private const int MaxAttempts = 5;
    private const int InitialDelayMs = 1000;
    private const int MaxDelayMs = 30000;

    public event Action<string, int>? OnReconnectAttempt;
    public event Action<string>? OnReconnectSuccess;
    public event Action<string>? OnReconnectFailed;

    public ReconnectionPolicy(TcpClientHandler clientHandler)
    {
        _clientHandler = clientHandler;
    }

    /// <summary>
    /// Attempt to reconnect to a peer with exponential backoff
    /// </summary>
    public async Task<bool> TryReconnect(Peer peer)
    {
        // TODO: Implement reconnection with retry logic
        var peerId = peer.Id;
        _attemptCount.TryGetValue(peerId, out int attempt);

        while (attempt < MaxAttempts)
        {
            attempt++;
            _attemptCount[peerId] = attempt;

            Console.WriteLine($"[Reconnect] Attempting to reconnect to {peer.Name} (attempt {attempt}/{MaxAttempts})");
            OnReconnectAttempt?.Invoke(peerId, attempt);

            // Calculate delay with exponential backoff
            var delay = Math.Min(InitialDelayMs * (int)Math.Pow(2, attempt - 1), MaxDelayMs);

            try
            {
                var success = await _clientHandler.ConnectAsync(peer.Address!.ToString(), peer.Port);
                if (success)
                {
                    Console.WriteLine($"[Reconnect] Success! Reconnected to {peer.Name}");
                    ResetAttempts(peerId);
                    OnReconnectSuccess?.Invoke(peerId);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Reconnect] Failed: {ex.Message}, retrying in {delay}ms...");
            }

            await Task.Delay(delay);
        }

        Console.WriteLine($"[Reconnect] Giving up on {peer.Name} after {MaxAttempts} attempts");
        OnReconnectFailed?.Invoke(peerId);
        return false;
    }

    /// <summary>
    /// Reset attempt count for a peer (call after successful connection)
    /// </summary>
    public void ResetAttempts(string peerId)
    {
        _attemptCount.TryRemove(peerId, out _);
    }

    /// <summary>
    /// Get current attempt count for a peer
    /// </summary>
    public int GetAttemptCount(string peerId)
    {
        _attemptCount.TryGetValue(peerId, out int count);
        return count;
    }
}
