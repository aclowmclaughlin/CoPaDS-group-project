# Sprint 2 Documentation
## Secure Distributed Messenger

**Team Name:** Team 7

**Team Members:**
- Rue Clow-McLaughlin   - Security/AesEncryption.cs
- Devlin Gallagher      - Security/RsaEncryption.cs
- Nicholas Merante      - Security/MessageSigner.cs
- Sophie Duquette       - Security/KeyExchange.cs

**Date:** March 27, 2026

**Github**
https://github.com/aclowmclaughlin/CoPaDS-group-project
---

## Build & Run Instructions
Build and run instructions are same as Sprint 1, which are as follows.

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

#### Key Exchange Process
Given two clients/peers:

1. A sends B the public key
2. B generates AES key, and sends it to A
3. A then decrypts the AES key
4. Both A and B then store the AES key for the encryption

#### Message Encryption
The plaintext messages are first encrypted using the shared AES key. This is then signed by the using the sender's private key before being sent to the recipient, which is sent alongside the client's signature.

- **Algorithm:** AES-256-CBC
- **Key Size:** 32 bytes
- **IV Generation:** randomly generated (16-bytes) for each message and inserted at the start of the encrypted message

#### Message Signing
[Describe how messages are signed and verified]
The encrypted message and the IV are being signed, which is then verified using the signature once the ciphered message is sent to the recipient. The message is decrypted only on successful verification, otherwise the ciphertext is not evaluated.

- **Algorithm:** RSA with OEAPSHA-256
- **Key Size:** 2048 bits

---

## Key Management

### Key Generation
[Describe when and how keys are generated]
When a `keyExchange` object is created, a RSA key pair is generated with a public and private key for each peer. A session key is also created, which is given to any peers with that RSA public key.

### Key Storage
During runtime, RSA keys are stored in the `_rsa` variable, and the AES session key is stored as a byte array in `_sessionKey`. 

### Key Lifetime
| Key Type | Generated When | Expires When |
|----------|----------------|--------------|
| RSA Key Pair | | |
| AES Session Key | | |

---

## Wire Protocol

### Message Format
```
[Describe your message format, e.g.:]
[4 bytes: length][1 byte: type][payload]
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
- message content is AES encrypted
- integrity since messages are signed
- session keys are RSA encrypted


### Threats Addressed
| Threat | Mitigation |
|--------|------------|
| Eavesdropping | AES encryption of all messages |
| Man-in-the-middle | [Your mitigation] |
| Message tampering | Digital signatures |
| Replay attacks | no current mitigation |
| | |

### Known Limitations
- metadata is not encrypted
- checking integrity for public keys

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
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| Messages are encrypted on wire | Cannot read plaintext in network capture | | |
| Key exchange completes | Both peers have shared session key | | |
| Tampered message rejected | Signature verification fails | | |
| Different keys per conversation | Each peer pair has unique keys | | |

---

## Known Issues

| Issue | Description | Workaround |
|-------|-------------|------------|
| | | |

---

## Video Demo Checklist

Your demo video (5-7 minutes) should show:
- [ ] Two peers connecting and exchanging keys
- [ ] Sending encrypted messages
- [ ] Showing that messages are encrypted (e.g., log output)
- [ ] Demonstrating signature verification
- [ ] Showing what happens with a tampered message (if possible)
- [ ] Multiple simultaneous conversations
