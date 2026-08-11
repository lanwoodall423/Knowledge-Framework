# Knowledge Framework

- Package ID: `lan.knowledgeframework`.
- Adapter source: `DevTools/BridgeAdapter/KnowledgeFrameworkBridgeAdapter.cs`; package output: `DevTools/BridgeAdapters`.
- Build: `DevTools\Build-HotBridgeAdapter.ps1`; validate: `DevTools\Test-BridgeAdapter.ps1`; behavioral suite: `DevTools\Run-KnowledgeFrameworkBehavioralTests.ps1 -SkipBuild`.
- DevBridge2 is the only supported live-test coordinator: use `C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2\DevBridge.cmd` for status, leases, restart, and readiness.
- DevBridge2 has no adapter-registration or adapter-reload protocol. The historical adapter is not a release input.
- Gameplay, defs, Harmony, serialized types, or core changes require a full DevBridge2 restart followed by wait-ready.
- Knowledge Framework remains usable without DevBridge2.
- Full workflow: `DevTools/DEVBRIDGE2_AGENT.md` in the consuming mod repository.
