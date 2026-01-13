// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger

using System.Collections.Concurrent;

namespace SecureMessenger.Core;

/// <summary>
/// Thread-safe message queue for incoming/outgoing messages.
/// Uses BlockingCollection for thread-safe producer/consumer pattern.
/// </summary>
public class MessageQueue
{
    // TODO: Implement thread-safe queue using BlockingCollection or manual synchronization

    private readonly BlockingCollection<Message> _incomingMessages = new();
    private readonly BlockingCollection<Message> _outgoingMessages = new();
    private readonly object _lock = new();

    /// <summary>
    /// Enqueue an incoming message (received from network)
    /// </summary>
    public void EnqueueIncoming(Message message)
    {
        // TODO: Add message to incoming queue
        lock (_lock)
        {
            _incomingMessages.Add(message);
        }
    }

    /// <summary>
    /// Dequeue an incoming message for processing (blocks if empty)
    /// </summary>
    public Message DequeueIncoming(CancellationToken cancellationToken = default)
    {
        // TODO: Take message from incoming queue (should block if empty)
        return _incomingMessages.Take(cancellationToken);
    }

    /// <summary>
    /// Try to dequeue an incoming message without blocking
    /// </summary>
    public bool TryDequeueIncoming(out Message? message)
    {
        return _incomingMessages.TryTake(out message);
    }

    /// <summary>
    /// Enqueue an outgoing message (to be sent to network)
    /// </summary>
    public void EnqueueOutgoing(Message message)
    {
        // TODO: Add message to outgoing queue
        _outgoingMessages.Add(message);
    }

    /// <summary>
    /// Dequeue an outgoing message for sending (blocks if empty)
    /// </summary>
    public Message DequeueOutgoing(CancellationToken cancellationToken = default)
    {
        // TODO: Take message from outgoing queue
        return _outgoingMessages.Take(cancellationToken);
    }

    public int IncomingCount => _incomingMessages.Count;
    public int OutgoingCount => _outgoingMessages.Count;

    /// <summary>
    /// Signal that no more messages will be added
    /// </summary>
    public void CompleteAdding()
    {
        _incomingMessages.CompleteAdding();
        _outgoingMessages.CompleteAdding();
    }
}
