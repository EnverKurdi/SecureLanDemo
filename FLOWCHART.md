# SecureLanDemo System Flowchart

## System Architecture Overview

```mermaid
graph TB
    subgraph Client["🖥️ CLIENT (Port 9200)"]
        CLI["Console UI<br/>- Login<br/>- List/Upload/Download"]
        TLS_C["TLS Stream<br/>Encrypted Transport"]
    end

    subgraph Server["🔒 SERVER (Port 9200)"]
        AUTH["User Authenticator<br/>Username/Password"]
        ACL["Access Control Manager<br/>Role-Based Permissions"]
        FS["File Service<br/>Encryption Orchestration"]
        WP_S["Wire Protocol"]
    end

    subgraph HSM["🛡️ HSM EMULATOR (Port 9000)"]
        KEK["KEK Storage<br/>AES-256<br/>Never Exported"]
        WRAP["Key Wrapping<br/>WRAP/UNWRAP"]
    end

    subgraph DataStore["💾 DATASTORE (Port 9100)"]
        DB["In-Memory Database<br/>FileRecord Storage"]
        WP_D["Wire Protocol"]
    end

    Client -->|TLS| Server
    Server -->|Authenticate| AUTH
    Server -->|Check Permission| ACL
    Server -->|Wrap/Unwrap DEK| HSM
    HSM -->|KEK Protected| KEK
    HSM -->|Wrap/Unwrap Ops| WRAP
    Server -->|Store/Load| DataStore
    DataStore -->|Serialize| WP_D
```

---

## Authentication Flow

```mermaid
sequenceDiagram
    participant C as Client
    participant S as Server
    participant A as UserAuthenticator

    C->>S: LOGIN + username + password
    S->>A: Authenticate(user, pass)
    alt Authentication Success
        A-->>S: OK, group
        S-->>C: LOGIN_OK + group + allowed_folders
        C->>C: Display folders
    else Authentication Failed
        A-->>S: FAIL
        S-->>C: LOGIN_FAILED
        C->>C: Display error, return
    end
```

---

## File Upload Flow

```mermaid
sequenceDiagram
    participant C as Client
    participant S as Server
    participant FS as FileService
    participant H as HSM
    participant D as DataStore

    C->>S: UPLOAD + folder + filename + plaintext
    S->>FS: SaveFileAsync(user, folder, file, plaintext)
    
    rect rgb(200, 220, 255)
    Note over FS: 1. Generate DEK
    FS->>FS: DEK = Random 32 bytes
    end
    
    rect rgb(200, 255, 220)
    Note over FS: 2. Encrypt File Content
    FS->>FS: (nonce, ciphertext, tag) = AES-GCM(DEK, plaintext)
    FS->>FS: ZeroMemory(plaintext)
    end
    
    rect rgb(255, 220, 200)
    Note over FS,H: 3. Wrap DEK in HSM
    FS->>H: WrapKey(plaintext DEK)
    H->>H: (nonce, wrapped, tag) = AES-GCM(KEK, DEK)
    H->>H: ZeroMemory(DEK)
    H-->>FS: wrapped DEK blob
    FS->>FS: ZeroMemory(DEK)
    end
    
    rect rgb(220, 220, 255)
    Note over FS,D: 4. Store to DataStore
    FS->>D: SAVE + ciphertext + wrapped_dek
    D->>D: Store FileRecord
    D-->>FS: FileId
    FS-->>S: FileId
    S-->>C: UPLOAD_OK + FileId
    end
```

---

## File Download Flow

```mermaid
sequenceDiagram
    participant C as Client
    participant S as Server
    participant FS as FileService
    participant D as DataStore
    participant H as HSM

    C->>S: DOWNLOAD + fileId
    S->>FS: LoadFileAsync(user, fileId)
    
    rect rgb(220, 220, 255)
    Note over FS,D: 1. Load from DataStore
    FS->>D: LOAD + fileId
    D-->>FS: FileRecord(ciphertext, wrapped_dek)
    end
    
    rect rgb(200, 220, 255)
    Note over FS: 2. Check ACL Permission
    FS->>FS: HasPermission(user, folder, read)
    end
    
    rect rgb(255, 220, 200)
    Note over FS,H: 3. Unwrap DEK from HSM
    FS->>H: UNWRAP + wrapped_dek
    H->>H: DEK = AES-GCM-Decrypt(KEK, wrapped_dek)
    H-->>FS: plaintext DEK
    end
    
    rect rgb(200, 255, 220)
    Note over FS: 4. Decrypt File Content
    FS->>FS: plaintext = AES-GCM-Decrypt(DEK, ciphertext)
    FS->>FS: ZeroMemory(DEK)
    FS-->>S: plaintext
    end
    
    S->>S: ZeroMemory(plaintext)
    S-->>C: DOWNLOAD_OK + plaintext
    C->>C: Display to console
```

