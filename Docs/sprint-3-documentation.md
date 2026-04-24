# Sprint 3 Documentation (Final)
## Secure Distributed Messenger

**Team Name:** Team 7

**Team Members:**
- Rue Clow-McLaughlin - PeerDiscovery.cs, Server/ClientHandler Combination
- Devlin Gallagher    - ReconnectionPolicy.cs, key saving redesign (save to peers)
- Nicholas Merante    - HearbeatMonitor.cs, key exchange redesign (move to peer connection list), Add peer SendAsync() methods
- Sophie Duquette     - MessageHistory.cs, peer receive callback (in Program) redesign (needs to do gossip protocol)

**Date:** Friday, April 24th

---

**Github** 
https://github.com/aclowmclaughlin/CoPaDS-group-project

## Build & Run Instructions

### Prerequisites
- .NET 9.0 SDK or later

### Building
```bash
dotnet build
```

### Running
```bash
dotnet run --project SecureMessenger.csproj
```
By default, the application automatically starts listening on the first available TCP port starting at `5000`. Port `5001` is skipped because it is reserved for UDP peer discovery.

To request a specific TCP port:
```bash
dotnet run --project SecureMessenger.csproj -- 5004
```

### Command Line Arguments

| Argument | Description | Default |
|----------|-------------|---------|
| `<port>` | Optional TCP listen port. If omitted, the app chooses the first available port from 5000-5999, skipping 5001. | First available port |

---

## Application Commands

| Command | Description | Example |
|---------|-------------|---------|
| `/connect <ip> <port>` | Manually connect to a peer | `/connect 192.168.1.100 5000` |
| `/listen <port>` | Manually start listening if not already listening | `/listen 5004` |
| `/peers` | List connected peers | `/peers` |
| `/history` | View message history | `/history` |
| `/history clear` | Clear local message history | `/history clear` |
| `/quit` | Exit application | `/quit` |
| `/help` | Show help message | `/help` |
| `/create #<room>` | Create and join a room | `/create #test` |
| `/rooms` | List known rooms | `/rooms` |
| `/join #<room>` | Join a room | `/join #test` |
| Any text without `/` | Send a chat message to all connected peers | `hello everyone` |
| `/msg #<room> message` | Send a message to a room you have joined | `/msg #test hello world` |
| `/leave #<room>` | Leave a room | `/leave #test` |

Plain text messages are sent to all connected peers. Room-specific messages use `/msg #<room> message` after joining or creating the room.

---

## Architecture Diagram

```text
+------------------------------ SecureMessenger Peer ------------------------------+
|                                                                                  |
|  +----------------+       +----------------------+       +---------------------+ |
|  | Console UI     | ----> | MessageQueue         | ----> | TcpPeerHandler      | |
|  | - commands     |       | - outgoing messages  |       | - TCP listener      | |
|  | - display      | <---- | - incoming messages  | <---- | - TCP connections   | |
|  +----------------+       +----------------------+       | - key exchange      | |
|                                                          | - encrypted sends   | |
|  +----------------+                                      | - heartbeat loops   | |
|  | MessageHistory | <----------------------------------> | - reconnect policy  | |
|  | - JSON file    |                                      +---------------------+ |
|  +----------------+                                                 ^            |
|                                                                     |            |
|  +----------------+                                                 |            |
|  | PeerDiscovery  | ------------------------------------------------+            |
|  | - UDP 5001     |                                                              |
|  | - broadcasts   |                                                              |
|  +----------------+                                                              |
|                                                                                  |
|  +----------------+       +----------------+       +--------------------------+  |
|  | AES encryption |       | RSA exchange   |       | Message signing          |  |
|  | per message IV |       | session keys   |       | verify before decrypt    |  |
|  +----------------+       +----------------+       +--------------------------+  |
+----------------------------------------------------------------------------------+
```

### Component Descriptions

| Component | Responsibility |
|-----------|----------------|
| `Program.cs` | Starts the app, creates shared services, handles commands, starts background send/receive processing tasks, and coordinates shutdown. |
| `ConsoleUI` | Parses commands and displays chat messages. |
| `MessageQueue` | Provides thread-safe incoming and outgoing message queues using `BlockingCollection`. |
| `TcpPeerHandler` | Owns TCP listening, outgoing peer connections, key exchange, encrypted sends, receive loops, heartbeat monitoring, reconnection, and room membership tracking. |
| `PeerDiscovery` | Broadcasts and listens for peer presence over UDP port 5001. |
| `HeartbeatMonitor` | Tracks last heartbeat time for each active TCP connection and reports connection timeouts. |
| `ReconnectionPolicy` | Attempts reconnection using exponential backoff after failures. |
| `MessageHistory` | Saves and loads message history from `message_history.json`, with a cross-process file lock for local multi-instance testing. |
| `Security` classes | Provide AES encryption, RSA key exchange, and RSA-SHA256 message signing/verification. |

