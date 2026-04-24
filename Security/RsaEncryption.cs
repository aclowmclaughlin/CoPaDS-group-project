// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using System.Security.Cryptography;

namespace SecureMessenger.Security;

/// <summary>
/// Manages RSA key generation, public key export/import, and AES session key encryption/decryption.
/// </summary>
public class RsaEncryption
{
    private readonly RSA _rsa;

    public RSA Rsa => _rsa; // Expose _rsa for MessageSigner use

    /// <summary>
    /// Create a new RSA key pair.
    /// </summary>
    public RsaEncryption()
    {
        _rsa = RSA.Create(2048);
    }

    /// <summary>
    /// Export our public key to send to a peer.
    /// </summary>
    public byte[] ExportPublicKey()
    {
        return _rsa.ExportRSAPublicKey();
    }

    /// <summary>
    /// Import a peer's public key.
    /// </summary>
    public void ImportPublicKey(byte[] publicKey)
    {
        _rsa.ImportRSAPublicKey(publicKey, out _);
    }

    /// <summary>
    /// Encrypt an AES session key with a peer's public key.
    /// </summary>
    public byte[] EncryptSessionKey(byte[] aesKey, byte[] peerPublicKey)
    {
        RSA peerRSA = RSA.Create();
        peerRSA.ImportRSAPublicKey(peerPublicKey, out _);
        return peerRSA.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);
    }

    /// <summary>
    /// Decrypt an AES session key with our private key.
    /// </summary>
    public byte[] DecryptSessionKey(byte[] encryptedKey)
    {
        return _rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
    }

    /// <summary>
    /// Dispose of RSA resources
    /// </summary>
    public void Dispose()
    {
        _rsa?.Dispose();
    }
}
