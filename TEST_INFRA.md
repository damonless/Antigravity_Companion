# E2E Test Infra: Antigravity Companion V0.5

## Test Philosophy
- Opaque-box, requirement-driven. Derives strictly from user requirements in `ORIGINAL_REQUEST.md`.
- Pure native execution: Tests run against built `Antigravity_Companion.exe` or standalone test binaries using native .NET Framework `csc.exe`.
- Methodology: 4-Tier verification (Tier 1: Feature Coverage >=5 per feature; Tier 2: Boundary & Corner Cases >=5 per feature; Tier 3: Cross-Feature Interactions; Tier 4: Real-World Workload Scenarios).

## Feature Inventory & Test Matrix
| # | Feature | Source (requirement) | Tier 1 | Tier 2 | Tier 3 |
|---|---------|---------------------|:------:|:------:|:------:|
| F1 | Custom Title Lossless Extraction | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ |
| F2 | Pure Native Protobuf AST Codec | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ |
| F3 | In-Memory Streaming Zip Inspection | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ |
| F4 | Incremental Protobuf Shard Merger | ORIGINAL_REQUEST §R1, §R3 | 5 | 5 | ✓ |
| F5 | Smart Ephemeral/Junk Project Filter | ORIGINAL_REQUEST §R2 | 5 | 5 | ✓ |
| F6 | Bidirectional Visual Diff Classification | ORIGINAL_REQUEST §R2, §R3 | 5 | 5 | ✓ |
| F7 | Interactive Conflict Resolution Wizard | ORIGINAL_REQUEST §R2 | 5 | 5 | ✓ |
| F8 | Dual-Tree Visual Diff Workspace | ORIGINAL_REQUEST §R3 | 5 | 5 | ✓ |
| F9 | Cascading CheckBox Selection Model | ORIGINAL_REQUEST §R3 | 5 | 5 | ✓ |
| F10 | Fine-Grained Cascading Selective Import | ORIGINAL_REQUEST §R3 | 5 | 5 | ✓ |
| F11 | Post-Import UI Auto-Refresh & Highlight | ORIGINAL_REQUEST §R3 | 5 | 5 | ✓ |
| F12 | Pure Native Single-File Build | ORIGINAL_REQUEST §R4 | 5 | 5 | ✓ |
| F13 | Complete User Documentation & Commit | ORIGINAL_REQUEST §R4 | 5 | 5 | ✓ |

## Test Architecture
- Test runner: `tests/TestRunner.cs` compiled via `csc.exe` to `tests/TestRunner.exe`.
- Mock sandbox: Automated generator creating synthetic `.gemini/antigravity` environments and mock `.agy` zip packages with known UUIDs, custom Chinese titles, conflict cases, junk projects, and brain memory directories.
- Exit code 0 indicates all tests passed; non-zero exit code indicates failure with descriptive error assertion.

## Real-World Application Scenarios (Tier 4)
| # | Scenario | Features Exercised | Complexity |
|---|----------|--------------------|------------|
| 1 | Multi-Machine Developer Migration (350MB package, 20 custom titles, 5 collided projects) | F1, F3, F4, F6, F7, F10 | High |
| 2 | Selective Single Conversation Extraction (Select 1 of 50 conversations, verify only target `.db` + `brain` + `.pb` shard migrated) | F1, F4, F10 | Medium |
| 3 | Junk Project Hygiene & Temp Subagent Isolation (`subagent-*`, `Recovered project` hidden by default, visible on toggle) | F5, F8 | Low |
| 4 | Project Name Collision Resolution (Testing Merge vs Rename vs Skip vs Overwrite behaviors) | F6, F7, F10 | High |
| 5 | Cross-Machine Chinese Title Lossless Integrity (Verify Antigravity opens restored session with 100% exact Chinese name) | F1, F2, F4, F10 | High |

## Coverage Thresholds
- Tier 1: >=5 per feature
- Tier 2: >=5 per feature
- Tier 3: >=13 pairwise interaction tests
- Tier 4: >=5 realistic real-world application scenarios
- Total Target: >=150 test assertions
