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
- Run the development stress action in a disposable test save and archive its
  report separately from player saves.
- Check XML, translations, internal documentation links, and unintended packaged
  artifacts.
- Report PASS, FAIL, and BLOCKED/UNAVAILABLE checks separately.
- Do not commit or redistribute RimWorld or Harmony assemblies.
