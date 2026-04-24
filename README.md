# Secure Distributed Messenger - Starter Code

CSCI 251: Concepts of Parallel and Distributed Systems

## Team Information

**Team Name:** Team 7

**Team Members:**
=======
- Rue Clow-McLaughlin
- Devlin Gallagher
- Nicholas Merante
- Sophie Duquette

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

---
## Run Instructions

### Starting the Application

```bash
dotnet run --project SecureMessenger.csproj
```

The app automatically starts listening on the first available TCP port beginning at `5000`. Port `5001` is reserved for UDP peer discovery and is skipped.

To request a specific TCP port:

```bash
dotnet run --project SecureMessenger.csproj -- 5004
```

When running multiple local instances, open multiple terminals and run the same command in each:

```bash
dotnet run --project SecureMessenger.csproj
```

Each instance should choose a different available TCP port automatically.

---

## Usage

### Available Commands

- `/help` - Display all available commands
- `/connect <ip> <port>` - Manually connect to a peer
- `/listen <port>` - Manually start listening if not already listening
- `/peers` - List connected peers
- `/history` - View message history
- `/history clear` - Clear local message history
- `/quit` - Exit the application
- `/create #<room>` - Create and join a room
- `/join #<room>` - Join a room
- `/leave #<room>` - Leave a room
- `/rooms` - List known rooms
- `/msg #<room> <message>` - Send a room message
- Any text without `/` - Send a chat message to all connected peers

---

### Example Local P2P Session

Open three terminals from the repository root.

Terminal 1:
```bash
dotnet run --project SecureMessenger.csproj
```

Terminal 2:
```bash
dotnet run --project SecureMessenger.csproj
```

Terminal 3:
```bash
dotnet run --project SecureMessenger.csproj
```

Each instance automatically starts listening on an available TCP port. Peers advertise themselves over UDP port 5001 and auto-connect when discovered.

Example commands:

```text
/peers
hello everyone
/history
/create #demo
```

On another peer:

```text
/join #demo
/msg #demo hello room
```

To clear local history:

```text
/history clear
```

To exit:

```text
/quit
```

---

## Project Structure

```text
SecureMessenger/
├── Program.cs                 # Entry point, command loop, background tasks, startup coordination
├── Core/
│   ├── Message.cs             # Message model
│   ├── MessageQueue.cs        # Thread-safe producer/consumer queue
│   └── Peer.cs                # Peer connection/session state
├── Network/
│   ├── TcpPeerHandler.cs      # Handles incoming/outgoing TCP peer connections
│   ├── PeerDiscovery.cs       # UDP broadcast discovery
│   ├── HeartbeatMonitor.cs    # Connection health monitoring
│   └── ReconnectionPolicy.cs  # Automatic reconnection
├── Security/
│   ├── AesEncryption.cs       # AES encrypt/decrypt
│   ├── RsaEncryption.cs       # RSA key management
│   ├── MessageSigner.cs       # Digital signatures
└── UI/
    ├── ConsoleUI.cs           # User interface
    └── MessageHistory.cs      # Message persistence
```

## What's Provided vs. What You Implement

### Provided (Do Not Modify)
- **Class structures**: All classes, fields, properties, and method signatures
- **Data models**: `Message.cs` and `Peer.cs` are complete
- **Events**: All event declarations for thread communication
- **Constants**: Configuration values (timeouts, intervals, key sizes)
- **Enums**: `CommandType`, `ConnectionState`, etc.

### You Must Implement
All methods marked with `throw new NotImplementedException()` - look for the detailed TODO comments in each method that explain exactly what to implement.

## Sprint Implementation Guide

### Sprint 1: Threading & Basic Networking (Week 5)

**Files to complete:**
- `Program.cs` - Main loop, thread creation, event handling
- `Core/MessageQueue.cs` - Thread-safe producer/consumer queue
- `Network/TcpServer.cs` - TCP listener, accept loop, receive threads
- `Network/TcpClientHandler.cs` - TCP client, connect, send/receive
- `UI/ConsoleUI.cs` - Command parsing and message display

