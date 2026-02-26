# Sprint 1 Documentation
## Secure Distributed Messenger

**Team Name:** Team 7

**Team Members:**
- Rue Clow-McLaughlin - ConsoleUI & MessageQueue
- Devlin Gallagher - TCPClientHandler
- Nicholas Merante - TCPServer
- Sophie Duquette - Program

**Date:** 02/27

---

## Build Instructions

### Prerequisites
- .NET SDK version

### Building the Project
```
dotnet build
```

---

## Run Instructions

### Starting the Application
```
dotnet run 
```

### Command Line Arguments (if any)
| Argument | Description | Example |
|----------|-------------|---------|
| | | |

---

## Application Commands

| Command | Description | Example |
|---------|-------------|---------|
| `/connect <ip> <port>` | Connect to a peer | `/connect 192.168.1.100 5000` |
| `/listen <port>` | Start listening for connections | `/listen 5000` |
| `/help`| Displays valid commands | `/help` |
| `/peers` | Lists known/discoverable peers | `/peers` |
| `/history` | Displays message history | `/history` |
| `/exit` | End the current session | `/exit` |
| `/quit` | Exit the application | `/quit` |

---

## Architecture Overview

### Threading Model
[Describe your threading approach - which threads exist and what each does]

- **Main Thread:** [Purpose]
- **Receive Thread:** [Purpose]
- **Send Thread:** [Purpose]
- [Additional threads...]

### Thread-Safe Message Queue
[Describe your message queue implementation and synchronization approach]

---

## Features Implemented

- [x] Multi-threaded architecture - Sophie
- [x] Thread-safe message queue - Rue
- [x] TCP server (listen for connections) -Nick
- [x] TCP client (connect to peers) - Devlin
- [x] Send/receive text messages - Team
- [x] Graceful disconnection handling - Sophie
- [x] Console UI with commands - Rue

---

## Testing Performed

### Test Cases
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| Two instances can connect | Connection established | connection between them established | PASS |
| Messages sent and received | Message appears on other instance | Messages display on another peer instance | PASS |
| Disconnection handled | No crash, appropriate message | disconnection message for peer and user | PASS |
| Thread safety under load | No race conditions | thread conditions and locking handled accurately, no race conditions present | PASS |

---

## Known Issues

| Issue | Description | Workaround |
|-------|-------------|------------|
| | | |

---

## Video Demo Checklist

Your demo video (3-5 minutes) should show:
- [ ] Starting two instances of the application
- [ ] Connecting the instances
- [ ] Sending messages in both directions
- [ ] Disconnecting gracefully
- [ ] (Optional) Showing thread-safe behavior under load
