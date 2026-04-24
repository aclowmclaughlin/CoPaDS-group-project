// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using SecureMessenger.Core;
using System.Text.Json;
using SecureMessenger.Security;

namespace SecureMessenger.UI;

/// <summary>
/// Saves, loads, displays, clears, and encrypts local file-based message history for the peer.
/// </summary>
public class MessageHistory
{
    private readonly string _historyFile;
    private readonly List<Message> _messages = new();
    private readonly object _lock = new();
    private static readonly Mutex _fileMutex = new(false, "SecureMessengerMessageHistoryFileMutex");

    private readonly string _historyKeyFile; // Used for message history encryption

    /// <summary>
    /// Creates a message history store, initializes the encrypted history key path,
    /// and loads any existing saved history from disk.
    /// </summary>
    /// <param name="historyFile">The encrypted history file path.</param>
    public MessageHistory(string historyFile = "message_history.json")
    {
        _historyFile = historyFile;
        _historyKeyFile = Path.ChangeExtension(historyFile, ".key");
        Load();
    }

    /// <summary>
    /// Save a message to history and persist to file.
    /// </summary>
    public void SaveMessage(Message message)
    {
        lock (_lock)
        {
            _messages.Add(message);
            PersistToFile();
        }
    }

    /// <summary>
    /// Load history from file on startup.
    /// </summary>
    public void Load()
    {
        RunWithFileLock(() =>
        {
            try {
                if(File.Exists(_historyFile))
                {
                    byte[] encryptedJson = File.ReadAllBytes(_historyFile); // Decrypt file before reading it
                    string json = DecryptHistoryJson(encryptedJson);

                    var messages = JsonSerializer.Deserialize<List<Message>>(json);

                    if(messages != null)
                    {
                        lock(_lock)
                        {
                            _messages.Clear();
                            _messages.AddRange(messages);
                        }
                    }
                }
            }
            catch(Exception ex) {
                Console.WriteLine($"History won't load ;() : {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Writes the current in-memory message history to the history file.
    /// </summary>
    private void PersistToFile()
    {
        RunWithFileLock(() =>
        {
            try {
                var json = JsonSerializer.Serialize(_messages, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                byte[] encryptedJson = EncryptHistoryJson(json); // Encrypt before local file write
                File.WriteAllBytes(_historyFile, encryptedJson);
            }
            catch(Exception ex) {
                Console.WriteLine($"History not saved ;() : {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Get messages from history
    /// </summary>
    public IEnumerable<Message> GetHistory(int? limit = null)
    {
        lock (_lock)
        {
            var messages = _messages.OrderByDescending(m => m.Timestamp);
            return limit.HasValue
                ?messages.Take(limit.Value).ToList():messages.ToList();
                //if message is good we add the limit, if not we just shove the msg on list
        }
    }

    /// <summary>
    /// Runs a file operation while holding a cross-process lock for the history file.
    /// </summary>
    /// <param name="fileAction">The file operation to perform.</param>
    private static void RunWithFileLock(Action fileAction)
    {
        bool lockTaken = false;

        try {
            try {
                lockTaken = _fileMutex.WaitOne(TimeSpan.FromSeconds(5));
            }
            catch(AbandonedMutexException) {
                lockTaken = true;
            }

            if(!lockTaken) {
                Console.WriteLine("History file is busy; skipping this history update.");
                return;
            }

            fileAction();
        }
        finally {
            if(lockTaken)
                _fileMutex.ReleaseMutex();
        }
    }

    /// <summary>
    /// Display history to console.
    /// </summary>
    public void ShowHistory(int limit = 50)
    {
        Console.WriteLine($"--- Message History (last {limit} messages) ---");
        foreach (var message in GetHistory(limit).Reverse())
        {
            Console.WriteLine(message.ToString());
        }
        Console.WriteLine($"--- End of History ---");
    }

    /// <summary>
    /// Clear all history from memory and disk.
    /// </summary>
    public void Clear()
    {
        RunWithFileLock(() =>
        {
            lock(_lock)
            {
                _messages.Clear();
                if(File.Exists(_historyFile))
                    File.Delete(_historyFile);
            }
        });
    }

    //----------------------------------
    // History encryption helper methods
    //----------------------------------

    /// <summary>
    /// Loads the local AES history key if it exists, or creates and saves a new key file.
    /// </summary>
    /// <returns>The AES key used to encrypt and decrypt message history.</returns>
    private byte[] GetOrCreateHistoryKey()
    {
        if(File.Exists(_historyKeyFile))
            return Convert.FromBase64String(File.ReadAllText(_historyKeyFile));

        byte[] key = AesEncryption.GenerateKey();
        File.WriteAllText(_historyKeyFile, Convert.ToBase64String(key));
        return key;
    }

    /// <summary>
    /// Encrypts serialized message history JSON before it is written to disk.
    /// </summary>
    /// <param name="json">The plaintext serialized history JSON.</param>
    /// <returns>The encrypted history bytes.</returns>
    private byte[] EncryptHistoryJson(string json)
    {
        byte[] key = GetOrCreateHistoryKey();
        AesEncryption aesEncryption = new AesEncryption(key);
        return aesEncryption.Encrypt(json);
    }

    /// <summary>
    /// Decrypts encrypted message history bytes after they are read from disk.
    /// </summary>
    /// <param name="encryptedJson">The encrypted history bytes from the history file.</param>
    /// <returns>The plaintext serialized history JSON.</returns>
    private string DecryptHistoryJson(byte[] encryptedJson)
    {
        byte[] key = GetOrCreateHistoryKey();
        AesEncryption aesEncryption = new AesEncryption(key);
        return aesEncryption.Decrypt(encryptedJson);
    }
}