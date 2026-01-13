// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger

using System.Security.Cryptography;

namespace SecureMessenger.Security;

/// <summary>
/// Sprint 2: RSA encryption for key exchange.
/// Used to securely exchange AES session keys.
/// </summary>
public class RsaEncryption
{
    private readonly RSA _rsa;

    public RsaEncryption()
    {
        // TODO: Generate RSA key pair (2048 bits)
        _rsa = RSA.Create(2048);
    }

    /// <summary>
    /// Export our public key to send to peer
    /// </summary>
    public byte[] ExportPublicKey()
    {
        // TODO: Export public key for sending to peer
        return _rsa.ExportRSAPublicKey();
    }

    /// <summary>
    /// Import peer's public key
    /// </summary>
    public void ImportPublicKey(byte[] publicKey)
    {
        // TODO: Import peer's public key
        _rsa.ImportRSAPublicKey(publicKey, out _);
    }

    /// <summary>
    /// Encrypt AES session key with peer's public key
    /// </summary>
    public byte[] EncryptSessionKey(byte[] aesKey, byte[] peerPublicKey)
    {
        // TODO: Encrypt AES key with RSA using OAEP padding
        using var peerRsa = RSA.Create();
        peerRsa.ImportRSAPublicKey(peerPublicKey, out _);

        return peerRsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);
    }

    /// <summary>
    /// Decrypt AES session key with our private key
    /// </summary>
    public byte[] DecryptSessionKey(byte[] encryptedKey)
    {
        // TODO: Decrypt AES key with our RSA private key
        return _rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
    }

    public void Dispose()
    {
        _rsa.Dispose();
    }
}
