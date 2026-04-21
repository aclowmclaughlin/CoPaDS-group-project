// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

namespace SecureMessenger.Core;

// Server messages are meant to be handled by the server
// client messages should just be forwared by the server.
public enum MessageType
{
    // End-to-end client messages
    Chat,
    RoomChat,

    // Server commands
    ListPeers,
    ListRooms,
    ListPeersInRoom,
    CreateRoom,
    JoinRoom,
    LeaveRoom,

    // Server replies
    ListPeersReply,
    ListRoomsReply,
    ListPeersInRoomReply,
    ServerNotice
}

/// <summary>
/// Represents a message in the system
/// </summary>
public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public MessageType Type { get; set; } = MessageType.Chat;

    public string Sender { get; set; }          = string.Empty; // no longer need field?
    public string TargetPeerID { get; set; }    = string.Empty; // no longer need field?
    public string Room { get; set; }            = string.Empty;

    public string Content { get; set; } = string.Empty;
    public byte[]? EncryptedContent { get; set; }
    public byte[]? Signature { get; set; }

    public byte[]? PublicKey { get; set; }
    public byte[]? EncryptedSessionKey { get; set; } // no longer need field?
    
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public override string ToString()
    {
        return $"[{Timestamp:HH:mm:ss}] {Sender}: {Content}";
    }
}
