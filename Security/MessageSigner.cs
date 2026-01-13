// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger

using System.Security.Cryptography;

namespace SecureMessenger.Security;

/// <summary>
/// Sprint 2: Message signing and verification.
/// Uses RSA with SHA-256 for digital signatures.
/// </summary>
public class MessageSigner
{
    private readonly RSA _rsa;

    public MessageSigner(RSA rsa)
    {
        _rsa = rsa;
    }

    /// <summary>
    /// Sign a message with our private key
    /// </summary>
    public byte[] SignData(byte[] data)
    {
        // TODO: Sign data with RSA and SHA-256
        return _rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    /// <summary>
    /// Verify a message signature with peer's public key
    /// </summary>
    public bool VerifyData(byte[] data, byte[] signature, byte[] publicKey)
    {
        // TODO: Verify signature
        // Return false if signature is invalid (reject tampered messages)
        try
        {
            using var peerRsa = RSA.Create();
            peerRsa.ImportRSAPublicKey(publicKey, out _);

            bool isValid = peerRsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            if (!isValid)
            {
                Console.WriteLine("WARNING: Invalid signature detected - message may be tampered!");
                throw new CryptographicException("Signature verification failed");
            }

            return isValid;
        }
        catch (CryptographicException)
        {
            Console.WriteLine("ERROR: Failed to verify signature - rejecting message");
            return false;
        }
    }
}
