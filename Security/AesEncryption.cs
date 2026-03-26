// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using System.Data.SqlTypes;
using System.Security.Cryptography;
using System.Text;

namespace SecureMessenger.Security;

/// <summary>
/// Sprint 2: AES encryption for message content.
/// Uses AES-256-CBC with random IV for each message.
///
/// AES-256 Configuration:
/// - Key size: 256 bits (32 bytes)
/// - Block size: 128 bits (16 bytes)
/// - Mode: CBC (Cipher Block Chaining)
/// - IV: Random 16 bytes, prepended to ciphertext
///
/// Wire format: [IV (16 bytes)][Ciphertext (variable length)]
/// </summary>
public class AesEncryption
{
    private readonly byte[] _key;
    private static readonly int IV_LENGTH = 16;

    /// <summary>
    /// Create with existing key (32 bytes for AES-256)
    /// </summary>
    public AesEncryption(byte[] key)
    {
        if (key.Length != 32)
            throw new ArgumentException("AES-256 requires a 32-byte key", nameof(key));
        _key = key;
    }

    /// <summary>
    /// Generate a new random AES-256 key.
    /// </summary>
    public static byte[] GenerateKey()
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.GenerateKey();
        return aes.Key;
    }

    /// <summary>
    /// Encrypt plaintext message using AES-256-CBC.
    /// Returns the encrypted plaintext with the IV prepended to it.
    /// </summary>
    public byte[] Encrypt(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.GenerateIV();
        using var encrypter = aes.CreateEncryptor();
        byte[] plaintext_bytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] ciphertext = encrypter.TransformFinalBlock(plaintext_bytes, 0, plaintext_bytes.Length);

        byte[] result = new byte[aes.IV.Length + ciphertext.Length];
        
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertext, 0, result, aes.IV.Length, ciphertext.Length);
        return result;
    }

    /// <summary>
    /// Decrypt ciphertext back to plaintext.
    /// </summary>
    public string Decrypt(byte[] ciphertext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        // extract IV from ciphertext
        byte[] extracted_iv = new byte[IV_LENGTH];
        Buffer.BlockCopy(ciphertext, 0, extracted_iv, 0, IV_LENGTH);
        aes.IV = extracted_iv;
        // extract ciphertext from new ciphertext
        byte[] extracted_ciphertext = new byte[ciphertext.Length - IV_LENGTH];
        Buffer.BlockCopy(extracted_ciphertext, IV_LENGTH, ciphertext, 0, ciphertext.Length);
        // create decryptor
        var decryptor = aes.CreateDecryptor();
        // decrypt
        byte[] plaintext_bytes = decryptor.TransformFinalBlock(extracted_ciphertext, 0, extracted_ciphertext.Length);
        return Encoding.UTF8.GetString(plaintext_bytes);
    }
}