---

## LIST Command Flow

```mermaid
graph TD
    A["Client: LIST"] -->|Send LIST| B["Server"]
    B -->|Query| C["DataStore"]
    C -->|Return all FileMeta| D["File Metadata List"]
    D -->|Filter by ACL| E["AccessControlManager"]
    E -->|User permissions| F["Filtered File List"]
    F -->|Return to Client| G["Client: Display Files"]
    
    style A fill:#e1f5ff
    style B fill:#fff3e0
    style C fill:#f3e5f5
    style E fill:#fff3e0
    style G fill:#e1f5ff
```

---

## Encryption/Decryption Detail

```mermaid
graph LR
    subgraph Upload["UPLOAD - Encryption"]
        U1["Plaintext File"] -->|AES-256-GCM<br/>with DEK| U2["Ciphertext +<br/>Nonce + Tag"]
        U3["DEK 32 bytes"] -->|AES-256-GCM<br/>with KEK| U4["Wrapped DEK +<br/>Nonce + Tag"]
        U1 --> U5["RAM: Clear DEK +<br/>Plaintext"]
    end
    
    subgraph DataStore["STORAGE"]
        DS["Ciphertext +<br/>Wrapped DEK<br/>+ Metadata"]
    end
    
    subgraph Download["DOWNLOAD - Decryption"]
        D1["Wrapped DEK"] -->|AES-256-GCM<br/>Decrypt with KEK| D2["DEK 32 bytes"]
        D3["Ciphertext"] -->|AES-256-GCM<br/>Decrypt with DEK| D4["Plaintext File"]
        D2 --> D5["RAM: Clear DEK"]
    end
    
    U2 & U4 --> DS
    DS --> D1 & D3
    
    style Upload fill:#c8e6c9
    style DataStore fill:#ffe0b2
    style Download fill:#bbdefb
```

---

## ACL & Permission Matrix

```mermaid
graph TB
    subgraph Groups["User Groups"]
        G1["👑 Group1: Admin"]
        G2["👤 Group2: TeamA"]
        G3["👤 Group3: TeamB"]
    end
    
    subgraph Users["Users"]
        U1["UserAdmin"] --> G1
        U2["userA, userB, userE"] --> G2
        U3["userC, userD, userF"] --> G3
    end
    
    subgraph Permissions["Folder Permissions"]
        G1 -->|Read/Write| F1["Folder_Group2"]
        G1 -->|Read/Write| F2["Folder_Group3"]
        G2 -->|Read/Write| F1
        G3 -->|Read/Write| F2
    end
    
    style G1 fill:#ffcccc
    style G2 fill:#ccffcc
    style G3 fill:#ccccff
```

---

## Error Handling Flow

```mermaid
graph TD
    A["Client Action"] -->|Send Command| B["Server Receives"]
    
    B --> C{Authentication<br/>Check}
    C -->|Not Logged In| D["❌ ERROR_NOT_LOGGED_IN"]
    C -->|OK| E{Command<br/>Valid?}
    
    E -->|Unknown| F["❌ Unknown Command"]
    E -->|LOGIN| G["Authenticate User"]
    E -->|LIST| H["Query DataStore"]
    E -->|UPLOAD| I["Validate ACL"]
    E -->|DOWNLOAD| J["Validate ACL"]
    
    G -->|Invalid| K["❌ LOGIN_FAILED"]
    G -->|Valid| L["✅ LOGIN_OK"]
    
    I -->|Denied| M["❌ DENIED"]
    H -->|OK| N["✅ Return Metadata"]
    I -->|OK| O["Encrypt & Store"]
    J -->|OK| P["Decrypt & Return"]
    
    D --> Q["Client: Error"]
    F --> Q
    K --> Q
    M --> Q
    L --> Q
    N --> Q
    O --> Q
    P --> Q
    
    style D fill:#ffcccc
    style F fill:#ffcccc
    style K fill:#ffcccc
    style M fill:#ffcccc
    style L fill:#ccffcc
    style N fill:#ccffcc
    style O fill:#ccffcc
    style P fill:#ccffcc
```

---

## System Startup Sequence