**Key concepts:**
- Multi-threading with `Thread` and `Task`
- Thread synchronization with `lock` and `BlockingCollection`
- TCP sockets with `TcpListener` and `TcpClient`
- Event-driven programming with C# events

### Sprint 2: Security & Encryption (Week 10)

**Files to complete:**
- `Security/AesEncryption.cs` - AES-256-CBC encryption/decryption
- `Security/RsaEncryption.cs` - RSA-2048 key pair management
- `Security/MessageSigner.cs` - RSA-SHA256 digital signatures
- `Security/KeyExchange.cs` - Key exchange state machine

**Key concepts:**
- Symmetric encryption (AES)
- Asymmetric encryption (RSA)
- Digital signatures
- Key exchange protocols

### Sprint 3: P2P & Advanced Features (Week 14)

**Files to complete:**
- `Network/PeerDiscovery.cs` - UDP broadcast for peer discovery
- `Network/HeartbeatMonitor.cs` - Connection health monitoring
- `Network/ReconnectionPolicy.cs` - Exponential backoff reconnection
- `UI/MessageHistory.cs` - JSON-based message persistence

**Key concepts:**
- UDP broadcast
- Heartbeat/keepalive patterns
- Exponential backoff retry logic
- File I/O with JSON serialization

---

## Current Implementation Summary

The final application implements:

- Multi-threaded console input, incoming processing, outgoing processing, TCP receive loops, heartbeat loops, and UDP discovery loops
- Thread-safe message queues using `BlockingCollection`
- P2P TCP communication without a central server
- UDP peer discovery on port 5001
- Automatic peer connection with duplicate-connection prevention
- AES-encrypted message content
- RSA key exchange for AES session keys
- RSA message signing and verification
- Heartbeat-based failure detection
- Reconnection attempts with exponential backoff
- JSON-based local message history
- Room-based group messaging

---

## Technical Specifications

### Wire Protocol
- Messages sent as newline-terminated strings
- Sprint 2+: JSON-serialized Message objects with encrypted content
- Initial public key delivery uses: `[4 bytes key length][public key bytes]`
- Application messages use: `[message length as text][newline][JSON serialized Message object]`

### Encryption (Sprint 2)
- **AES-256-CBC**: 32-byte key, 16-byte IV prepended to ciphertext
- **RSA-2048**: OAEP-SHA256 padding for key exchange
- **Signatures**: RSA-SHA256 with PKCS#1 v1.5 padding

### Discovery Protocol (Sprint 3)
- UDP broadcast on port 5001
- Message format: `PEER:<peerId>:<tcpPort>`
- Broadcast interval: 5 seconds
- Peer timeout: 30 seconds

### Heartbeat (Sprint 3)
- Interval: 5 seconds
- Timeout: 15 seconds

### Reconnection (Sprint 3)
- Max attempts: 5
- Backoff: 1s → 2s → 4s → 8s → 16s (capped at 30s)

## Known Issues

## Known Issues

- Direct one-to-one peer messaging is not implemented; plain text broadcasts to all connected peers.
- Message relay through intermediate peers is not implemented.
- Message history is stored as local JSON and is not encrypted.
- Public keys are exchanged directly but are not independently verified by certificate or persistent trust store.
- Metadata such as sender and room name is not encrypted.
- UDP discovery may be blocked by firewall or network configuration; manual `/connect` is available as a fallback.

## Testing

The following behaviors were tested during Sprint 3:

- Three local peers can start in separate terminals and automatically choose available ports.
- Peers discover each other over UDP and auto-connect over TCP.
- `/peers` lists active connected peers.
- Plain text messages are encrypted, signed, sent, verified, decrypted, displayed, and saved.
- Room messages can be sent with `/msg #room message` after peers join the room.
- Message history is saved to `message_history.json` and viewed with `/history`.
- Message history can be cleared with `/history clear`.
- Closing a peer triggers heartbeat timeout and disconnect cleanup.
- Restarting a peer allows rediscovery/reconnection.
- Busy TCP ports are skipped during automatic startup.
