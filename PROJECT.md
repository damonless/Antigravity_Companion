# Project: Antigravity Companion V0.5 (Dual-Pane Visual Diff & Selective Import Manager)

## Architecture
Antigravity Companion V0.5 is a single-file, zero-dependency native Windows Forms application (.NET Framework 4.0/4.5+ C# 5) designed for lossless migration, visual diff comparison, and fine-grained selective cascading import of Antigravity IDE environments.

### Core Architectural Layers:
1. **Data & Storage Layer (Engine)**:
   - `ProtoAstCodec`: Pure native C# Protocol Buffers wire format parser and serializer (AST-based, supporting Varint, Fixed64, Length-Delimited, Fixed32).
   - `SessionMetadataExtractor`: Parses `agyhub_summaries_proto.pb` extracting custom titles (`Field 15 custom_title_meta`), auto summaries, timestamps, workspace/folder URIs, and step counts without spawning external processes.
   - `StreamingZipIndexReader`: Reads metadata (<150KB total: `metadata.json`, `projects/*.json`, `agyhub_summaries_proto.pb`) in memory directly from `.agy` ZipArchive streams in <150ms without disk extraction.
   - `SelectiveCascadingImporter`: Safely extracts selected `conversations/<uuid>.db*`, `brain/<uuid>/**`, and performs non-destructive incremental splicing into local `agyhub_summaries_proto.pb`.
2. **Diff & Strategy Layer**:
   - `SmartProjectFilter`: Identifies and filters ephemeral junk projects (`subagent-*`, `outside-of-project`, `default-cli-project`, `Recovered project`, `temp-*`).
   - `VisualDiffEngine`: Computes bidirectional 3-way diff between Local and Backup:
     - `[★ 新增]` (New in Backup)
     - `[已对齐]` (Exists on both sides, identical)
     - `[重名/路径差异]` (Name collision / path conflict)
     - `[本地独有]` (Local only)
   - `ConflictResolutionWizard`: Modal dialog offering 4 strategies (`Merge`, `Rename`, `Overwrite`, `Skip`) with "Apply to all remaining conflicts".
3. **Presentation Layer (WinForms UI)**:
   - `DoubleBufferedTreeView`: Smooth, flicker-free rendering of large trees.
   - `Left Pane (tvLocal)`: Local environment hierarchy (Projects -> Sessions with custom titles & timestamps, Global Configs).
   - `Right Pane (tvBackup)`: Backup archive hierarchy with cascading tri-state CheckBoxes, diff status color coding, and quick-select toolbar (`全选`, `全不选`, `仅选新增`).
   - `Bottom Workspace`: TabControl containing Action Logs (`rtbLog`), Config/Rule Inspector, and Diff Details.
4. **Build & Toolchain Layer**:
   - Single-file zero-warning native build via `build.bat` using `csc.exe` targeting .NET Framework 4.0/4.5+, embedding `app.ico`.

---

## Feature Inventory
| # | Feature | Description | Milestone | Source | Status |
|---|---------|-------------|-----------|--------|:------:|
| F1 | Custom Title Lossless Extraction | Parse `Field 15` (`custom_title_meta`) in `agyhub_summaries_proto.pb` to extract user-renamed Chinese/custom session titles without fallback to English summaries | M1 | ORIGINAL_REQUEST §R1 | **DONE** |
| F2 | Pure Native Protobuf AST Codec | In-memory wire-format parsing and byte-exact serialization of `.pb` files in pure .NET 4.0/4.5+ C# 5 | M1 | ORIGINAL_REQUEST §R1, Survey | **DONE** |
| F3 | In-Memory Streaming Zip Inspection | Sub-second extraction of `<150KB` metadata from `.agy` archives directly in memory without disk unpack | M1 | ORIGINAL_REQUEST §R1, Survey | **DONE** |
| F4 | Incremental Protobuf Shard Merger | Merge selected session entry messages into local `agyhub_summaries_proto.pb` while preserving local entries and remapping `folderUri` | M1 | ORIGINAL_REQUEST §R1, §R3 | **DONE** |
| F5 | Smart Ephemeral/Junk Project Filter | Automatically filter `subagent-*`, `outside-of-project`, `default-cli-project`, `Recovered project` with UI toggle `chkHideTemp` | M2 | ORIGINAL_REQUEST §R2 | **DONE** |
| F6 | Bidirectional Visual Diff Classification | Classify project and session nodes into New, Aligned, Conflicted (Name collision/Path mismatch), and Local-Only | M2 | ORIGINAL_REQUEST §R2, §R3 | **DONE** |
| F7 | Interactive Conflict Resolution Wizard | Modal `ConflictResolutionDialog` offering Merge, Rename, Overwrite, Skip strategies with "Apply to all" option | M2 | ORIGINAL_REQUEST §R2 | **DONE** |
| F8 | Dual-Tree Visual Diff Workspace | SplitContainer with left local tree and right backup tree with colored diff badges and metadata | M3 | ORIGINAL_REQUEST §R3 | **DONE** |
| F9 | Cascading CheckBox Selection Model | Tri-state parent-child cascading selection with re-entrancy protection and Quick-Select bar (`全选`, `全不选`, `仅选新增`) | M3 | ORIGINAL_REQUEST §R3 | **DONE** |
| F10 | Fine-Grained Cascading Selective Import | Extract only checked conversations (`.db*`), memory trajectories (`brain/<uuid>/**`), and `.pb` shard into local environment without contaminating existing data | M3 | ORIGINAL_REQUEST §R3 | **DONE** |
| F11 | Post-Import UI Auto-Refresh & Highlight | Automatically reload local tree after import and highlight newly imported projects/sessions in bold green | M3 | ORIGINAL_REQUEST §R3 | **DONE** |
| F12 | Pure Native Single-File Build | One-click zero-warning compilation via `build.bat` using `csc.exe`, embedding `app.ico` into `Antigravity_Companion.exe` | M4 | ORIGINAL_REQUEST §R4 | IN_PROGRESS |
| F13 | Complete User Documentation & Commit | Update `Antigravity_Companion使用说明书.md` with V0.5 Dual-Tree guide, `.gitignore`, and BMS 3-part commit | M4 | ORIGINAL_REQUEST §R4 | IN_PROGRESS |
| F14 | Comprehensive Multi-Tier E2E Verification | Automated test suite validating build, metadata parsing, filtering, conflict resolution, selective import, and zero contamination | M5 / Test Track | ORIGINAL_REQUEST §Acceptance Criteria | **DONE** (Suite Ready) |

---

## Milestones

| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| E2E | E2E Testing Track | Requirement-driven test harness, mock sandboxes, Tiers 1-4 automated test suite | None | **DONE** |
| M1 | Metadata & Stream Parser Engine (R1) | Protobuf AST Codec, custom title extractor, streaming zip reader, shard merger | None | **DONE** |
| M2 | Filtering, Diff & Conflict Engine (R2) | Junk project filter, 3-way diff calculator, conflict resolution dialog | M1 | **DONE** |
| M3 | Dual-Tree WinForms UI & Selective Import (R3) | Dual-Tree layout, cascading checkboxes, quick select bar, cascading import worker, post-import highlight | M1, M2 | **DONE** |
| M4 | Native Build, Documentation & Commit (R4) | Single-file zero-warning build, app.ico, manual update, .gitignore, BMS commit format | M3 | IN_PROGRESS |
| M5 | Final E2E Verification & Adversarial Hardening | Pass 100% E2E test suite (Tiers 1-4) + Tier 5 adversarial stress testing | E2E, M4 | PLANNED |

---

## Interface Contracts

### 1. Protobuf & Metadata Contract (`ProtoCodec` ↔ `DataLayer`)
```csharp
public class ProtoNode {
    public int FieldNumber;
    public int WireType;
    public ulong VarintValue;
    public ulong Fixed64Value;
    public uint Fixed32Value;
    public byte[] LengthDelimitedValue;
    public List<ProtoNode> Children;
    
    public static List<ProtoNode> Decode(byte[] buffer);
    public static byte[] Encode(List<ProtoNode> nodes);
    public string GetString();
}

public class ConversationSummaryInfo {
    public string ConversationId;      // UUID (Field 1)
    public string DisplayTitle;        // Field 15 Custom Title if present, else Field 1 Auto Summary
    public string AutoSummaryTitle;    // Field 1
    public string CustomTitle;         // Field 15 -> Field 1
    public bool HasCustomTitle;
    public string FolderUri;           // Field 9 -> Field 1 or Field 17 -> Field 7
    public string ProjectName;
    public DateTime UpdatedTime;
    public DateTime CreatedTime;
    public int StepCount;
    public ProtoNode RawProtoEntry;    // Preserved for lossless merge
}
```

### 2. Diff & Filter Contract (`DiffEngine` ↔ `Presentation`)
```csharp
public enum DiffStatus {
    NewInBackup,    // [★ 新增]
    Aligned,        // [已对齐]
    Conflicted,     // [重名/路径差异]
    LocalOnly       // [本地独有]
}

public class ProjectDiffNode {
    public string ProjectName;
    public string LocalFolderUri;
    public string BackupFolderUri;
    public DiffStatus Status;
    public bool IsTemporaryOrJunk;
    public List<ConversationSummaryInfo> LocalConversations;
    public List<ConversationSummaryInfo> BackupConversations;
}

public enum ConflictResolutionStrategy {
    Merge,
    Rename,
    Overwrite,
    Skip
}
```

### 3. Selective Import Contract (`ImportManager` ↔ `FileSystem`)
```csharp
public class SelectiveImportRequest {
    public string BackupFilePath;
    public List<string> SelectedConversationIds;
    public List<string> SelectedProjectNames;
    public Dictionary<string, ConflictResolutionStrategy> ProjectStrategies;
    public Dictionary<string, string> ProjectRenames;
    public bool ImportGlobalRules;
    public bool ImportGlobalSkills;
    public bool ImportGlobalMcp;
}

public class SelectiveImportResult {
    public bool Success;
    public int ImportedConversationsCount;
    public int ImportedBrainDirsCount;
    public int MergedProtoSummariesCount;
    public List<string> ImportedConversationIds;
    public List<string> ImportedProjectNames;
    public List<string> Errors;
}
```

---

## Code Layout
- `MainForm.cs`: Monolithic single-file WinForms application (contains all UI components, Proto AST engine, diff calculator, selective importer, conflict wizard, and backup/restore handlers).
- `build.bat`: Native build script compiling `MainForm.cs` via `csc.exe` with `/warnaserror` and `/win32icon:app.ico`.
- `app.ico`: Windows icon resource embedded in `Antigravity_Companion.exe`.
- `Antigravity_Companion使用说明书.md`: User manual and technical reference.
- `tests/`: Automated E2E test harness and test suites.
- `.agents/`: Coordination and orchestration metadata only.