---

## Protocol Specification

### Connection Establishment

```text
Peer A                                      Peer B
  |                                           |
  |---- TCP connect ------------------------->|
  |---- RSA public key ---------------------->|
  |<--- RSA public key -----------------------|
  |---- AES session key encrypted with B key ->|
  |                                           |
  |  Both peers now store the same AES key    |
  |  Messages are AES-encrypted and signed    |
  |                                           |
```

1. The connecting peer opens a TCP connection.
2. Both peers exchange RSA public keys.
3. The connecting peer generates a random AES session key.
4. The connecting peer encrypts the AES session key using the receiver's RSA public key.
5. The receiver decrypts the AES session key using its RSA private key.
6. All later chat, room, and room-listing messages are encrypted and signed.

### Message Flow

1. The user enters a command or plain text in the console.
2. Plain text is converted into a `Chat` message and added to the outgoing queue.
3. `/msg #room message` creates a `RoomChat` message for the specified room.
4. The send task dequeues outgoing messages and sends them to connected peers.
5. Before sending, the message content is AES-encrypted for each peer and signed with RSA.
6. The receiving peer verifies the signature, decrypts the content, and passes the logical message to `Program.cs`.
7. Displayable chat messages are added to the incoming queue and saved to message history.
8. The incoming processing task displays messages through `ConsoleUI`.

### Peer Discovery Protocol

Peer discovery uses UDP broadcast on port `5001`.

#### Broadcast Message Format

```text
PEER:<peerId>:<tcpPort>
```

Example:

```text
PEER:Laptop-12345:5002
```

#### Discovery Process

1. Each peer starts TCP listening automatically.
2. Each peer broadcasts its identity and TCP listen port every 5 seconds.
3. Peers listen for discovery messages on UDP port 5001.
4. A peer ignores discovery messages from itself.
5. If the discovered peer is not already connected, the app may auto-connect.
6. To avoid both peers connecting at the same time, only the peer with the smaller ID initiates the connection.
7. `/peers` lists active TCP connections.

### Heartbeat Protocol

Heartbeats are sent over active TCP connections.

- **Interval:** 5 seconds
- **Timeout:** 15 seconds
- **Action on timeout:** Stop monitoring the peer, disconnect the old connection, clean up streams/sockets, remove the peer from rooms, and start reconnection attempts.

Heartbeat messages are used only for connection health. UDP discovery messages are not treated as heartbeat messages.

---

## P2P Architecture

### Peer Management

Each running application instance is a peer. There is no central server. Every peer can listen for incoming TCP connections and can also initiate outgoing TCP connections to other peers.

Connected peers are stored in `TcpPeerHandler`. Each connected peer has its own TCP stream, RSA public key, AES session key, heartbeat monitoring entry, and send lock.

### Connection Strategy

The app starts listening automatically on the first available TCP port beginning at 5000, skipping 5001 because 5001 is used for UDP discovery. Peers advertise their TCP port through UDP discovery and automatically connect when appropriate.

Manual `/listen` and `/connect` commands are still available as fallback controls for firewall, network, or demo issues.

### Message Routing

Plain text messages are broadcast to all currently connected peers. Room messages are sent with a room name and are displayed only by peers that have joined that room. Message relay through intermediate peers is not implemented.

---

## Resilience Features

### Failure Detection

Each TCP connection sends heartbeat messages every 5 seconds. If a peer does not receive a heartbeat within 15 seconds, the connection is considered failed.

### Automatic Reconnection

When a connection fails, the old connection is cleaned up and reconnection is attempted using exponential backoff.

- **Initial delay:** 1 second
- **Backoff strategy:** 1s → 2s → 4s → 8s → 16s
- **Maximum delay:** 30 seconds
- **Max attempts:** 5

### Graceful Degradation

If a peer disconnects or cannot be reached, the remaining peers continue running. Failed sends are reported without crashing the app, and unavailable peers are removed from active connection tracking.

---

## Message History

### Storage Format

Message history is stored as a JSON array of `Message` objects.

### File Location

History is stored in:

```text
message_history.json
```

This file is ignored by Git because it is local runtime data.

### History Commands

Users can view recent history with:

```text
/history
```

Users can clear saved history with:

```text
/history clear
```

### Local Multi-Instance Behavior

When multiple local peer instances run from the same repository folder, they may all access the same history file. File access is protected with a named mutex so local demo instances do not write to the file at the same time.

---

## User Guide

### Getting Started

1. Build the project with `dotnet build`.
2. Start the app with `dotnet run --project SecureMessenger.csproj`.
3. Start additional instances in separate terminals.
4. Wait a few seconds for UDP discovery and automatic TCP connections.
5. Use `/peers` to confirm connected peers.

### Connecting to Peers

The app attempts automatic peer discovery and connection. Manual connection is also available:

```text
/connect <ip> <port>
```

