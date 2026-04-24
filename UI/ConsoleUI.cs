// Team 7: Rue Clow-McLaughlin, Devlin Gallagher, Nicholas Merante, Sophie Duquette
// CSCI 251 - Secure Distributed Messenger

using SecureMessenger.Core;

namespace SecureMessenger.UI;

/// <summary>
/// Parses console commands, displays messages, and shows command help for the text-based user interface.
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
        /help                - Show this help message
        /connect <ip> <port> - Connect to a peer
        /listen <port>       - Start listening for connections
        /peers               - List known peers
        /history             - View message history
        /history clear       - Clear saved message history
        /quit                - Exit the application
        /join #<room>        - Join a room
        /create #<room>      - Create a room with the specified room-id
        /leave #<room>       - Leaves the room with the specified room-id
        /rooms               - lists all rooms that are registers with the server
        /msg #<room> message - Send a message to the specified room
        Any text without /   - Send a chat message to all connected peers
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
        result.Args = tokens;
        switch(tokens[0].ToLower())
        {
            case "/connect":
                result.CommandType = CommandType.Connect;
                break;
            case "/help":
                result.CommandType = CommandType.Help;
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
            case "/rooms":
                result.CommandType  = CommandType.ListRooms;
                break;
            case "/create":
                result.CommandType = CommandType.CreateRoom;
                break;
            case "/leave":
                result.CommandType = CommandType.LeaveRoom;
                break;
            case "/join":
                result.CommandType = CommandType.JoinRoom;
                break;
            case "/msg":
                result.CommandType = CommandType.MessageRoom;
                break;
            case "/quit":
                result.CommandType = CommandType.Quit;
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
    Help,
    Connect,
    Listen,
    ListPeers,
    History,
    ListRooms,
    CreateRoom,
    LeaveRoom,
    JoinRoom,
    MessageRoom,
    Tamper,
    Quit,
    Exit,
}

/// <summary>
/// Stores the parsed result of a console input line, including command type, arguments, and message text.
/// </summary>
public class CommandResult
{
    /// <summary>True if the input was a command (started with /)</summary>
    public bool IsCommand { get; set; }

    /// <summary>The type of command parsed</summary>
    public CommandType CommandType { get; set; }

    /// <summary>Arguments for the command (e.g., IP and port for /connect)</summary>
    /// index 0 of Args is always the command string
    public string[]? Args { get; set; }

    /// <summary>The message content (for non-commands or error messages)</summary>
    public string? Message { get; set; }
}
