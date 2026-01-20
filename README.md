# Secure Distributed Messenger - Starter Code

CSCI 251: Concepts of Parallel and Distributed Systems

## Team Information

**Team Name:** Team 7

**Team Members:**
- Rue Clow-McLaughlin
- [Name 2]
- [Name 3]
- [Name 4]
- [Name 5]

## Build Instructions

### Prerequisites
- .NET 9.0 SDK or later

### Building the Project
```bash
dotnet build
```

Or for a release build:
```bash
dotnet build -c Release
```

## Run Instructions

### Starting the Application
```bash
dotnet run
```

Or run the compiled executable:
```bash
dotnet run --project SecureMessenger.csproj
```

## Usage

### Available Commands
- `/connect <ip> <port>` - Connect to a peer at the specified address
- `/listen <port>` - Start listening for incoming connections
- `/peers` - List all known peers
- `/history` - View message history
- `/quit` - Exit the application

### Example Session
```
Secure Distributed Messenger
============================
Type /help for available commands

/listen 5000
Listening on port 5000...

/connect 192.168.1.100 5000
Connected to 192.168.1.100:5000

Hello, world!
[10:30:45] You: Hello, world!
[10:30:47] Peer1: Hi there!

/quit
Goodbye!
```

## Project Structure

```
SecureMessenger/
+-- Program.cs                 # Entry point
+-- Core/
|   +-- Message.cs             # Message model
|   +-- MessageQueue.cs        # Thread-safe queue
|   +-- Peer.cs                # Peer information
+-- Network/
|   +-- TcpServer.cs           # Listens for connections
|   +-- TcpClientHandler.cs    # Handles outgoing connections
|   +-- PeerDiscovery.cs       # UDP broadcast discovery
|   +-- HeartbeatMonitor.cs    # Connection health monitoring
|   +-- ReconnectionPolicy.cs  # Automatic reconnection
+-- Security/
|   +-- AesEncryption.cs       # AES encrypt/decrypt
|   +-- RsaEncryption.cs       # RSA key management
|   +-- MessageSigner.cs       # Digital signatures
|   +-- KeyExchange.cs         # Key exchange protocol
+-- UI/
|   +-- ConsoleUI.cs           # User interface
|   +-- MessageHistory.cs      # Message persistence
```

## Sprint Implementation Notes

### Sprint 1: Threading & Basic Networking
- TODO: Complete the threading model in Program.cs
- TODO: Implement message sending/receiving in TcpServer and TcpClientHandler
- TODO: Wire up the MessageQueue for thread-safe communication

### Sprint 2: Security & Encryption
- TODO: Integrate AesEncryption for message content
- TODO: Implement key exchange protocol using KeyExchange
- TODO: Add message signing with MessageSigner

### Sprint 3: P2P & Advanced Features
- TODO: Enable PeerDiscovery for automatic peer finding
- TODO: Integrate HeartbeatMonitor for connection health
- TODO: Implement ReconnectionPolicy for resilience

## Known Issues

[Document any known issues here]

## Testing

[Document testing procedures here]
