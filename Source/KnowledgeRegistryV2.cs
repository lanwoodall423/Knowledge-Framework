using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    public static class KnowledgeRegistry
    {
        private sealed class SubjectCacheEntry
        {
            public KnowledgeSubjectSnapshot value;
            public int revision;
            public int lastAccess;
        }

        private static readonly Dictionary<string, KnowledgeSchema> Schemas = new Dictionary<string, KnowledgeSchema>(StringComparer.Ordinal);
        private static readonly Dictionary<string, KnowledgeSubjectSnapshot> StaticSubjects = new Dictionary<string, KnowledgeSubjectSnapshot>(StringComparer.Ordinal);
        private static readonly Dictionary<string, SubjectCacheEntry> DynamicSubjects = new Dictionary<string, SubjectCacheEntry>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> DomainAliases = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> SubjectAliases = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly List<KnowledgeValidationIssue> Issues = new List<KnowledgeValidationIssue>();
        private static readonly HashSet<string> IssueKeys = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<KnowledgeInsightDef>> InsightsByDependency = new Dictionary<string, List<KnowledgeInsightDef>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<KnowledgeRelationshipDef>> RelationshipsByTarget = new Dictionary<string, List<KnowledgeRelationshipDef>>(StringComparer.Ordinal);
        private static IReadOnlyCollection<KnowledgeSchema> schemaSnapshot = Array.Empty<KnowledgeSchema>();
        private static bool defsBuilt;
        private static int revision;

        public static int Revision => revision;
        public static IReadOnlyCollection<KnowledgeSchema> SchemasSnapshot => schemaSnapshot;
        public static IReadOnlyList<KnowledgeValidationIssue> ValidationIssues => new ReadOnlyCollection<KnowledgeValidationIssue>(Issues.ToList());

        public static void ClearDiagnostics(string ownerId = null)
        {
            if (ownerId.NullOrEmpty())
            {
                Issues.Clear();
                IssueKeys.Clear();
                return;
            }
            Issues.RemoveAll(issue => issue.ownerId == ownerId || !issue.ownerId.NullOrEmpty() && issue.ownerId.StartsWith(ownerId + "/", StringComparison.Ordinal));
            IssueKeys.Clear();
            foreach (KnowledgeValidationIssue issue in Issues) IssueKeys.Add(IssueKey(issue));
        }

        public static void BuildDefSchemas()
        {
            if (defsBuilt) return;
            Stopwatch stopwatch = Stopwatch.StartNew();
            defsBuilt = true;
            foreach (KnowledgeDomainDef def in DefDatabase<KnowledgeDomainDef>.AllDefsListForReading)
                RegisterSchema(KnowledgeSchema.FromDef(def), new KnowledgeRegistrationOptions
                {
                    source = def.modContentPack?.PackageId ?? "Defs",
                    conflict = KnowledgeRegistrationConflict.Reject
                });
            foreach (KnowledgeSubjectDef subject in DefDatabase<KnowledgeSubjectDef>.AllDefsListForReading)
                RegisterStaticSubject(subject);
            RebuildDependencyIndexes();
            KnowledgeDiagnostics.RecordSchemaBuild(stopwatch.ElapsedTicks, Schemas.Count, StaticSubjects.Count);
        }

        public static bool RegisterDomain(KnowledgeDomainRegistration registration, KnowledgeRegistrationOptions options = null)
        {
            options = options ?? new KnowledgeRegistrationOptions();
            if (registration == null || !ValidId(registration.id))
            {
                AddIssue("registration.domain.id", registration?.id ?? "<null>", "A domain requires a stable non-whitespace ID.");
                return false;
            }
            Stopwatch stopwatch = Stopwatch.StartNew();
            KnowledgeSchema schema;
            try { schema = new KnowledgeSchema(registration, options.priority, options.source); }
            catch (Exception exception)
            {
                AddIssue("registration.domain.exception", registration.id, "Schema construction failed safely.");
                KnowledgeLog.ErrorOnce("registration-domain:" + registration.id, "Domain schema construction failed.", exception);
                return false;
            }
            bool result = RegisterSchema(schema, options);
            if (result)
            {
                RebuildDependencyIndexes();
                KnowledgeDiagnostics.RecordRegistration(stopwatch.ElapsedTicks);
            }
            return result;
        }

        public static bool RegisterSubject(string domainId, KnowledgeSubjectRegistration subject,
            KnowledgeRegistrationOptions options = null)
        {
            domainId = ResolveDomainId(domainId);
            if (Schema(domainId) == null || subject == null || !ValidId(subject.id))
            {
                AddIssue("registration.subject", domainId + "/" + (subject?.id ?? "<null>"), "Subject registration references an unknown domain or invalid ID.");
                return false;
            }
            string key = SubjectKey(domainId, subject.id);
            if (StaticSubjects.ContainsKey(key) && (options?.conflict ?? KnowledgeRegistrationConflict.Reject) == KnowledgeRegistrationConflict.Reject)
            {
                AddIssue("registration.subject.duplicate", key, "Duplicate subject registration was rejected.");
                return false;
            }
            StaticSubjects[key] = new KnowledgeSubjectSnapshot(domainId, subject);
            DynamicSubjects.Remove(key);
            revision++;
            KnowledgeUiCache.Reset();
            return true;
        }

        public static bool UnregisterSubject(string domainId, string subjectId, string source = null)
        {
            string key = SubjectKey(ResolveDomainId(domainId), ResolveSubjectId(domainId, subjectId));
            if (key == null || !StaticSubjects.TryGetValue(key, out KnowledgeSubjectSnapshot subject) || !subject.dynamic) return false;
            StaticSubjects.Remove(key);
            DynamicSubjects.Remove(key);
            revision++;
            KnowledgeUiCache.Reset();
            return true;
        }

        public static bool UnregisterDomain(string domainId, string source = null)
        {
            domainId = ResolveDomainId(domainId);
            if (!Schemas.TryGetValue(domainId, out KnowledgeSchema schema) || (!source.NullOrEmpty() && schema.source != source)) return false;
            Schemas.Remove(domainId);
            string prefix = domainId + "\n";
            foreach (string key in StaticSubjects.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList()) StaticSubjects.Remove(key);
            foreach (string key in DynamicSubjects.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList()) DynamicSubjects.Remove(key);
            KnowledgeProviderRegistry.Unregister(domainId);
            KnowledgeV2Ui.Unregister(domainId);
            KnowledgeEffects.Unregister(domainId);
            revision++;
            RebuildDependencyIndexes();
            ResetGameCaches();
            return true;
        }

        public static bool RegisterDomainAlias(string oldId, string currentId)
        {
            if (!ValidId(oldId) || !ValidId(currentId) || oldId == currentId) return false;
            DomainAliases[oldId] = currentId;
            if (ResolveDomainId(oldId) == null)
            {
                DomainAliases.Remove(oldId);
                return false;
            }
            GameComponent_KnowledgeFramework.Current?.MigrateAliasesV2();
            revision++;
            return true;
        }

        public static bool RegisterSubjectAlias(string domainId, string oldId, string currentId)
        {
            if (!ValidId(domainId) || !ValidId(oldId) || !ValidId(currentId) || oldId == currentId) return false;
            string key = SubjectKey(domainId, oldId);
            SubjectAliases[key] = currentId;
            if (ResolveSubjectId(domainId, oldId) == null)
            {
                SubjectAliases.Remove(key);
                return false;
            }
            GameComponent_KnowledgeFramework.Current?.MigrateAliasesV2();
            revision++;
            return true;
        }

        public static KnowledgeSchema Schema(string domainId)
        {
            domainId = ResolveDomainId(domainId);
            return domainId != null && Schemas.TryGetValue(domainId, out KnowledgeSchema value) ? value : null;
        }

        public static KnowledgeSubjectSnapshot ResolveSubject(string domainId, string subjectId)
        {
            domainId = ResolveDomainId(domainId);
            subjectId = ResolveSubjectId(domainId, subjectId);
            string key = SubjectKey(domainId, subjectId);
            if (key == null) return null;
            if (StaticSubjects.TryGetValue(key, out KnowledgeSubjectSnapshot value)) return value;
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (DynamicSubjects.TryGetValue(key, out SubjectCacheEntry cached) && cached.revision == revision)
            {
                cached.lastAccess = tick;
                KnowledgeDiagnostics.CacheHit();
                return cached.value;
            }
            KnowledgeDiagnostics.CacheMiss();
            KnowledgeSchema schema = Schema(domainId);
            if (schema == null) return null;
            KnowledgeSubjectRegistration resolved = SafeConsumerCall("subject resolver", domainId, () => schema.ResolveDynamic(subjectId));
            if (resolved != null && (!ValidId(resolved.id) || resolved.id != subjectId))
            {
                AddIssue("subject.resolver.id", domainId + "/" + subjectId, "A dynamic subject resolver returned a mismatched stable ID.");
                resolved = null;
            }
            DynamicSubjects[key] = new SubjectCacheEntry
            {
                value = resolved == null ? null : new KnowledgeSubjectSnapshot(domainId, resolved),
                revision = revision,
                lastAccess = tick
            };
            TrimDynamicCache(tick);
            return DynamicSubjects[key].value;
        }

        public static IReadOnlyList<KnowledgeSubjectSnapshot> Subjects(string domainId)
        {
            domainId = ResolveDomainId(domainId);
            KnowledgeSchema schema = Schema(domainId);
            if (schema == null) return Array.Empty<KnowledgeSubjectSnapshot>();
            List<KnowledgeSubjectSnapshot> result = StaticSubjects.Where(pair => pair.Value.domainId == domainId).Select(pair => pair.Value).ToList();
            HashSet<string> subjectIds = new HashSet<string>(result.Select(item => item.id), StringComparer.Ordinal);
            IEnumerable<KnowledgeSubjectRegistration> dynamic = SafeConsumerCall("subject source", domainId, () => schema.DynamicSubjects().ToList())
                ?? Enumerable.Empty<KnowledgeSubjectRegistration>();
            foreach (KnowledgeSubjectRegistration subject in dynamic)
                if (subject != null && ValidId(subject.id) && subjectIds.Add(subject.id))
                    result.Add(new KnowledgeSubjectSnapshot(domainId, subject));
            return new ReadOnlyCollection<KnowledgeSubjectSnapshot>(result.OrderBy(item => item.sortOrder).ThenBy(item => item.label).ThenBy(item => item.id).ToList());
        }

        internal static IReadOnlyList<KnowledgeInsightDef> InsightsFor(string domainId, string facetId)
        {
            string key = DependencyKey(ResolveDomainId(domainId), facetId);
            return key != null && InsightsByDependency.TryGetValue(key, out List<KnowledgeInsightDef> result) ? result : Array.Empty<KnowledgeInsightDef>();
        }

        internal static IReadOnlyList<KnowledgeRelationshipDef> RelationshipsTo(string domainId, string subjectId, string facetId)
        {
            domainId = ResolveDomainId(domainId);
            subjectId = ResolveSubjectId(domainId, subjectId);
            string requestedFacet = facetId.NullOrEmpty() ? KnowledgeSchema.DefaultFacetId : facetId;
            string key = RelationshipKey(domainId, subjectId, requestedFacet);
            List<KnowledgeRelationshipDef> result = new List<KnowledgeRelationshipDef>();
            if (key != null && RelationshipsByTarget.TryGetValue(key, out List<KnowledgeRelationshipDef> exact)) result.AddRange(exact);
            if (requestedFacet != KnowledgeSchema.DefaultFacetId)
            {
                string genericKey = RelationshipKey(domainId, subjectId, KnowledgeSchema.DefaultFacetId);
                if (genericKey != null && RelationshipsByTarget.TryGetValue(genericKey, out List<KnowledgeRelationshipDef> generic)) result.AddRange(generic);
            }
            return result.Count == 0 ? (IReadOnlyList<KnowledgeRelationshipDef>)Array.Empty<KnowledgeRelationshipDef>() :
                new ReadOnlyCollection<KnowledgeRelationshipDef>(result.Distinct().ToList());
        }

        internal static string ResolveDomainId(string domainId)
        {
            if (domainId == null) return null;
            HashSet<string> seen = null;
            while (DomainAliases.TryGetValue(domainId, out string next))
            {
                if (seen == null) seen = new HashSet<string>(StringComparer.Ordinal);
                if (!seen.Add(domainId)) return null;
                domainId = next;
            }
            return domainId;
        }

        internal static string ResolveSubjectId(string domainId, string subjectId)
        {
            if (subjectId == null) return null;
            domainId = ResolveDomainId(domainId);
            string current = subjectId;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            while (seen.Add(current))
            {
                string key = SubjectKey(domainId, current);
                if (key == null || !SubjectAliases.TryGetValue(key, out string next)) return current;
                current = next;
            }
            return null;
        }

        internal static void ResetGameCaches()
        {
            DynamicSubjects.Clear();
            KnowledgeProviderRegistry.InvalidateAll();
            KnowledgeUiCache.Reset();
            KnowledgeBioPanel.ResetGameState();
            KnowledgeMenuUI.ResetGameState();
        }

        public static void InvalidateSubjects(string domainId = null)
        {
            if (domainId.NullOrEmpty()) DynamicSubjects.Clear();
            else
            {
                string prefix = ResolveDomainId(domainId) + "\n";
                foreach (string key in DynamicSubjects.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList()) DynamicSubjects.Remove(key);
            }
            revision++;
            RebuildSchemaSnapshot();
            KnowledgeUiCache.Reset();
        }

        private static bool RegisterSchema(KnowledgeSchema schema, KnowledgeRegistrationOptions options)
        {
            if (!ValidateSchema(schema)) return false;
            if (Schemas.TryGetValue(schema.id, out KnowledgeSchema existing))
            {
                bool replace = options.conflict == KnowledgeRegistrationConflict.Replace ||
                    options.conflict == KnowledgeRegistrationConflict.ReplaceIfHigherPriority && options.priority > existing.priority;
                if (!replace)
                {
                    AddIssue("registration.domain.duplicate", schema.id, "Duplicate domain from '" + schema.source + "' was rejected; existing source is '" + existing.source + "'.");
                    return false;
                }
            }
            Schemas[schema.id] = schema;
            KnowledgeV2Ui.EnsureBioProvider(schema);
            revision++;
            RebuildSchemaSnapshot();
            return true;
        }

        private static void RegisterStaticSubject(KnowledgeSubjectDef subject)
        {
            string domainId = ResolveDomainId(subject.domainId);
            if (Schema(domainId) == null || !ValidId(subject.StableId))
            {
                AddIssue("definition.subject", subject.defName, "Static subject references an unknown domain or invalid stable ID.");
                return;
            }
            string key = SubjectKey(domainId, subject.StableId);
            if (StaticSubjects.ContainsKey(key))
            {
                AddIssue("definition.subject.duplicate", key, "Duplicate static subject was rejected.");
                return;
            }
            StaticSubjects.Add(key, new KnowledgeSubjectSnapshot(domainId, subject));
        }

        private static bool ValidateSchema(KnowledgeSchema schema)
        {
            bool valid = true;
            if (schema.facets.Any(item => !ValidId(item.id)) || schema.facets.GroupBy(item => item.id).Any(group => group.Count() > 1))
            {
                AddIssue("schema.facet.id", schema.id, "Facet stable IDs must be valid and unique.");
                valid = false;
            }
            if (schema.stages.Any(item => !ValidId(item.id)) || schema.stages.GroupBy(item => item.id).Any(group => group.Count() > 1) || schema.stages.Zip(schema.stages.Skip(1), (a, b) => a.order >= b.order).Any(value => value))
            {
                AddIssue("schema.stage.order", schema.id, "Discovery stage IDs and order values must be strictly increasing.");
                valid = false;
            }
            if (schema.expertiseTracks.Any(item => !ValidId(item.id)) || schema.expertiseTracks.GroupBy(item => item.id).Any(group => group.Count() > 1) || schema.expertiseTracks.Any(item => !item.ranks.IsValid))
            {
                AddIssue("schema.expertise", schema.id, "Expertise track IDs must be unique and thresholds strictly increasing.");
                valid = false;
            }
            foreach (KnowledgeObservationDef observation in schema.observations)
            {
                if (observation == null || !ValidId(observation.StableId) || !KnowledgeMath.IsFinite(observation.baseKnowledge) || observation.baseKnowledge < 0f ||
                    !KnowledgeMath.IsFinite(observation.baseExpertise) || observation.baseExpertise < 0f ||
                    !KnowledgeMath.IsFinite(observation.baseFamiliarity) || observation.baseFamiliarity < 0f ||
                    !KnowledgeMath.IsFinite(observation.failureKnowledgeFactor) || observation.failureKnowledgeFactor < 0f ||
                    !KnowledgeMath.IsFinite(observation.failureExpertiseFactor) || observation.failureExpertiseFactor < 0f)
                {
                    AddIssue("schema.observation.values", observation?.defName ?? schema.id, "Observation values must be finite and non-negative.");
                    valid = false;
                }
            }
            if (schema.observations.Any(item => !ValidId(item.StableId)) || schema.observations.GroupBy(item => item.StableId).Any(group => group.Count() > 1))
            {
                AddIssue("schema.observation.id", schema.id, "Observation stable IDs must be valid and unique.");
                valid = false;
            }
            foreach (KnowledgeRevealDef reveal in schema.reveals)
            {
                if (reveal == null || !KnowledgeMath.IsFinite(reveal.minimumKnowledge) || reveal.minimumKnowledge < 0f ||
                    !KnowledgeMath.IsFinite(reveal.minimumConfidence) || reveal.minimumConfidence < 0f || reveal.minimumConfidence > 1f ||
                    !reveal.facetId.NullOrEmpty() && schema.Facet(reveal.facetId) == null ||
                    !reveal.minimumStageId.NullOrEmpty() && schema.Stage(reveal.minimumStageId) == null)
                {
                    AddIssue("schema.reveal.values", reveal?.defName ?? schema.id, "Reveal thresholds must be finite and valid.");
                    valid = false;
                }
            }
            foreach (KnowledgeEffectDef effect in schema.effects)
            {
                if (effect == null || !KnowledgeMath.IsFinite(effect.value) || !KnowledgeMath.IsFinite(effect.minimumKnowledge) ||
                    !KnowledgeMath.IsFinite(effect.minimumConfidence) || effect.minimumKnowledge < 0f || effect.minimumConfidence < 0f || effect.minimumConfidence > 1f ||
                    !effect.facetId.NullOrEmpty() && schema.Facet(effect.facetId) == null ||
                    !effect.minimumStageId.NullOrEmpty() && schema.Stage(effect.minimumStageId) == null)
                {
                    AddIssue("schema.effect.values", effect?.defName ?? schema.id, "Effect values and thresholds must be finite and valid.");
                    valid = false;
                }
            }
            foreach (KnowledgeInsightDef insight in schema.insights)
            {
                foreach (KnowledgeInsightRequirement requirement in insight?.requirements ?? Enumerable.Empty<KnowledgeInsightRequirement>())
                {
                    if (requirement == null || !KnowledgeMath.IsFinite(requirement.minimum) || requirement.minimum < 0f)
                    {
                        AddIssue("schema.insight.requirement", insight?.defName ?? schema.id, "Insight requirements must have finite non-negative thresholds.");
                        valid = false;
                    }
                }
                foreach (KnowledgeInsightOutcome outcome in insight?.outcomes ?? Enumerable.Empty<KnowledgeInsightOutcome>())
                {
                    if (outcome == null || !KnowledgeMath.IsFinite(outcome.knowledge) || outcome.knowledge < 0f ||
                        !KnowledgeMath.IsFinite(outcome.familiarity) || outcome.familiarity < 0f ||
                        !KnowledgeMath.IsFinite(outcome.expertise) || outcome.expertise < 0f ||
                        !outcome.facetId.NullOrEmpty() && schema.Facet(outcome.facetId) == null ||
                        !outcome.stageId.NullOrEmpty() && schema.Stage(outcome.stageId) == null ||
                        !outcome.expertiseTrackId.NullOrEmpty() && schema.ExpertiseTrack(outcome.expertiseTrackId) == null)
                    {
                        AddIssue("schema.insight.outcome", insight?.defName ?? schema.id, "Insight outcomes must use valid finite values and references.");
                        valid = false;
                    }
                }
            }
            if (schema.insights.Any(item => !ValidId(item.defName)) || schema.insights.GroupBy(item => item.defName).Any(group => group.Count() > 1))
            {
                AddIssue("schema.insight.id", schema.id, "Insight IDs must be valid and unique.");
                valid = false;
            }
            foreach (KnowledgeRelationshipDef relationship in schema.relationships)
            {
                if (relationship.domainId.NullOrEmpty()) relationship.domainId = schema.id;
                if (relationship.domainId != schema.id || !ValidId(relationship.fromSubjectId) || !ValidId(relationship.toSubjectId) ||
                    !relationship.facetId.NullOrEmpty() && schema.Facet(relationship.facetId) == null ||
                    !KnowledgeMath.IsFinite(relationship.coefficient) || relationship.coefficient < 0f || relationship.coefficient > 1f ||
                    !KnowledgeMath.IsFinite(relationship.confidenceCoefficient) || relationship.confidenceCoefficient < 0f || relationship.confidenceCoefficient > 1f)
                {
                    AddIssue("schema.relationship.coefficient", relationship.defName, "Relationship coefficients must be finite values from zero to one.");
                    valid = false;
                }
            }
            if (HasRelationshipCycle(schema))
            {
                AddIssue("schema.relationship.cycle", schema.id, "Relationship cycles are rejected to prevent amplification.");
                valid = false;
            }
            if (HasInsightCycle(schema))
            {
                AddIssue("schema.insight.cycle", schema.id, "Insight dependencies are cyclic.");
                valid = false;
            }
            if (schema.transmission != null &&
                (!KnowledgeMath.IsFinite(schema.transmission.knowledgeEfficiency) || schema.transmission.knowledgeEfficiency < 0f || schema.transmission.knowledgeEfficiency > 1f ||
                 !KnowledgeMath.IsFinite(schema.transmission.confidenceEfficiency) || schema.transmission.confidenceEfficiency < 0f || schema.transmission.confidenceEfficiency > 1f))
            {
                AddIssue("schema.transmission.values", schema.id, "Transmission efficiencies must be finite values from zero to one.");
                valid = false;
            }
            return valid;
        }

        private static bool HasRelationshipCycle(KnowledgeSchema schema)
        {
            return KnowledgeGraphValidation.HasCycle(schema.relationships.Where(item => item != null)
                .Select(item => new KeyValuePair<string, string>(item.fromSubjectId, item.toSubjectId)));
        }

        private static bool HasInsightCycle(KnowledgeSchema schema)
        {
            return KnowledgeGraphValidation.HasCycle((schema.insights ?? Array.Empty<KnowledgeInsightDef>()).SelectMany(insight =>
                (insight.requirements ?? new List<KnowledgeInsightRequirement>()).Where(requirement => requirement?.kind == KnowledgeRequirementKind.Insight &&
                    !requirement.insightId.NullOrEmpty()).Select(requirement => new KeyValuePair<string, string>(insight.defName, requirement.insightId))));
        }

        private static void RebuildDependencyIndexes()
        {
            InsightsByDependency.Clear();
            RelationshipsByTarget.Clear();
            foreach (KnowledgeSchema schema in Schemas.Values)
            {
                foreach (KnowledgeInsightDef insight in schema.insights)
                {
                    if (insight.domainId.NullOrEmpty()) insight.domainId = schema.id;
                    IEnumerable<string> facets = insight.requirements?.Select(item => item?.facetId).Where(item => !item.NullOrEmpty()).Distinct()
                        ?? Enumerable.Empty<string>();
                    if (!facets.Any()) facets = schema.facets.Select(item => item.id);
                    foreach (string facet in facets) AddTo(InsightsByDependency, DependencyKey(schema.id, facet), insight);
                }
                foreach (KnowledgeRelationshipDef relation in schema.relationships)
                    AddTo(RelationshipsByTarget, RelationshipKey(schema.id, relation.toSubjectId, relation.facetId), relation);
            }
        }

        private static void AddTo<T>(Dictionary<string, List<T>> index, string key, T value)
        {
            if (key == null || value == null) return;
            if (!index.TryGetValue(key, out List<T> list)) index.Add(key, list = new List<T>());
            list.Add(value);
        }

        private static T SafeConsumerCall<T>(string operation, string owner, Func<T> action)
        {
            try { return action == null ? default(T) : action(); }
            catch (Exception exception)
            {
                KnowledgeLog.ErrorOnce(operation + ":" + owner, "Consumer " + operation + " failed for '" + owner + "'.", exception);
                return default(T);
            }
        }

        private static void TrimDynamicCache(int tick)
        {
            const int limit = 2048;
            if (DynamicSubjects.Count <= limit) return;
            foreach (string key in DynamicSubjects.OrderBy(pair => pair.Value.lastAccess).Take(DynamicSubjects.Count - limit).Select(pair => pair.Key).ToList())
                DynamicSubjects.Remove(key);
        }

        private static void AddIssue(string code, string owner, string message)
        {
            KnowledgeValidationIssue issue = new KnowledgeValidationIssue(code, owner, message);
            if (IssueKeys.Add(IssueKey(issue))) Issues.Add(issue);
        }
        private static string IssueKey(KnowledgeValidationIssue issue) => issue.code + "\n" + issue.ownerId + "\n" + issue.message;
        private static void RebuildSchemaSnapshot() => schemaSnapshot = new ReadOnlyCollection<KnowledgeSchema>(
            Schemas.Values.OrderBy(item => item.sortOrder).ThenBy(item => item.id).ToList());
        private static bool ValidId(string value) => !value.NullOrEmpty() && value.Trim() == value && value.IndexOf('\n') < 0;
        private static string SubjectKey(string domainId, string subjectId) => ValidId(domainId) && ValidId(subjectId) ? domainId + "\n" + subjectId : null;
        private static string DependencyKey(string domainId, string facetId) => ValidId(domainId) ? domainId + "\n" + (facetId.NullOrEmpty() ? KnowledgeSchema.DefaultFacetId : facetId) : null;
        private static string RelationshipKey(string domainId, string subjectId, string facetId) =>
            ValidId(domainId) && ValidId(subjectId) ? domainId + "\n" + subjectId + "\n" + (facetId.NullOrEmpty() ? KnowledgeSchema.DefaultFacetId : facetId) : null;
    }

    internal static class KnowledgeLog
    {
        public static void ErrorOnce(string key, string message, Exception exception)
        {
            Log.ErrorOnce("[Knowledge Framework] " + message + "\n" + exception,
                GenText.StableStringHash("KnowledgeFrameworkV2/" + key));
        }
    }
}
