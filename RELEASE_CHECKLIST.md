# Release Checklist

- Confirm the release decision and update `VERSION`.
- Confirm `About/About.xml`, documentation, assembly metadata, API baseline, and
  bridge manifest use the same semantic release version.
- Run `pwsh ./DevTools/Verify-KnowledgeFramework.ps1 -AllowBlockedIntegration`.
- Run `pwsh ./Examples/ReferenceConsumer/Verify-ReferenceConsumer.ps1`.
- With legitimate local dependencies, run the Release build in [BUILDING.md](BUILDING.md).
- Verify `1.6/Assemblies/KnowledgeFramework.dll` and its build manifest hashes.
- Run `pwsh ./DevTools/Test-ReleaseContract.ps1`.
- Run `pwsh ./DevTools/Test-BridgeAdapter.ps1` when the optional adapter is part of
  the documented integration package.
- Run the behavioral harness against both the shipped and source-built DLL where
  local dependencies permit it.
- Run the canonical RELEASE runtime suite in a disposable RimWorld environment:
  `& ..\RimTest\rimliaison.cmd suite run release --json`. It runs the existing
  V2/V3 behavioral and two-consumer isolation checks before and after a real
  save/reload, then invokes bounded V3 stress validation.
- Archive `Config/KnowledgeFramework_Verification.txt` and
  `Config/KnowledgeFramework_Stress.txt` with the runtime evidence. A stress
  prerequisite gap is BLOCKED/UNAVAILABLE, never a synthetic pass.
- Check XML, translations, internal documentation links, and unintended packaged
  artifacts.
- Report PASS, FAIL, and BLOCKED/UNAVAILABLE checks separately.
- Do not commit or redistribute RimWorld or Harmony assemblies.