For same-machine fallback testing:

```text
/connect 127.0.0.1 5000
```

### Sending Messages

To send a general chat message to all connected peers, type text without a slash:

```text
hello everyone
```

To use a room:

```text
/create #demo
/join #demo
/msg #demo hello room
/leave #demo
```

### Viewing Peer Status

Use:

```text
/peers
```

### Viewing History

Use:

```text
/history
```

To clear local history:

```text
/history clear
```

### Troubleshooting

| Problem | Solution |
|---------|----------|
| Cannot connect to peer | Check firewall settings, confirm peers are on the same network, or use manual `/connect <ip> <port>`. |
| UDP discovery does not find peers | Make sure UDP port 5001 is not blocked. Manual `/connect` can be used as a fallback. |
| Port is already in use | Restart the app and let it auto-select a port, or pass a specific port such as `dotnet run --project SecureMessenger.csproj -- 5004`. |
| Messages not sending | Run `/peers` to confirm active TCP connections. |
| Room message says no peers are known in room | Make sure the other peers have run `/join #room` first. |

---

## Features Implemented

### Core Features
- [x] P2P architecture (no central server)
- [x] Peer discovery (UDP broadcast)
- [x] Automatic peer connection
- [x] Heartbeat monitoring
- [x] Failure detection
- [x] Automatic reconnection
- [x] Message history (file-based)
- [x] Parallel message processing

### Security Features (from Sprint 2)
- [x] AES encryption
- [x] RSA key exchange
- [x] Message signing

### Bonus Features (if implemented)
- [ ] Message relay through intermediate peers
- [x] Encrypted history storage
- [ ] Peer persistence (save/load known peers)

---

## Testing Performed

### P2P Tests
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| 3+ peers can form mesh | All peers connected | Three local peers start on separate TCP ports and discover/connect to each other | Pass |
| Peer discovery works | New peer found automatically | UDP discovery broadcasts peer ID and TCP port; peers auto-connect | Pass |
| `/peers` lists connected peers | Active peers are displayed | Connected peers appear with name/address/port/status | Pass |
| Plain text message broadcast | Message reaches all connected peers | Plain text input sends encrypted chat to connected peers | Pass |
| Room messaging | Joined peers receive room message | Peers that join the same room can receive `/msg #room` messages | Pass |
| Message history works | Messages save and reload | `/history` displays saved messages from `message_history.json` | Pass |
| History clear works | History file is cleared | `/history clear` clears saved history | Pass |

### Resilience Tests
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| Kill peer process | Failure detected | Heartbeat timeout detects failed peer and removes connection | Pass |
| Peer rejoins | Connection restored | Restarted peer is rediscovered and can reconnect | Pass |
| Duplicate discovery connections | No repeated duplicate peers | Peer ID comparison prevents both peers from initiating at the same time | Pass |
| Busy port handling | App avoids crashing | Auto-port selection skips unavailable ports and reserved UDP port 5001 | Pass |
| Multiple local history writers | No file access crash | Named mutex synchronizes history file access across local app instances | Pass |

---

## AI Usage Note

AI tools were used as a limited support resource for method documentation, syntax reference, debugging guidance, and documentation wording. The team manually reviewed, tested, and verified all generated suggestions before including them in the project.

---

## Known Issues

| Issue | Description | Severity | Workaround |
|-------|-------------|----------|------------|
| Direct one-to-one peer messaging is not implemented | Plain text messages are broadcast to connected peers, while `/msg #room` is used for room chat. The assignment does not require direct `@peer` messaging. | Low | Use room chat or general connected-peer chat. |
| Public keys are not independently verified | Peers exchange RSA public keys directly, but there is no certificate authority or trust-on-first-use persistence. | Medium | Demo on trusted local peers. |
| Message history encryption uses a local key file | History is encrypted, but the AES key is stored locally in `message_history.key`. Anyone with both the encrypted history file and key file could decrypt it. | Low | Keep `message_history.key` private and ignored by Git; use `/history clear` to remove saved history. |
| UDP discovery can be blocked by firewall/network settings | UDP broadcast may not work on all networks. | Medium | Use manual `/connect <ip> <port>` fallback. |
| Message relay is not implemented | Peers send to directly connected peers only. | Low | Ensure peers are directly connected for the demo. |


---

## Future Improvements

- Add direct one-to-one messaging by peer name.
- Add encrypted message history.
- Add persistent trusted peer keys.
- Add message relay through intermediate peers.
- Improve room membership synchronization and room status display.
- Add a GUI for easier multi-peer demonstrations.

---

## Video Demo Checklist

Your demo video (8-10 minutes) should show:
- [x] Starting 3+ peer instances
- [x] Peer discovery in action
- [x] Messages between multiple peers
- [x] Killing a peer and showing failure detection
- [x] Automatic reconnection when peer returns
- [x] Message history feature
- [x] `/peers` command showing connected peers
