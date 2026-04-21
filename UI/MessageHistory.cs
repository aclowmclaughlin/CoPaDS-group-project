// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using System.Text.Json;
using Microsoft.VisualBasic;
using SecureMessenger.Core;

namespace SecureMessenger.UI;

/// <summary>
/// Sprint 3: Message history storage and retrieval.
/// Persists messages to a JSON file for retrieval across sessions.
///
/// Features:
/// - Thread-safe message storage
/// - JSON serialization/deserialization
/// - Automatic loading on startup
/// - Configurable history display limit
///
/// File Format: JSON array of Message objects
/// Default file: "message_history.json"
/// </summary>
public class MessageHistory
{
    private readonly string _historyFile;
    private readonly List<Message> _messages = new();
    private readonly object _lock = new();

    /// <summary>
    /// Create a MessageHistory with optional custom file path.
    /// Automatically loads existing history from file.
    ///
    /// </summary>
    public MessageHistory(string historyFile = "message_history.json")
    {
        _historyFile = historyFile;
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
            Console.WriteLine($"History won't load ;() : {ex.Message}");
        }
    }

    /// <summary>
    /// Write the current messages to the history file.
    /// </summary>
    private void PersistToFile()
    {
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
            Console.WriteLine($"History not saved ;() : {ex.Message}");
        }
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
    /// Display history to console.
    /// </summary>
    public void ShowHistory(int limit = 50)
    {
        Console.WriteLine($"--- Message History (last N messages) ---");
        foreach (var message in GetHistory(limit).Reverse())
        {
            Console.WriteLine(message.ToString);
        }
        Console.WriteLine($"--- End of History ---");
    }

    /// <summary>
    /// Clear all history from memory and disk.
    /// </summary>
    public void Clear()
    {
        lock(_lock)
        {
            _messages.Clear();
            if (File.Exists(_historyFile))
            {
                File.Delete(_historyFile);
            }
        }
    }
}