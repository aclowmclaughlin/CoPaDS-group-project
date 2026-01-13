// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger

using SecureMessenger.Core;

namespace SecureMessenger.UI;

/// <summary>
/// Console-based user interface.
/// Handles user input and message display.
/// </summary>
public class ConsoleUI
{
    private readonly MessageQueue _messageQueue;

    public ConsoleUI(MessageQueue messageQueue)
    {
        _messageQueue = messageQueue;
    }

    /// <summary>
    /// Display a received message
    /// </summary>
    public void DisplayMessage(Message message)
    {
        // TODO: Display message with timestamp and sender
        var timestamp = message.Timestamp.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] {message.Sender}: {message.Content}");
    }

    /// <summary>
    /// Display system message
    /// </summary>
    public void DisplaySystem(string message)
    {
        Console.WriteLine($"[System] {message}");
    }

    /// <summary>
    /// Show available commands
    /// </summary>
    public void ShowHelp()
    {
        Console.WriteLine("\nAvailable Commands:");
        Console.WriteLine("  /connect <ip> <port>  - Connect to a peer");
        Console.WriteLine("  /listen <port>        - Start listening for connections");
        Console.WriteLine("  /peers                - List known peers");
        Console.WriteLine("  /history              - View message history");
        Console.WriteLine("  /quit                 - Exit the application");
        Console.WriteLine();
    }

    /// <summary>
    /// Parse and execute a command
    /// </summary>
    public CommandResult ParseCommand(string input)
    {
        // TODO: Parse user commands
        if (!input.StartsWith("/"))
        {
            return new CommandResult { IsCommand = false, Message = input };
        }

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLower();

        return command switch
        {
            "/connect" when parts.Length >= 3 => new CommandResult
            {
                IsCommand = true,
                CommandType = CommandType.Connect,
                Args = parts[1..].ToArray()
            },
            "/listen" when parts.Length >= 2 => new CommandResult
            {
                IsCommand = true,
                CommandType = CommandType.Listen,
                Args = parts[1..].ToArray()
            },
            "/peers" => new CommandResult
            {
                IsCommand = true,
                CommandType = CommandType.ListPeers
            },
            "/history" => new CommandResult
            {
                IsCommand = true,
                CommandType = CommandType.History
            },
            "/quit" or "/exit" => new CommandResult
            {
                IsCommand = true,
                CommandType = CommandType.Quit
            },
            _ => new CommandResult
            {
                IsCommand = true,
                CommandType = CommandType.Unknown,
                Message = $"Unknown command: {command}"
            }
        };
    }
}

public enum CommandType
{
    Unknown,
    Connect,
    Listen,
    ListPeers,
    History,
    Quit
}

public class CommandResult
{
    public bool IsCommand { get; set; }
    public CommandType CommandType { get; set; }
    public string[]? Args { get; set; }
    public string? Message { get; set; }
}
