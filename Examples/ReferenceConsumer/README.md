# Knowledge Framework Reference Consumer

This is a deliberately small, separate RimWorld 1.6 consumer mod. It is
executable documentation and a compatibility fixture, not part of the
Knowledge Framework package. The framework root `LoadFolders.xml` does not
load anything under `Examples`; install this folder as its own mod when
running the walkthrough.

The example has no bundled Knowledge Framework, Harmony, RimWorld, or Unity
assemblies. Its project accepts local dependency paths only.

## Installation

1. Build or obtain the framework's `1.6/Assemblies/KnowledgeFramework.dll`.
2. Build this project against a legitimate local RimWorld 1.6 `Managed`
   directory using the commands below.
3. Copy or symlink this `Examples/ReferenceConsumer` directory into the
   RimWorld `Mods` directory as a separate mod.
4. Enable Knowledge Framework before Knowledge Framework Reference Consumer.

PowerShell 5.1 or 7 on Windows:

```powershell
$env:RIMWORLD_MANAGED_PATH = 'D:\Games\RimWorld\RimWorldWin64_Data\Managed'
$env:KNOWLEDGEFRAMEWORK_ASSEMBLY_PATH = (Resolve-Path '.\1.6\Assemblies\KnowledgeFramework.dll').Path
dotnet build .\Examples\ReferenceConsumer\ReferenceConsumer.csproj -c Release
```

PowerShell 7 on Windows or Linux:

```powershell
$env:RIMWORLD_MANAGED_PATH = '/path/to/RimWorldWin64_Data/Managed'
$env:KNOWLEDGEFRAMEWORK_ASSEMBLY_PATH = (Resolve-Path './1.6/Assemblies/KnowledgeFramework.dll').Path
dotnet build ./Examples/ReferenceConsumer/ReferenceConsumer.csproj -c Release
```

The same values may be supplied through `RIMWORLD_MANAGED_PATH` and
`KNOWLEDGEFRAMEWORK_ASSEMBLY_PATH`. Missing local files produce a clear
`BLOCKED` build error. No Harmony reference is needed by this consumer.

## Walkthrough

`ReferenceConsumer_Knowledge.xml` declares the static
`reference.field-guide` domain, two facets, balanced discovery stages, a
typed Float claim, an observation recipe, a milestone, expertise, a typed
effect channel, a context type, and a structural relation type. All Def and
UI IDs have English localization entries.

`ReferenceConsumerRuntime.Initialize` calls `KnowledgeConsumerApi.PrepareRegistration`
and registers a consumer-owned runtime domain through `RegisterDomain`. It never
constructs global schemas or replaces a foreign registration. It then registers
`observed-specimen` at runtime. If the framework, Def, or optional content is
absent, initialization returns without breaking the game.

The public methods demonstrate the contract:

- `Observe(pawn)` submits a personal `KnowledgeObservation` through
  `KnowledgeEngine`, with contextual typed temperature evidence and expertise.
- `ReportAndDocument(pawn)` exercises personal-to-colony reporting and
  documentation.
- `ConfirmMilestone(pawn)` and `Milestone(pawn)` exercise progression.
- `AddStructuralRelation()` inserts a relation between the dynamic and static
  subjects.
- `QueryTypedEffect(pawn)` queries the typed gameplay-effect channel.
- `OpenGuide(pawn)` opens the minimal `KnowledgeV2Ui.Open` deep link.
- `MigrateConsumer(pawn)` imports version 1 of the consumer migration.
- `IsCompatible`, `Release`, `Readiness`, `RuntimeDomainOwnership`, and
  `OptionalInsight` show capability/version checks and safe optional-content
  probing.
- `InvalidateObservedSpecimen()` demonstrates targeted invalidation after a
  consumer-owned subject or relation changes.

The supported lifecycle is: check `KnowledgeConsumerApi.Readiness`, call
`PrepareRegistration()` once or repeatedly until it returns `Ready`, inspect
ownership, then call `RegisterDomain()`. A no-game or missing-framework status is
temporary; an initialization failure disables optional content for that lifecycle.
Consumers never inspect `GameComponent_KnowledgeFramework.Current` and never call
`KnowledgeRegistry.BuildDefSchemas()`.

The framework owns persistence. Its migration record and V3 records remain in
the save if this consumer is removed, and the same versioned migration is
idempotent when the consumer is restored. A restored consumer must still use
the same stable IDs; it should not reinterpret old data when optional Defs are
missing.

## Verification

Run the static fixture check from the repository root:

```powershell
pwsh ./Examples/ReferenceConsumer/Verify-ReferenceConsumer.ps1
```

It validates XML, package isolation, stable IDs, localization coverage,
configurable paths, required API examples, and the absence of bundled or
absolute dependency paths. It does not claim that RimWorld runtime behavior
was tested.

## Smoke test

- Enable both mods with Knowledge Framework first.
- Confirm the reference domain and localized labels load without errors.
- Confirm `observed-specimen` appears after consumer initialization.
- Observe a pawn in the `reference.region:north-field` context.
- Query the contextual `temperature` claim and balanced discovery stage.
- Report, then document, the subject to the colony.
- Confirm the `field-guide/documented` milestone.
- Add and query the `reference.observed-from` relation.
- Query the `reference.field-guide.insight` typed effect.
- Open the subject through `OpenGuide` or an equivalent consumer button.
- Run the version/capability check and import migration version 1.
- Save, disable the consumer, reload, restore it, and verify the migration does
  not apply twice.
- Repeat with the optional insight/other content absent; the game must remain
  usable and the consumer must report the feature as unavailable.
