// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

namespace SecureMessenger.Core;

/// <summary>
/// Types of messages exchanged between peers.
/// </summary>
public enum MessageType
{
    // End-to-end peer messages
    Chat,
    DirectChat,
    RoomChat,

    // Room and connection-control messages
    CreateRoom,
    JoinRoom,
    LeaveRoom,
    RoomsListing,
    Heartbeat
}

/// <summary>
/// Represents a chat, room, heartbeat, or control message exchanged between peers.
/// Messages may contain plaintext content before sending or encrypted content and a signature while in transit.
/// </summary>
public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public MessageType Type { get; set; } = MessageType.Chat;

    public string Sender { get; set; }          = string.Empty;
    public string Room { get; set; }            = string.Empty;

    public string Content { get; set; } = string.Empty;
    public byte[]? EncryptedContent { get; set; }
    public byte[]? Signature { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public override string ToString()
    {
        return $"[{Timestamp:HH:mm:ss}] {Sender}: {Content}";
    }
}
