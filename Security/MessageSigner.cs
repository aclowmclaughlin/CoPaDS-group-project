// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using System.Security.Cryptography;

namespace SecureMessenger.Security;

/// <summary>
/// Sprint 2: Message signing and verification.
/// Uses RSA with SHA-256 for digital signatures.
///
/// Digital Signature Configuration:
/// - Algorithm: RSA with SHA-256
/// - Padding: PKCS#1 v1.5 (RSASignaturePadding.Pkcs1)
///
/// Purpose:
/// - Signing proves the message came from the claimed sender
/// - Verification detects if the message was tampered with
/// - Reject any message with an invalid signature
/// </summary>
public class MessageSigner
{
    private readonly RSA _rsa;

    /// <summary>
    /// Create a MessageSigner with an RSA key pair.
    /// Use your own RSA instance for signing outgoing messages.
    /// </summary>
    public MessageSigner(RSA rsa)
    {
        _rsa = rsa;
    }

    /// <summary>
    /// Sign data with our private key.
    /// </summary>
    public byte[] SignData(byte[] data)
    {
        // return _rsa.SignData() with:
        //    - The data bytes to sign
        //    - HashAlgorithmName.SHA256
        //    - RSASignaturePadding.Pkcs1
        return _rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    /// <summary>
    /// Verify a message signature with the sender's public key.
    /// </summary>
    public static bool VerifyData(byte[] data, byte[] signature, byte[] publicKey)
    {
        try
        {
            // Create a new RSA instance for the sender's public key
            using var peerRsa = RSA.Create();
            
            // Import the sender's public key
            peerRsa.ImportRSAPublicKey(publicKey, out _);

            // Use VerifyData() with:
            //    - The original data bytes
            //    - The signature bytes
            //    - HashAlgorithmName.SHA256
            //    - RSASignaturePadding.Pkcs1
            bool isValid = peerRsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        
            if(!isValid)
            {
                Console.WriteLine("[WARNING] Invalid signature detected; message may be tampered with");
                throw new CryptographicException("Signature verification failed");
            }

            // Signature deemed valid, return true
            return isValid;
        }
        catch(CryptographicException) // Reject messages with invalid signatures
        {
            Console.WriteLine("[ERROR] Failed to verify message signature; rejecting message");
            return false;
        }
    }
}
