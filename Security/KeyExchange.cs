// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using System.Security.Cryptography;
using SecureMessenger.Core;

namespace SecureMessenger.Security;

/// <summary>
/// Sprint 2: Key exchange protocol handler.
/// Manages the handshake process between peers to establish a shared session key.
///
/// Key Exchange Protocol:
/// 1. Both peers generate RSA key pairs
/// 2. Peers exchange public keys
/// 3. One peer (initiator) generates an AES session key
/// 4. Initiator encrypts session key with responder's public key
/// 5. Responder decrypts session key with their private key
/// 6. Both peers now share the same AES session key for encryption
///
/// State Machine:
/// Disconnected -> SendingPublicKey -> ReceivingPublicKey ->
/// SendingSessionKey/ReceivingSessionKey -> Established
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

    /// <summary>
    /// Initialize the key exchange by creating our RSA key pair.
    /// </summary>
    public KeyExchange()
    {
        _rsa = new RsaEncryption();
        //make the encryption!!
    }

    /// <summary>
    /// Get our public key to send to the peer.
    /// </summary>
    public byte[] GetPublicKey()
    {
        State = ConnectionState.SendingPublicKey;
        return _rsa.ExportPublicKey();
    }

    /// <summary>
    /// Store the peer's public key when received.
    /// </summary>
    public void ReceivePublicKey(byte[] peerPublicKey)
    {
        _peerPublicKey = peerPublicKey;
        State = ConnectionState.ReceivingPublicKey;
    }

    /// <summary>
    /// Generate a new AES session key and encrypt it for the peer (initiator side).
    /// </summary>
    public byte[] CreateEncryptedSessionKey()
    {
        _sessionKey = AesEncryption.GenerateKey();
        //store a new key ;)

        State = ConnectionState.SendingSessionKey;
        return _rsa!.EncryptSessionKey(_sessionKey, _peerPublicKey!);
        //The exclamation point says "I promise it won't be null"

    }

    /// <summary>
    /// Decrypt the received session key (responder side).
    /// </summary>
    public void ReceiveEncryptedSessionKey(byte[] encryptedKey)
    {
        _sessionKey =  _rsa.DecryptSessionKey(encryptedKey);
        State = ConnectionState.Established;
    }

    /// <summary>
    /// Mark the key exchange as complete (initiator side, after sending session key).
    /// </summary>
    public void Complete()
    {
        State = ConnectionState.Established;
    }

    /// <summary>
    /// Check if key exchange is complete and we have a valid session key.
    /// </summary>
    public bool IsEstablished => State == ConnectionState.Established && _sessionKey != null;
}
