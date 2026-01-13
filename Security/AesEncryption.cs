// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger

using System.Security.Cryptography;

namespace SecureMessenger.Security;

/// <summary>
/// Sprint 2: AES encryption for message content.
/// Uses AES-256-CBC with random IV for each message.
/// </summary>
public class AesEncryption
{
    private readonly byte[] _key;

    /// <summary>
    /// Create with existing key
    /// </summary>
    public AesEncryption(byte[] key)
    {
        _key = key;
    }

    /// <summary>
    /// Generate a new random AES key
    /// </summary>
    public static byte[] GenerateKey()
    {
        // TODO: Generate a random 256-bit key
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.GenerateKey();
        return aes.Key;
    }

    /// <summary>
    /// Encrypt plaintext message
    /// </summary>
    public byte[] Encrypt(string plaintext)
    {
        // TODO: Implement AES encryption
        // - Generate random IV
        // - Encrypt with CBC mode
        // - Prepend IV to ciphertext

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Prepend IV to ciphertext
        var result = new byte[aes.IV.Length + ciphertext.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertext, 0, result, aes.IV.Length, ciphertext.Length);

        return result;
    }

    /// <summary>
    /// Decrypt ciphertext back to plaintext
    /// </summary>
    public string Decrypt(byte[] ciphertext)
    {
        // TODO: Implement AES decryption
        // - Extract IV from beginning
        // - Decrypt with CBC mode

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;

        // Extract IV (first 16 bytes)
        var iv = new byte[16];
        Buffer.BlockCopy(ciphertext, 0, iv, 0, 16);
        aes.IV = iv;

        // Extract actual ciphertext
        var actualCiphertext = new byte[ciphertext.Length - 16];
        Buffer.BlockCopy(ciphertext, 16, actualCiphertext, 0, actualCiphertext.Length);

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(actualCiphertext, 0, actualCiphertext.Length);

        return System.Text.Encoding.UTF8.GetString(plaintextBytes);
    }
}
