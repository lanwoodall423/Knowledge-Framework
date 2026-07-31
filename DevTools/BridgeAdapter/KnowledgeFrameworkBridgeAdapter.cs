using System;
using System.Collections.Generic;
using System.Linq;
using KnowledgeFramework;
using Verse;

namespace KnowledgeFrameworkBridgeAdapter
{
    public static class KnowledgeFrameworkBridgeAdapter
    {
        public static string[] BridgeCommandSpecs() => new[]
        {
            "KF_V2_STATE|R|Inspect registered schemas and selected pawn state",
            "KF_V2_VALIDATE|R|Inspect validation issues and runtime diagnostics",
            "KF_V2_VERIFY|W|Run the bounded V2 verification suite in the sandbox"
        };

        public static string BridgeAdapterInfo() =>
            "KnowledgeFramework|2.0.0|Bounded V2 schema, transaction, persistence, insight, relationship, and compatibility probes.";

        public static List<string> ExecuteBridgeCommand(string command, string argument, Map map)
        {
            switch ((command ?? string.Empty).ToUpperInvariant())
            {
                case "KF_V2_STATE": return State(map, argument);
                case "KF_V2_VALIDATE": return Validate();
                case "KF_V2_VERIFY": return Verify(map, argument);
                default: return null;
            }
        }

        private static List<string> State(Map map, string argument)
        {
            Pawn pawn = Pawn(map, argument);
            List<string> result = new List<string>
            {
                "apiVersion=" + KnowledgeFrameworkApi.ApiVersion,
                "schemas=" + KnowledgeRegistry.SchemasSnapshot.Count,
                "revision=" + KnowledgeRegistry.Revision,
                "pawn=" + (pawn?.thingIDNumber.ToString() ?? "none")
            };
            foreach (KnowledgeSchema schema in KnowledgeRegistry.SchemasSnapshot)
            {
                result.Add("schema=" + schema.id + " facets:" + schema.facets.Count + " stages:" + schema.stages.Count +
                    " tracks:" + schema.expertiseTracks.Count + " insights:" + schema.insights.Count +
                    " relationships:" + schema.relationships.Count);
                if (pawn == null) continue;
                KnowledgeExpertiseSnapshotV2 expertise = schema.expertiseTracks.Count == 0 ? null :
                    KnowledgeQuery.Expertise(schema.id, pawn, schema.expertiseTracks[0].id);
                if (expertise != null) result.Add("expertise=" + schema.id + "/" + expertise.trackId + ":" + expertise.amount.ToString("0.##") + "/" + expertise.rank);
                foreach (KnowledgeFacetSnapshotV2 facet in KnowledgeQuery.PersonalFacets(schema.id, pawn))
                    result.Add("facet=" + facet.domainId + "/" + facet.subjectId + "/" + facet.facetId +
                        " amount:" + facet.amount.ToString("0.##") + " confidence:" + facet.confidence.ToStringPercent() +
                        " evidence:" + facet.evidenceCount + " derived:" + facet.derivedAmount.ToString("0.##"));
            }
            return result;
        }

        private static List<string> Validate()
        {
            KnowledgeDiagnosticsSnapshot diagnostics = KnowledgeDiagnostics.Snapshot();
            IReadOnlyList<KnowledgeValidationIssue> issues = KnowledgeValidation.ValidateAll();
            List<string> result = new List<string>
            {
                "validationIssues=" + issues.Count,
                "orphanRecords=" + diagnostics.orphanRecords,
                "personalFacetRecords=" + diagnostics.personalFacetRecords,
                "colonyFacetRecords=" + diagnostics.colonyFacetRecords,
                "expertiseRecords=" + diagnostics.expertiseRecords,
                "persistentBytes=" + diagnostics.approximatePersistentBytes,
                "cacheHits=" + diagnostics.cacheHits + " cacheMisses=" + diagnostics.cacheMisses
            };
            result.AddRange(issues.Take(50).Select(issue => "issue=" + Clean(issue.ToString())));
            return result;
        }

        private static List<string> Verify(Map map, string argument)
        {
            KnowledgeVerificationResult result = KnowledgeFrameworkVerification.RunGameTests(Pawn(map, argument));
            List<string> lines = new List<string>
            {
                "passed=" + result.passed,
                "success=" + result.Success,
                "failureCount=" + result.failures.Count
            };
            lines.AddRange(result.failures.Select(failure => "failure=" + Clean(failure)));
            return lines;
        }

        private static Pawn Pawn(Map map, string argument)
        {
            if (map == null) return null;
            if (int.TryParse((argument ?? string.Empty).Trim(), out int thingId))
                return map.mapPawns.AllPawns.FirstOrDefault(pawn => pawn?.thingIDNumber == thingId);
            return map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
        }

        private static string Clean(string value) => (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
    }
}
