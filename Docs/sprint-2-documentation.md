# Sprint 2 Documentation
## Secure Distributed Messenger

**Team Name:** Team 7

**Team Members:**
- Rue Clow-McLaughlin   - Security/AesEncryption.cs
- Devlin Gallagher      - Security/RsaEncryption.cs
- Nicholas Merante      - Security/MessageSigner.cs
- Sophie Duquette       - Security/KeyExchange.cs

**Date:** March 27, 2026
**Sprint:** Sprint 2

**Github**
https://github.com/aclowmclaughlin/CoPaDS-group-project
---

## Build & Run Instructions
Build and run instructions remain the same as Sprint 1.

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

## Security Protocol Overview

### Encryption Protocol
The messenger uses a hybrid cryptographic design. RSA is used to exchange a shared AES session key, and AES is then used to encrypt message contents. Encrypted messages are signed before delivery so recipients can verify integrity before decrypting.

#### Key Exchange Process
Given two clients/peers:

1. The initiating peer sends its RSA public key to the other peer.
2. The receiving peer generates a new AES session key.
3. The AES session key is encrypted with the initiator's RSA public key.
4. The encrypted session key is sent back to the initiator.
5. The initiator decrypts the session key with its RSA private key.
6. Both peers use the shared AES key for subsequent encrypted messages.

#### Message Encryption
Plaintext messages are encrypted with the shared AES session key before being sent. A fresh IV is generated for each message and prepended to the encrypted payload. The resulting encrypted byte array is then signed with the sender's RSA private key, and the signature is sent alongside the encrypted message.

- **Algorithm:** AES-256-CBC
- **Key Size:** 32 bytes
- **IV Generation:** Randomly generated 16-byte IV for each message, prepended to the encrypted payload

#### Message Signing
The sender signs the full encrypted payload using its RSA private key. Because the IV is prepended to the ciphertext before signing, the signature covers both the IV and the ciphertext. On receipt, the recipient verifies the signature with the sender's RSA public key before attempting decryption. If verification fails, the message is rejected.

- **Algorithm:** RSA with SHA-256 for signatures; RSA-OAEP with SHA-256 for encrypting session keys
- **Padding:** PKCS#1 v1.5
- **Key Size:** 2048 bits

### Verification Rule
Incoming encrypted messages are only decrypted after signature verification succeeds. Messages that fail verification are rejected and not processed as plaintext.

---

## Key Management

### Key Generation
Each peer creates an RSA key pair during runtime. During key exchange, the receiving peer generates an AES session key and encrypts it with the other peer's RSA public key before sending it back.

### Key Storage
During runtime, RSA key material is stored in `RsaEncryption` / `KeyExchange` objects. Shared AES session keys are stored per peer so separate encrypted conversations can use separate keys.

### Key Lifetime
| Key Type | Generated When | Expires When |
|----------|----------------|--------------|
| RSA Key Pair | When `RsaEncryption` / `KeyExchange` objects are created during runtime | End of process/runtime; not persisted |
| AES Session Key | When responder creates encrypted session key during key exchange | End of process/runtime or until dictionaries are cleared/disconnect occurs |

---

## Wire Protocol

### Message Format
```

There are two message encodings used:

1. **Initial TCP connection setup**
   - `[4 bytes: RSA public key length][public key bytes]`

2. **Application messages**
   - `[ASCII decimal JSON length][newline][JSON serialized Message object]`

The JSON `Message` object contains fields such as:
| Type | Purpose |
|------|---------|
| `Chat` | Encrypted direct message |
| `PublicKey` | Public key exchange message |
| `SessionKey` | RSA-encrypted AES session key |
| `RoomChat` | Encrypted room message |
| `RegisterClient` | Registers a client's logical name with the server |
| `ListPeers` | Requests connected peer list |
| `ListRooms` | Requests room list |
| `ListPeersInRoom` | Requests peers and public keys for a room |
| `CreateRoom` | Creates a room |
| `JoinRoom` | Joins a room |
| `LeaveRoom` | Leaves a room |
| `ListPeersReply` | Server response with peer list |
| `ListRoomsReply` | Server response with room list |
| `ListPeersInRoomReply` | Server response with room membership/public keys |
| `ServerNotice` | Server status/notification message |
```

### Message Types
| Type ID | Name | Description |
|---------|------|-------------|
| 0x01 | PUBLIC_KEY | RSA public key exchange |
| 0x02 | SESSION_KEY | Encrypted AES session key |
| 0x03 | MESSAGE | Encrypted chat message |
| 0x04 | SIGNED_MESSAGE | Signed and encrypted message |

---

## Threat Model

### Assets Protected
- Message content is AES encrypted
- Integrity since messages are signed
- AES session keys are protected during exchange using RSA encryption


### Threats Addressed
| Threat | Mitigation |
|--------|------------|
| Eavesdropping | AES-256-CBC encryption of message contents |
| Message tampering | RSA signatures verified before decryption |
| Replay attacks | No current mitigation |
| Man-in-the-middle | Not fully mitigated; exchanged public keys are not independently authenticated |

### Known Limitations
- Message metadata such as sender, target, and room information is not fully protected
- Public keys are exchanged without an external trust mechanism
- Replay protection is not currently implemented

---

## Features Implemented

- [X] AES encryption of messages
- [X] RSA key pair generation
- [X] RSA key exchange
- [X] AES session key exchange (encrypted with RSA)
- [X] Message signing
- [X] Signature verification
- [X] Multiple simultaneous conversations
- [X] Per-conversation encryption keys

---

## Testing Performed

### Security Tests
| Issue | Description | Workaround |
|-------|-------------|------------|
| No replay protection | Duplicate encrypted messages are not currently detected | None currently implemented |
| Public key trust | Public keys are accepted during exchange without external validation | Use only in trusted demo/test environments |
| Metadata exposure | Routing information is still visible to the server/application layer | Limit sensitive information in room names and identifiers |

---

## Known Issues

| Issue | Description | Workaround |
|-------|-------------|------------|
| No replay protection | Replayed messages are not detected | None |
| Public key trust | Public keys are not independently verified | Trusted demo use only |
| Metadata exposure | Sender/room info is not encrypted | Avoid sensitive names |

---

## Video Demo Checklist

Your demo video (5-7 minutes) should show:
- [ ] Two peers connecting and exchanging keys
- [ ] Sending encrypted messages
- [ ] Showing that messages are encrypted (e.g., log output)
- [ ] Demonstrating signature verification
- [ ] Showing what happens with a tampered message (if possible)
- [ ] Multiple simultaneous conversations
