// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger

using System.Text.Json;
using SecureMessenger.Core;

namespace SecureMessenger.UI;

/// <summary>
/// Sprint 3: Message history storage and retrieval.
/// Saves messages to a local file.
/// </summary>
public class MessageHistory
{
    private readonly string _historyFile;
    private readonly List<Message> _messages = new();
    private readonly object _lock = new();

    public MessageHistory(string historyFile = "message_history.json")
    {
        _historyFile = historyFile;
        Load();
    }

    /// <summary>
    /// Save a message to history
    /// </summary>
    public void SaveMessage(Message message)
    {
        // TODO: Add message to history and persist
        lock (_lock)
        {
            _messages.Add(message);
            PersistToFile();
        }
    }

    /// <summary>
    /// Load history from file on startup
    /// </summary>
    public void Load()
    {
        // TODO: Load messages from file
        try
        {
            if (File.Exists(_historyFile))
            {
                var json = File.ReadAllText(_historyFile);
                var messages = JsonSerializer.Deserialize<List<Message>>(json);
                if (messages != null)
                {
                    lock (_lock)
                    {
                        _messages.Clear();
                        _messages.AddRange(messages);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load history: {ex.Message}");
        }
    }

    private void PersistToFile()
    {
        // TODO: Write messages to file
        try
        {
            var json = JsonSerializer.Serialize(_messages, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_historyFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save history: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all messages in history
    /// </summary>
    public IEnumerable<Message> GetHistory(int? limit = null)
    {
        lock (_lock)
        {
            var messages = _messages.OrderByDescending(m => m.Timestamp);
            return limit.HasValue
                ? messages.Take(limit.Value).ToList()
                : messages.ToList();
        }
    }

    /// <summary>
    /// Display history to console
    /// </summary>
    public void ShowHistory(int limit = 50)
    {
        Console.WriteLine($"\n--- Message History (last {limit} messages) ---");
        foreach (var message in GetHistory(limit).Reverse())
        {
            Console.WriteLine(message);
        }
        Console.WriteLine("--- End of History ---\n");
    }

    /// <summary>
    /// Clear all history
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
            if (File.Exists(_historyFile))
            {
                File.Delete(_historyFile);
            }
        }
    }
}
