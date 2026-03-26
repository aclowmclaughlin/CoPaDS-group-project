// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

namespace SecureMessenger.Core;

public enum MessageType
{
    Chat,
    PublicKey,
    SessionKey,
    RoomCommand,
    RoomChat,
}

/// <summary>
/// Represents a message in the system
/// </summary>
public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public MessageType Type { get; set; } = MessageType.Chat;

    public string Sender { get; set; }          = string.Empty;
    public string TargetPeerID { get; set; }    = string.Empty;
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
