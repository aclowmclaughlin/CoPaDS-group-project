// Team 7: Rue Clow-McLaughli, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using System.Collections.Concurrent;

namespace SecureMessenger.Core;

/// <summary>
/// Thread-safe message queue for incoming/outgoing messages.
///
/// This class implements the Producer/Consumer pattern:
/// - Producers add messages to the queue (network threads, UI thread)
/// - Consumers take messages from the queue (processing threads, send threads)
///
/// Thread Safety Options:
/// 1. BlockingCollection<T> - recommended, handles blocking and thread safety
/// 2. ConcurrentQueue<T> with manual synchronization
/// 3. Queue<T> with explicit locking
///
/// The blocking behavior is important:
/// - Take() should block when the queue is empty
/// - This allows consumer threads to wait efficiently without busy-waiting
/// </summary>
public class MessageQueue
{
    /// <summary>
    /// Queue that holds & distributes incoming messages
    /// </summary>
    private readonly BlockingCollection<Message> _incoming_queue;
    
    /// <summary>
    /// Queue that holds & distributes outgoing messages
    /// </summary>
    private readonly BlockingCollection<Message> _outgoing_queue;

    public MessageQueue()
    {
        _incoming_queue = new BlockingCollection<Message>();
        _outgoing_queue = new BlockingCollection<Message>();
    }

    /// <summary>
    /// Enqueue an incoming message (received from network).
    /// </summary>
    public void EnqueueIncoming(Message message)
    {
        _incoming_queue.Add(message);
    }

    /// <summary>
    /// Dequeue an incoming message for processing.
    /// This method should BLOCK if the queue is empty.
    /// </summary>
    public Message DequeueIncoming(CancellationToken cancellationToken = default)
    {
        return _incoming_queue.Take(cancellationToken);
    }

    /// <summary>
    /// Try to dequeue an incoming message without blocking.
    /// </summary>
    public bool TryDequeueIncoming(out Message? message)
    {
        return _incoming_queue.TryTake(out message);
    }

    /// <summary>
    /// Enqueue an outgoing message (to be sent to network).
    /// </summary>
    public void EnqueueOutgoing(Message message)
    {
        _outgoing_queue.Add(message);
    }

    /// <summary>
    /// Dequeue an outgoing message for sending.
    /// This method should BLOCK if the queue is empty.
    /// </summary>
    public Message DequeueOutgoing(CancellationToken cancellationToken = default)
    {
        return _outgoing_queue.Take(cancellationToken);
    }

    /// <summary>
    /// Try to dequeue an outgoing message without blocking.
    /// </summary>
    public bool TryDequeueOutgoing(out Message? message)
    {
        return _outgoing_queue.TryTake(out message);
    }


    /// <summary>
    /// Get the count of incoming messages waiting to be processed.
    /// </summary>
    public int IncomingCount => _incoming_queue.Count;

    /// <summary>
    /// Get the count of outgoing messages waiting to be sent.
    ///
    /// TODO: Return the count of your outgoing queue
    /// </summary>
    public int OutgoingCount => _outgoing_queue.Count;

    /// <summary>
    /// Signal that no more messages will be added.
    /// Call this during shutdown to unblock waiting consumers.
    /// </summary>
    public void CompleteAdding()
    {
        _incoming_queue.CompleteAdding();
        _outgoing_queue.CompleteAdding();
    }
}
