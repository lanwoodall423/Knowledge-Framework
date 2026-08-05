# Knowledge Framework

- Package ID: `lan.knowledgeframework`.
- Adapter source: `DevTools/BridgeAdapter/KnowledgeFrameworkBridgeAdapter.cs`; package output: `DevTools/BridgeAdapters`.
- Build: `DevTools\Build-HotBridgeAdapter.ps1`; validate: `DevTools\Test-BridgeAdapter.ps1`; behavioral suite: `DevTools\Run-KnowledgeFrameworkBehavioralTests.ps1 -SkipBuild`.
- Query fresh live Dev Bridge context before runtime tests with the Dev Bridge checkout's `DevTools\devbridge.ps1`.
- Reload is safe only for adapter-only changes. Gameplay, defs, Harmony, serialized types, or core changes require a full restart.
- Knowledge Framework controls its optional adapter and remains usable without Dev Bridge.
- Full workflow: `DevTools/DEVBRIDGE_AGENT.md`.
