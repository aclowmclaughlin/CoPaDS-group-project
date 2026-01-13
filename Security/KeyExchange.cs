// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger

using System.Security.Cryptography;
using SecureMessenger.Core;

namespace SecureMessenger.Security;

/// <summary>
/// Sprint 2: Key exchange protocol handler.
/// Manages the handshake process between peers.
/// </summary>
public enum ConnectionState
{
    Disconnected,
    SendingPublicKey,
    ReceivingPublicKey,
    SendingSessionKey,
    ReceivingSessionKey,
    Established
}

public class KeyExchange
{
    private readonly RsaEncryption _rsa;
    private byte[]? _peerPublicKey;
    private byte[]? _sessionKey;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public byte[]? SessionKey => _sessionKey;

    public KeyExchange()
    {
        _rsa = new RsaEncryption();
    }

    /// <summary>
    /// Get our public key to send to peer
    /// </summary>
    public byte[] GetPublicKey()
    {
        State = ConnectionState.SendingPublicKey;
        return _rsa.ExportPublicKey();
    }

    /// <summary>
    /// Receive peer's public key
    /// </summary>
    public void ReceivePublicKey(byte[] peerPublicKey)
    {
        _peerPublicKey = peerPublicKey;
        State = ConnectionState.ReceivingPublicKey;
    }

    /// <summary>
    /// Generate and encrypt session key (client side)
    /// </summary>
    public byte[] CreateEncryptedSessionKey()
    {
        // Generate new AES session key
        _sessionKey = AesEncryption.GenerateKey();

        // Encrypt with peer's public key
        State = ConnectionState.SendingSessionKey;
        return _rsa.EncryptSessionKey(_sessionKey, _peerPublicKey!);
    }

    /// <summary>
    /// Decrypt received session key (server side)
    /// </summary>
    public void ReceiveEncryptedSessionKey(byte[] encryptedKey)
    {
        _sessionKey = _rsa.DecryptSessionKey(encryptedKey);
        State = ConnectionState.Established;
    }

    /// <summary>
    /// Complete the handshake
    /// </summary>
    public void Complete()
    {
        State = ConnectionState.Established;
    }

    /// <summary>
    /// Check if key exchange is complete
    /// </summary>
    public bool IsEstablished => State == ConnectionState.Established && _sessionKey != null;
}
