// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using System.Security.Cryptography;

namespace SecureMessenger.Security;

/// <summary>
/// Sprint 2: RSA encryption for key exchange.
/// Used to securely exchange AES session keys between peers.
///
/// RSA Configuration:
/// - Key size: 2048 bits
/// - Padding: OAEP with SHA-256 (RSAEncryptionPadding.OaepSHA256)
///
/// Usage:
/// 1. Each peer generates their own RSA key pair
/// 2. Peers exchange public keys
/// 3. One peer generates an AES session key
/// 4. That peer encrypts the AES key with the other's public key
/// 5. The encrypted key is sent and decrypted with the private key
/// 6. Both peers now have the same AES session key
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
