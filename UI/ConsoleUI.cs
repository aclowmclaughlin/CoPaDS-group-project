// Team 7: Rue Clow-McLaughli, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using SecureMessenger.Core;

namespace SecureMessenger.UI;

/// <summary>
/// Console-based user interface.
/// Handles user input parsing and message display.
///
/// Supported Commands:
/// - /connect <ip> <port>  - Connect to a peer
/// - /listen <port>        - Start listening for connections
/// - /peers                - List known peers
/// - /history              - View message history
/// - /quit                 - Exit the application
/// - /exit                 - End current session
/// - Any other text        - Send as a message
/// </summary>
public class ConsoleUI
{
    /// <summary>
    /// Time Stamp Format Pattern string. used for DisplayMessage method.
    /// </summary>
    private const string _TIME_STAMP_FORMAT_PATTERN = "HH:mm:ss";

    /// <summary>
    /// 
    /// </summary>
    private const string _HELP_MESSAGE = 
    """
        Supported Commands:
        /connect <ip> <port> - Connect to a peer
        /list <port>         - Start listening for connections
        /peers               - List known peers
        /history             - View message history
        /quit                - Exit the application
        /exit                - End current session
    """;
    public ConsoleUI() {}

    /// <summary>
    /// Display a received message to the console.
    /// </summary>
    public void DisplayMessage(Message message)
    {
        Console.WriteLine($"[{message.Timestamp.ToString(_TIME_STAMP_FORMAT_PATTERN)}] {message.Sender}: {message.Content}");
    }

    /// <summary>
    /// Display a system message to the console.
    ///
    /// TODO: Implement the following:
    /// 1. Print in format: "[System] message"
    /// </summary>
    public void DisplaySystem(string message)
    {
        Console.WriteLine($"[System] {message}");
    }

    /// <summary>
    /// Show available commands to the user.
    /// </summary>
    public void ShowHelp()
    {
        Console.WriteLine(_HELP_MESSAGE);
    }

    /// <summary>
    /// Parse user input and return a CommandResult.
    /// </summary>
    public CommandResult ParseCommand(string input)
    {
        CommandResult result = new CommandResult();
        if (input.Length == 0 || input[0] != '/')
        {
            result.IsCommand = false;
            result.Message = input;
            return result;
        }

        result.IsCommand = true;
        var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var args = tokens[1..];
        if (args.Length > 0)
        {
            result.Args = args;
        } else
        {
            result.Args = null;
        }
        switch(tokens[0].ToLower())
        {
            case "/connect":
                result.CommandType = CommandType.Connect;
                break;
            case "/listen":
                result.CommandType = CommandType.Listen;
                break;
            case "/peers":
                result.CommandType = CommandType.ListPeers;
                break;
            case "/history":
                result.CommandType = CommandType.History;
                break;
            case "/quit":
                result.CommandType = CommandType.Quit;
                break;
            case "/exit":
                result.CommandType = CommandType.Exit;
                break;
            default:
                result.CommandType = CommandType.Unknown;
                result.Message = $"Command {tokens[0]} not valid. Use /help to list valid commands.";
                break;
        }
        return result;
    }
}

/// <summary>
/// Types of commands the user can enter
/// </summary>
public enum CommandType
{
    Unknown,
    Connect,
    Listen,
    ListPeers,
    History,
    Quit,
    Exit
}

/// <summary>
/// Result of parsing a user input line
/// </summary>
public class CommandResult
{
    /// <summary>True if the input was a command (started with /)</summary>
    public bool IsCommand { get; set; }

    /// <summary>The type of command parsed</summary>
    public CommandType CommandType { get; set; }

    /// <summary>Arguments for the command (e.g., IP and port for /connect)</summary>
    public string[]? Args { get; set; }

    /// <summary>The message content (for non-commands or error messages)</summary>
    public string? Message { get; set; }
}
