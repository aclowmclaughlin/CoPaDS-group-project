// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using SecureMessenger.Security;

namespace SecureMessenger.Core;

/// <summary>
/// Represents a connected peer in the network
/// </summary>
public class Peer
{
    public string Id { get; set; } = Guid.NewGuid().ToString()[..8];
    public string Name { get; set; } = string.Empty;
    public IPAddress? Address { get; set; }
    public int Port { get; set; }
    // public DateTime LastSeen { get; set; } = DateTime.Now;
    public bool IsConnected { get; set; }

    // Network connection
    public TcpClient? Client { get; set; }
    public NetworkStream? Stream { get; set; }
    public SemaphoreSlim SendSemaphore { get; } = new(1, 1);

    // Sprint 2: Per-session encryption keys
    public byte[]? AesKey { get; set; }
    public byte[]? PublicKey { get; set; }

    /// <summary>
    /// Encrypts the message with this peers key. DOES NOT sign the message, 
    /// use CreateSignedMessage() in the TcpPeerHandler to do that.
    /// 
    /// </summary>
    /// <param name="logicalMessage"></param>
    /// <returns></returns>
    public Message CreateEncryptedMessage(Message logicalMessage)
    {
        // Encrypt given message using peer's AES session key
        if (AesKey == null)
        {
            Console.WriteLine($"Error creating encrypted message for Peer: {this}. No Aes Key Stored.");
        }
        var encryptedBytes = new AesEncryption(AesKey!).Encrypt(logicalMessage.Content);

        return new Message
        {
            Type                = logicalMessage.Type,
            Sender              = logicalMessage.Sender,
            Room                = logicalMessage.Room,
            EncryptedContent    = encryptedBytes,
            Signature           = logicalMessage.Signature,
            Timestamp           = logicalMessage.Timestamp
        };
    }

    /// <summary>
    /// Attempts to verify and then decrypt the given message.
    /// If the message cannot be verified, returns false.
    /// </summary>
    /// <param name="message">The message to be decrypted</param>
    /// <param name="decryptedMessage">The decrypted message, 
    /// or null if the message could not be verified or decrypted.</param>
    /// <returns>If the message was successfully decrypted 
    /// (True if it was, False if not)</returns>
    public bool TryVerifyAndDecrypt(Message message, out Message? decryptedMessage)
    {
        decryptedMessage = null;
        
        if(message.EncryptedContent == null || message.Signature == null)
        {
            return false;
        }

        if(AesKey == null)
        {
            return false;
        }


        if (PublicKey == null)
        {
            return false;
        }

        try
        {
            bool valid = MessageSigner.VerifyData(message.EncryptedContent, message.Signature, PublicKey);
            if(!valid)
            {
                Console.WriteLine("Signature verification failed");
                return false;
            }

            // Decrypt 
            AesEncryption aes = new AesEncryption(AesKey);
            string plaintext = aes.Decrypt(message.EncryptedContent);
            decryptedMessage = new Message
            {
                Type            = message.Type,
                Sender          = message.Sender,
                Room            = message.Room,
                Content         = plaintext,
                Timestamp       = message.Timestamp
            };
            return true;
        }
        catch (CryptographicException exception)
        {
            Console.WriteLine($"Rejected tampered or invalid encrypted message from {this}: {exception.Message}");
            return false;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to process encrypted message from {this}: {exception.Message}");
            return false;
        }

    }
    
    public override string ToString()
    {
        var status = IsConnected ? "Connected" : "Disconnected";
        return $"{Name} ({Address}:{Port}) - {status}";
    }
}