```mermaid
sequenceDiagram
    participant User as User Terminal
    participant T as Terminal 1: HSM
    participant T2 as Terminal 2: DataStore
    participant T3 as Terminal 3: Server
    participant T4 as Terminal 4: Client

    User->>T: dotnet run --port 9000
    T->>T: HSM Listening on 127.0.0.1:9000
    activate T

    User->>T2: dotnet run --port 9100
    T2->>T2: DataStore Listening on 127.0.0.1:9100
    activate T2

    User->>T3: dotnet run --listenPort 9200 --hsmPort 9000 --dataPort 9100
    T3->>T: Connect to HSM
    T3->>T2: Connect to DataStore
    T3->>T3: Server Listening on 127.0.0.1:9200
    activate T3

    User->>T4: dotnet run --port 9200
    T4->>T3: TLS Connect to Server
    T4->>T4: Client Ready for User Input
    activate T4

    Note over T,T4: System Ready for User Commands
```

---

## Wire Protocol Message Structure

```mermaid
graph TD
    A["Wire Protocol Message"] --> B["Length Prefix<br/>4 bytes LE"]
    A --> C["Data Type"]
    
    C -->|String| D["String<br/>4-byte length + UTF-8"]
    C -->|Int32| E["Int32<br/>4 bytes LE"]
    C -->|Int64| F["Int64<br/>8 bytes LE"]
    C -->|Bool| G["Bool<br/>1 byte 0/1"]
    C -->|Bytes| H["Bytes<br/>4-byte length + raw"]
    
    I["Example: WriteString 'LOGIN'"] -->|5 chars| J["0x05 0x00 0x00 0x00<br/>0x4C 0x4F 0x47 0x49 0x4E"]
    
    style A fill:#fff9c4
    style B fill:#b3e5fc
    style I fill:#f8bbd0
    style J fill:#f8bbd0
```

---

## Data at Rest vs In Transit

```mermaid
graph LR
    subgraph Transit["🔒 IN TRANSIT (Client ↔ Server)"]
        T1["TLS 1.2/1.3<br/>All Data Encrypted<br/>via SslStream"]
    end
    
    subgraph ServerRAM["RAM: Server<br/>(Transient)"]
        R1["Plaintext File<br/>DEK<br/>Auth Credentials"]
        R1 -.->|ZeroMemory<br/>After Use| R2["Cleared"]
    end
    
    subgraph Rest["💾 AT REST (DataStore)"]
        DS1["Ciphertext File +<br/>AES-256-GCM"]
        DS2["Wrapped DEK +<br/>AES-256-GCM"]
        DS3["Metadata<br/>(FileId, Owner, Timestamp)"]
    end
    
    subgraph HSM["🛡️ HSM (Hardware Protected)"]
        H["KEK<br/>Never Exported<br/>Only Used for<br/>Wrap/Unwrap"]
    end
    
    Transit -->|If Intercepted| T2["TLS Breaks<br/>No Access to<br/>Plaintext"]
    
    Rest -->|If Accessed| DS4["No DEK Available<br/>No Plaintext"]
    DS4 -->|Would Need| H
    
    style Transit fill:#c8e6c9
    style ServerRAM fill:#fff9c4
    style Rest fill:#bbdefb
    style HSM fill:#f8bbd0
    style H fill:#ffcccc
```

---

## Threat Model & Mitigation

```mermaid
graph TD
    A["Threat: Network Eavesdropping"] -->|Mitigation| B["TLS Encryption<br/>All traffic encrypted"]
    
    C["Threat: DataStore Breach"] -->|Mitigation| D["Only Ciphertext Stored<br/>No Plaintext<br/>No DEK"]
    
    E["Threat: DEK Extraction"] -->|Mitigation| F["DEK Only in RAM<br/>Wrapped in HSM<br/>Immediately Zeroed"]
    
    G["Threat: Unauthorized Access"] -->|Mitigation| H["ACL Enforcement<br/>Role-Based Permissions<br/>Per-User Validation"]
    
    I["Threat: HSM Compromise"] -->|Mitigation| J["KEK Never Exported<br/>Hardware Protected<br/>Only Wrap/Unwrap Ops"]
    
    K["Threat: Memory Dumps"] -->|Mitigation| L["CryptographicOperations<br/>.ZeroMemory()<br/>On Sensitive Data"]
    
    style B fill:#c8e6c9
    style D fill:#c8e6c9
    style F fill:#c8e6c9
    style H fill:#c8e6c9
    style J fill:#c8e6c9
    style L fill:#c8e6c9
```
