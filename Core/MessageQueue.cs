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
    ///
    /// TODO: Implement the following:
    /// 1. Add the message to your outgoing queue
    /// 2. Ensure thread safety
    /// </summary>
    public void EnqueueOutgoing(Message message)
    {
        
    }

    /// <summary>
    /// Dequeue an outgoing message for sending.
    /// This method should BLOCK if the queue is empty.
    ///
    /// TODO: Implement the following:
    /// 1. Take a message from the outgoing queue
    /// 2. Block if the queue is empty
    /// 3. Support cancellation via the CancellationToken
    /// </summary>
    public Message DequeueOutgoing(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Implement DequeueOutgoing() - see TODO in comments above");
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
    public int OutgoingCount => throw new NotImplementedException("Implement OutgoingCount property");

    /// <summary>
    /// Signal that no more messages will be added.
    /// Call this during shutdown to unblock waiting consumers.
    ///
    /// TODO: Implement the following:
    /// 1. Mark both queues as complete (no more additions)
    ///
    /// Hint: BlockingCollection.CompleteAdding() does this
    /// </summary>
    public void CompleteAdding()
    {
        throw new NotImplementedException("Implement CompleteAdding() - see TODO in comments above");
    }
}
