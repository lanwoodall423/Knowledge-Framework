using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using KnowledgeFramework;

internal static class PublicV3Audit
{
    private enum CoverageKind
    {
        Behavioral,
        Structural,
        Compatibility
    }

    private sealed class Coverage
    {
        internal readonly CoverageKind kind;
        internal readonly string layer;
        internal readonly string tests;

        internal Coverage(CoverageKind kind, string layer, string tests)
        {
            this.kind = kind;
            this.layer = layer;
            this.tests = tests;
        }
    }

    // This fingerprint is intentionally checked into the verifier. A new public declaration must
    // update this manifest and add a named production-path test mapping before the audit passes.
    private const string ExpectedDeclarationFingerprintHash = "JWuftClJ3oVLp+W8n6PSuLwB2mBVJtevbuEm1+gobaU=";

    private static readonly Dictionary<string, Coverage> TypeCoverage = BuildCoverage();

    internal static bool Run(Assembly assembly, KnowledgeVerificationResult pureResult)
    {
        bool valid = true;
        Type[] types = assembly.GetTypes().Where(IsKnowledgeType).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
        List<string> declarations = new List<string>();
        Dictionary<string, Type> declaredTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (Type type in types)
        {
            FieldInfo[] fields = type.IsEnum
                ? type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                : type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(property => property.GetMethod != null).ToArray();
            if (!type.IsEnum && fields.Length == 0 && properties.Length == 0) continue;
            declaredTypes[type.Name] = type;
            Coverage coverage;
            if (!TypeCoverage.TryGetValue(type.Name, out coverage))
            {
                valid = false;
                Console.Error.WriteLine("AUDIT FAIL declaration-type={0} expected=manifest classification actual=unmapped", type.Name);
                continue;
            }

            foreach (FieldInfo field in fields)
            {
                bool obsolete = field.GetCustomAttributes(typeof(ObsoleteAttribute), false).Length != 0;
                string declaration = type.Name + "." + field.Name;
                declarations.Add(declaration + (obsolete ? "#obsolete" : "#active"));
                if (obsolete)
                    Console.WriteLine("AUDIT declaration={0} status=obsolete-rejected mapping=validation-diagnostic layer=production-validation", declaration);
                else
                    PrintActive(declaration, coverage);
            }
            foreach (PropertyInfo property in properties)
            {
                bool obsolete = property.GetCustomAttributes(typeof(ObsoleteAttribute), false).Length != 0;
                string declaration = type.Name + "." + property.Name;
                declarations.Add(declaration + (obsolete ? "#obsolete" : "#active"));
                if (obsolete)
                    Console.WriteLine("AUDIT declaration={0} status=obsolete-rejected mapping=validation-diagnostic layer=production-validation", declaration);
                else
                    PrintActive(declaration, coverage);
            }
        }

        string fingerprint = string.Join("|", declarations.OrderBy(value => value, StringComparer.Ordinal));
        string fingerprintHash = Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(fingerprint)));
        if (string.IsNullOrEmpty(ExpectedDeclarationFingerprintHash))
        {
            valid = false;
            Console.Error.WriteLine("AUDIT FAIL declaration-fingerprint expected=checked-in manifest actualHash={0}", fingerprintHash);
        }
        else if (!string.Equals(ExpectedDeclarationFingerprintHash, fingerprintHash, StringComparison.Ordinal))
        {
            valid = false;
            Console.Error.WriteLine("AUDIT FAIL declaration-fingerprint changed expectedHash={0} actualHash={1}",
                ExpectedDeclarationFingerprintHash, fingerprintHash);
        }

        int active = declarations.Count(value => value.EndsWith("#active", StringComparison.Ordinal));
        int obsoleteCount = declarations.Count(value => value.EndsWith("#obsolete", StringComparison.Ordinal));
        int structural = 0;
        int compatibility = 0;
        foreach (Type type in declaredTypes.Values)
        {
            Coverage typeCoverage;
            if (!TypeCoverage.TryGetValue(type.Name, out typeCoverage)) continue;
            int memberCount = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length +
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Count(property => property.GetMethod != null);
            if (typeCoverage.kind == CoverageKind.Structural) structural += memberCount;
            if (typeCoverage.kind == CoverageKind.Compatibility) compatibility += memberCount;
        }
        int behavioral = Math.Max(0, active - structural - compatibility);
        Console.WriteLine("AUDIT summary active={0} behavioral={1} structural={2} compatibility={3} obsolete={4} manifest={5}",
            active, behavioral, structural, compatibility, obsoleteCount, valid ? "PASS" : "FAIL");
        return valid;
    }

    private static bool IsKnowledgeType(Type type)
    {
        return type.IsPublic && type.Namespace == "KnowledgeFramework" && type.Name.StartsWith("Knowledge", StringComparison.Ordinal);
    }

    private static void PrintActive(string declaration, Coverage coverage)
    {
        string status = coverage.kind == CoverageKind.Behavioral ? "supported-behavioral" :
            coverage.kind == CoverageKind.Structural ? "structural-data-only" : "compatibility-only";
        Console.WriteLine("AUDIT declaration={0} status={1} layer={2} tests={3}", declaration, status, coverage.layer, coverage.tests);
    }

    private static Dictionary<string, Coverage> BuildCoverage()
    {
        Dictionary<string, Coverage> result = new Dictionary<string, Coverage>(StringComparer.Ordinal);
        Add(result, CoverageKind.Behavioral, "pure", "claim aggregation/value tests; context-chain tests", 
            "KnowledgeClaimValue", "KnowledgeNumericRange", "KnowledgeContextKey", "KnowledgeStageSnapshot", "KnowledgeVerificationResult");
        Add(result, CoverageKind.Behavioral, "pure+game", "requirements, aggregation, fallback, and validation tests",
            "KnowledgeRequirement", "KnowledgeRequirementGroup", "KnowledgeStageDef", "KnowledgeFacetDef", "KnowledgeObservationDef",
            "KnowledgeClaimDef", "KnowledgeContextTypeDef", "KnowledgeAccrualPolicy", "KnowledgeWitnessDistribution");
        Add(result, CoverageKind.Behavioral, "game", "domain registration, lifecycle, observation, transmission, progression, and cleanup tests",
            "KnowledgeDomainDef", "KnowledgeDomainRegistration", "KnowledgeSubjectDef", "KnowledgeSubjectRegistration", "KnowledgeRegistrationOptions",
            "KnowledgeRevealDef", "KnowledgeEffectDef", "KnowledgeInsightRequirement", "KnowledgeInsightOutcome", "KnowledgeInsightDef",
            "KnowledgeRelationshipDef", "KnowledgeTransmissionDef", "KnowledgeSubjectArchetypeDef", "KnowledgeExpertiseTrackDef",
            "KnowledgeExpertiseOutcome", "KnowledgeObservationOutcome", "KnowledgeMeasurement", "KnowledgeMilestoneTrackDef",
            "KnowledgeMilestoneDef", "KnowledgeSubjectRelationTypeDef", "KnowledgeExpertiseNamespaceDef", "KnowledgeTransmissionRequest",
             "KnowledgeObservation", "KnowledgeTransaction", "KnowledgeBrowserFilter", "KnowledgeFrameworkSettings");
        Add(result, CoverageKind.Behavioral, "game", "browser context, visibility, fallback, and cache invalidation tests",
            "KnowledgeV3Ui");
        Add(result, CoverageKind.Behavioral, "pure+game", "validation, diagnostics, and unsupported-option rejection tests",
            "KnowledgeDiagnostics", "KnowledgeRegistry");
        Add(result, CoverageKind.Behavioral, "game", "domain registration, aliases, provider registration, and cleanup tests",
            "KnowledgeDomainRegistry", "KnowledgeFrameworkApi");
        Add(result, CoverageKind.Behavioral, "pure+game", "global/contextual query and compatibility query tests",
            "KnowledgeQuery");
        Add(result, CoverageKind.Compatibility, "pure+game", "rank and V1/V2 compatibility tests",
            "KnowledgeRankThresholds");
        Add(result, CoverageKind.Structural, "pure+game+save", "schema construction, query snapshots, persistence normalization, and index reconstruction tests",
            "KnowledgeFacetSchema", "KnowledgeStageSchema", "KnowledgeExpertiseTrackSchema", "KnowledgeSubjectSnapshot", "KnowledgeSchema",
            "KnowledgeClaimMeasurementSnapshot", "KnowledgeClaimSnapshot", "KnowledgeClaimChangedEvent", "KnowledgeSubjectUpdate",
            "KnowledgeSubjectRelation", "KnowledgeMilestoneState", "KnowledgeMilestoneChangedEvent", "KnowledgeMilestoneConditionSample",
            "KnowledgeSharedExpertiseSnapshot", "KnowledgeSharedExpertiseContribution", "KnowledgeComparisonRow", "KnowledgeComparisonSchema",
            "KnowledgeStructuredComparisonSnapshot", "KnowledgeFacetSnapshotV2", "KnowledgeEvidenceAggregateSnapshot", "KnowledgeProvenanceSnapshot",
            "KnowledgeSubjectSnapshotV2", "KnowledgeExpertiseSnapshotV2", "KnowledgeRelationshipSnapshot", "KnowledgeFacetComparison",
            "KnowledgeComparisonSnapshot", "KnowledgeChange", "KnowledgeTransactionResult", "KnowledgeBatchChangedEvent", "KnowledgeInsightContext",
            "KnowledgeInsightProgress", "KnowledgeEffectQuery", "KnowledgeEffectResult", "KnowledgeEffectAccumulator", "KnowledgeRevealResult",
            "KnowledgeDiagnosticsSnapshot", "KnowledgeMenuState", "KnowledgeMenuRow", "KnowledgeMenuSection", "KnowledgeMenuModel",
             "KnowledgeBrowserRow", "KnowledgeFrameworkMod", "KnowledgeConsumerMigration");
        Add(result, CoverageKind.Structural, "pure+game", "validation diagnostics and developer verification reporting tests",
            "KnowledgeValidationIssue");
        Add(result, CoverageKind.Compatibility, "pure+game", "V1/V2 compatibility and legacy save regression tests",
            "KnowledgeDomainDefinition", "KnowledgeSubjectDefinition", "KnowledgeRecord", "KnowledgeEntry", "KnowledgeSnapshot",
            "KnowledgeAward", "KnowledgeChangedEvent", "KnowledgeEffectContext", "KnowledgeKnowledgeRecord");
        AddEnum(result, "KnowledgeScope", "global/colony claim and stage tests");
        AddEnum(result, "KnowledgeSharingModel", "sharing/transmission tests");
        AddEnum(result, "KnowledgeStageAggregationMode", "LegacySumMax compatibility; balanced aggregation tests");
        AddEnum(result, "KnowledgeEvidenceDisposition", "supporting/neutral/contradictory claim tests");
        AddEnum(result, "KnowledgeEffectComposition", "V2 effect composition tests");
        AddEnum(result, "KnowledgeInsightScope", "insight scope tests");
        AddEnum(result, "KnowledgeRequirementKind", "requirement evaluation and obsolete-kind rejection tests");
        AddEnum(result, "KnowledgeRegistrationConflict", "registration priority/conflict tests");
        AddEnum(result, "KnowledgeClaimValueType", "typed claim value tests");
        AddEnum(result, "KnowledgeClaimAggregation", "claim aggregation tests");
        AddEnum(result, "KnowledgeClaimStalenessPolicy", "claim staleness production-path tests");
        AddEnum(result, "KnowledgeContextFallbackMode", "exact/parent/global context fallback tests");
        AddEnum(result, "KnowledgeRequirementGroupMode", "requirement group shape/evaluation tests");
        AddEnum(result, "KnowledgeRequirementComparison", "requirement comparison tests");
        AddEnum(result, "KnowledgeSubjectState", "lifecycle visibility tests");
        AddEnum(result, "KnowledgeWitnessDistributionPolicy", "witness distribution and obsolete-policy rejection tests");
        AddEnum(result, "KnowledgeMilestonePauseBehavior", "milestone pause/reset tests");
        AddEnum(result, "KnowledgeMilestoneResetBehavior", "milestone reset tests");
        AddEnum(result, "KnowledgeComparisonRowKind", "comparison row tests and obsolete-row rejection tests");
        AddEnum(result, "KnowledgeMilestoneEventKind", "milestone event tests");
        AddEnum(result, "KnowledgeStageProvenance", "stage provenance tests");
        AddEnum(result, "KnowledgeTransmissionKind", "transmission tests");
        AddEnum(result, "KnowledgeRank", "rank transition tests");
        AddEnum(result, "KnowledgeMenuScope", "menu scope tests");
        AddEnum(result, "KnowledgeBrowserSort", "browser sorting tests");
        return result;
    }

    private static void Add(Dictionary<string, Coverage> result, CoverageKind kind, string layer, string tests, params string[] names)
    {
        foreach (string name in names) result[name] = new Coverage(kind, layer, tests);
    }

    private static void AddEnum(Dictionary<string, Coverage> result, string name, string tests)
    {
        result[name] = new Coverage(CoverageKind.Behavioral, "pure+game", tests);
    }
}
