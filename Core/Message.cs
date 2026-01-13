// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger

namespace SecureMessenger.Core;

/// <summary>
/// Represents a message in the system
/// </summary>
public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // Sprint 2: Security fields
    public byte[]? Signature { get; set; }
    public byte[]? EncryptedContent { get; set; }

    public override string ToString()
    {
        return $"[{Timestamp:HH:mm:ss}] {Sender}: {Content}";
    }
}
