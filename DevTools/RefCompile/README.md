# RimWorld 1.7 reference-only compilation

The two projects here let CI compile Knowledge Framework and the example consumer without an installed copy of the game. The build uses [RimRef](https://github.com/krafs/RimRef) signature-only assemblies (published with Ludeon's permission) and [Harmony](https://www.nuget.org/packages/Lib.Harmony). It does **not** execute RimWorld or validate runtime Harmony patch application.

The pinned baseline is RimRef `1.7.4990-unstable` and Harmony `3.0.0-rc.4`. Update pins deliberately whenever a new game beta changes APIs, and record the tested versions.

```powershell
dotnet build DevTools/RefCompile/KnowledgeFramework.RefCompile.csproj -c Release
dotnet build DevTools/RefCompile/ReferenceConsumer.RefCompile.csproj -c Release
```

Output is isolated in ignored `.artifacts/refcompile/` and **must not be distributed as the mod**. Only `DevTools/Build-KnowledgeFramework.ps1`, using actual installed 1.7 game and Harmony 3 assemblies, can generate the release DLL and external-input manifest. Reference-only success means source API compatibility for the pinned game version; it cannot replace in-game RimTest, save/reload, patch signatures, or FPS testing.
