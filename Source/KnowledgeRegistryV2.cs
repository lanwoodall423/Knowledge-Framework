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

        private sealed class SubjectRegistrationMetadata
        {
            public int priority;
            public string source;
        }

        private static readonly Dictionary<string, KnowledgeSchema> Schemas = new Dictionary<string, KnowledgeSchema>(StringComparer.Ordinal);
        private static readonly Dictionary<string, KnowledgeSubjectSnapshot> StaticSubjects = new Dictionary<string, KnowledgeSubjectSnapshot>(StringComparer.Ordinal);
        private static readonly Dictionary<string, SubjectCacheEntry> DynamicSubjects = new Dictionary<string, SubjectCacheEntry>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> DomainAliases = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> SubjectAliases = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, KnowledgeSubjectRegistration> SubjectOverrides = new Dictionary<string, KnowledgeSubjectRegistration>(StringComparer.Ordinal);
        private static readonly Dictionary<string, SubjectRegistrationMetadata> SubjectRegistrationSources = new Dictionary<string, SubjectRegistrationMetadata>(StringComparer.Ordinal);
        private static readonly List<KnowledgeValidationIssue> Issues = new List<KnowledgeValidationIssue>();
        private static readonly HashSet<string> IssueKeys = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<KnowledgeInsightDef>> InsightsByDependency = new Dictionary<string, List<KnowledgeInsightDef>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<KnowledgeRelationshipDef>> RelationshipsByTarget = new Dictionary<string, List<KnowledgeRelationshipDef>>(StringComparer.Ordinal);
        private static IReadOnlyCollection<KnowledgeSchema> schemaSnapshot = Array.Empty<KnowledgeSchema>();
        private static bool defsBuilt;
        private static int revision;

        public static event Action<KnowledgeSubjectSnapshot> SubjectChanged;

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
            foreach (KnowledgeContextTypeDef context in DefDatabase<KnowledgeContextTypeDef>.AllDefsListForReading)
                KnowledgeContextRegistry.RegisterType(context, true);
            foreach (KnowledgeSubjectRelationTypeDef relationType in DefDatabase<KnowledgeSubjectRelationTypeDef>.AllDefsListForReading)
                KnowledgeRelationService.RegisterType(relationType, true);
            foreach (KnowledgeExpertiseNamespaceDef expertiseNamespace in DefDatabase<KnowledgeExpertiseNamespaceDef>.AllDefsListForReading)
                KnowledgeSharedExpertiseService.RegisterNamespace(expertiseNamespace, true);
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
                foreach (KnowledgeExpertiseNamespaceDef expertiseNamespace in registration.expertiseNamespaces ?? Array.Empty<KnowledgeExpertiseNamespaceDef>())
                    KnowledgeSharedExpertiseService.RegisterNamespace(expertiseNamespace, true);
                RebuildDependencyIndexes();
                KnowledgeDiagnostics.RecordRegistration(stopwatch.ElapsedTicks);
            }
            return result;
        }

        public static bool RegisterSubject(string domainId, KnowledgeSubjectRegistration subject,
            KnowledgeRegistrationOptions options = null)
        {
            options = options ?? new KnowledgeRegistrationOptions();
            domainId = ResolveDomainId(domainId);
            if (Schema(domainId) == null || subject == null || !ValidId(subject.id))
            {
                AddIssue("registration.subject", domainId + "/" + (subject?.id ?? "<null>"), "Subject registration references an unknown domain or invalid ID.");
                return false;
            }
            string key = SubjectKey(domainId, subject.id);
            if (StaticSubjects.ContainsKey(key) && options.conflict == KnowledgeRegistrationConflict.Reject)
            {
                AddIssue("registration.subject.duplicate", key, "Duplicate subject registration was rejected.");
                return false;
            }
            if (StaticSubjects.ContainsKey(key) && options.conflict == KnowledgeRegistrationConflict.ReplaceIfHigherPriority)
            {
                SubjectRegistrationMetadata existing = SubjectRegistrationSources.TryGetValue(key, out SubjectRegistrationMetadata value)
                    ? value : new SubjectRegistrationMetadata { priority = 0, source = "Defs" };
                if (options.priority <= existing.priority)
                {
                    AddIssue("registration.subject.priority", key, "Subject replacement was rejected because its priority was not higher than the existing registration.");
                    return false;
                }
            }
            StaticSubjects[key] = new KnowledgeSubjectSnapshot(domainId, subject);
            SubjectOverrides[key] = CloneSubject(subject);
            SubjectRegistrationSources[key] = new SubjectRegistrationMetadata
            {
                priority = options.priority,
                source = options.source ?? subject.source ?? "dynamic"
            };
            DynamicSubjects.Remove(key);
            revision++;
            KnowledgeUiCache.Reset();
            return true;
        }

        public static bool UnregisterSubject(string domainId, string subjectId, string source = null)
        {
            string key = SubjectKey(ResolveDomainId(domainId), ResolveSubjectId(domainId, subjectId));
            if (key == null || !StaticSubjects.TryGetValue(key, out KnowledgeSubjectSnapshot subject) || !subject.dynamic) return false;
            if (!source.NullOrEmpty() && (!SubjectRegistrationSources.TryGetValue(key, out SubjectRegistrationMetadata metadata) ||
                metadata.source != source)) return false;
            StaticSubjects.Remove(key);
            SubjectOverrides.Remove(key);
            SubjectRegistrationSources.Remove(key);
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
            foreach (string key in SubjectOverrides.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList()) SubjectOverrides.Remove(key);
            foreach (string key in SubjectRegistrationSources.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList()) SubjectRegistrationSources.Remove(key);
            foreach (string key in DomainAliases.Where(item => item.Key == domainId || ResolveDomainId(item.Value) == domainId)
                .Select(item => item.Key).ToList()) DomainAliases.Remove(key);
            foreach (string key in SubjectAliases.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList()) SubjectAliases.Remove(key);
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
            if (SubjectOverrides.TryGetValue(key, out KnowledgeSubjectRegistration overrideValue))
                return new KnowledgeSubjectSnapshot(domainId, overrideValue);
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
            if (resolved != null && SubjectOverrides.TryGetValue(key, out KnowledgeSubjectRegistration existingOverride))
                resolved = MergeSubject(existingOverride, resolved);
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
                    result.Add(new KnowledgeSubjectSnapshot(domainId, SubjectOverrides.TryGetValue(SubjectKey(domainId, subject.id), out KnowledgeSubjectRegistration value)
                        ? MergeSubject(value, subject) : subject));
            for (int i = 0; i < result.Count; i++)
                if (SubjectOverrides.TryGetValue(SubjectKey(domainId, result[i].id), out KnowledgeSubjectRegistration overrideValue))
                    result[i] = new KnowledgeSubjectSnapshot(domainId, overrideValue);
            return new ReadOnlyCollection<KnowledgeSubjectSnapshot>(result.OrderBy(item => item.sortOrder).ThenBy(item => item.label).ThenBy(item => item.id).ToList());
        }

        public static IReadOnlyList<KnowledgeFacetSchema> ApplicableFacets(string domainId, string subjectId)
        {
            KnowledgeSchema schema = Schema(domainId);
            KnowledgeSubjectSnapshot subject = ResolveSubject(domainId, subjectId);
            if (schema == null || subject == null) return Array.Empty<KnowledgeFacetSchema>();
            KnowledgeSubjectArchetypeDef archetype = schema.Archetype(subject.archetypeId);
            IEnumerable<string> explicitIds = subject.applicableFacetIds;
            if (explicitIds != null && explicitIds.Any()) return schema.facets.Where(item => explicitIds.Contains(item.id)).ToList();
            if (archetype?.applicableFacetIds != null && archetype.applicableFacetIds.Count > 0)
                return schema.facets.Where(item => archetype.applicableFacetIds.Contains(item.id)).ToList();
            return schema.facets;
        }

        public static IReadOnlyList<KnowledgeClaimDef> ApplicableClaims(string domainId, string subjectId, string facetId = null)
        {
            KnowledgeSchema schema = Schema(domainId);
            KnowledgeSubjectSnapshot subject = ResolveSubject(domainId, subjectId);
            KnowledgeFacetSchema facet = schema?.Facet(facetId);
            if (schema == null || subject == null || facet == null) return Array.Empty<KnowledgeClaimDef>();
            KnowledgeSubjectArchetypeDef archetype = schema.Archetype(subject.archetypeId);
            IEnumerable<string> ids = subject.applicableClaimIds?.Any() == true ? subject.applicableClaimIds : archetype?.applicableClaimIds;
            IEnumerable<KnowledgeClaimDef> claims = schema.claims.Where(claim => claim.facetId.NullOrEmpty() || claim.facetId == facet.id);
            if (ids != null && ids.Any()) claims = claims.Where(claim => ids.Contains(claim.StableId));
            if (facet.claimIds != null && facet.claimIds.Count > 0) claims = claims.Where(claim => facet.claimIds.Contains(claim.StableId));
            return claims.ToList();
        }

        public static bool UpdateSubject(string domainId, string subjectId, KnowledgeSubjectUpdate update)
        {
            domainId = ResolveDomainId(domainId);
            subjectId = ResolveSubjectId(domainId, subjectId);
            if (domainId.NullOrEmpty() || subjectId.NullOrEmpty() || update == null || Schema(domainId) == null) return false;
            KnowledgeSubjectSnapshot current = ResolveSubject(domainId, subjectId);
            if (current == null) return false;
            KnowledgeSubjectRegistration value = new KnowledgeSubjectRegistration
            {
                id = subjectId,
                label = update.label ?? current.label,
                description = update.description ?? current.description,
                unidentifiedLabel = update.unidentifiedLabel ?? current.unidentifiedLabel,
                unidentifiedDescription = update.unidentifiedDescription ?? current.unidentifiedDescription,
                archetypeId = update.archetypeId ?? current.archetypeId,
                categoryIds = update.categoryIds ?? current.categoryIds,
                applicableFacetIds = update.applicableFacetIds ?? current.applicableFacetIds,
                applicableClaimIds = update.applicableClaimIds ?? current.applicableClaimIds,
                templateSubjectId = update.templateSubjectId ?? current.templateSubjectId,
                sourceDef = update.sourceDef ?? current.sourceDef,
                iconPath = update.iconPath ?? current.iconPath,
                sortOrder = update.sortOrder ?? current.sortOrder,
                state = current.state,
                source = update.source ?? "lifecycle"
            };
            SubjectOverrides[SubjectKey(domainId, subjectId)] = value;
            DynamicSubjects.Remove(SubjectKey(domainId, subjectId));
            revision++;
            KnowledgeUiCache.Reset();
            KnowledgeV3PersistenceBridge.PersistSubjectOverride(domainId, value);
            InvokeSubjectChanged(new KnowledgeSubjectSnapshot(domainId, value));
            return true;
        }

        public static bool SetSubjectState(string domainId, string subjectId, KnowledgeSubjectState state)
        {
            KnowledgeSubjectSnapshot current = ResolveSubject(domainId, subjectId);
            if (current == null) return false;
            return UpdateSubject(domainId, subjectId, new KnowledgeSubjectUpdate
            {
                label = current.label,
                description = current.description,
                unidentifiedLabel = current.unidentifiedLabel,
                unidentifiedDescription = current.unidentifiedDescription,
                archetypeId = current.archetypeId,
                categoryIds = current.categoryIds,
                applicableFacetIds = current.applicableFacetIds,
                applicableClaimIds = current.applicableClaimIds,
                templateSubjectId = current.templateSubjectId,
                sourceDef = current.sourceDef,
                iconPath = current.iconPath,
                sortOrder = current.sortOrder,
                source = "lifecycle"
            }) && SetOverrideState(domainId, subjectId, state);
        }

        private static bool SetOverrideState(string domainId, string subjectId, KnowledgeSubjectState state)
        {
            string key = SubjectKey(ResolveDomainId(domainId), ResolveSubjectId(domainId, subjectId));
            if (key == null || !SubjectOverrides.TryGetValue(key, out KnowledgeSubjectRegistration value)) return false;
            value.state = state;
            DynamicSubjects.Remove(key);
            revision++;
            KnowledgeUiCache.Reset();
            KnowledgeV3PersistenceBridge.PersistSubjectOverride(domainId, value);
            InvokeSubjectChanged(new KnowledgeSubjectSnapshot(domainId, value));
            return true;
        }

        internal static void RestoreSubjectOverride(KnowledgeSubjectRegistration value, string domainId)
        {
            if (value == null || !ValidId(value.id)) return;
            string key = SubjectKey(ResolveDomainId(domainId), value.id);
            SubjectOverrides[key] = CloneSubject(value);
            if (!SubjectRegistrationSources.ContainsKey(key))
                SubjectRegistrationSources[key] = new SubjectRegistrationMetadata { priority = 0, source = value.source ?? "persisted" };
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
            KnowledgeDiagnostics.SubjectCacheInvalidated();
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
            SubjectRegistrationSources[key] = new SubjectRegistrationMetadata { priority = 0, source = "Defs" };
        }

        private static bool ValidateSchema(KnowledgeSchema schema)
        {
            bool valid = true;
            if (!Enum.IsDefined(typeof(KnowledgeStageAggregationMode), schema.stageAggregationMode))
            {
                AddIssue("schema.stage.aggregation", schema.id, "Stage aggregation mode is not defined; use LegacySumMax or Balanced.");
                valid = false;
            }
            if (schema.stageAggregationMode == KnowledgeStageAggregationMode.Balanced &&
                schema.stages.Any(item => item.minimumKnowledge > 100f))
            {
                AddIssue("schema.stage.aggregation.threshold", schema.id,
                    "Balanced stage minimumKnowledge values must be from zero through 100 because balanced knowledge is a normalized percentage.");
                valid = false;
            }
            foreach (KnowledgeStageSchema stage in schema.stages)
            {
                if (!KnowledgeMath.IsFinite(stage.minimumKnowledge) || stage.minimumKnowledge < 0f)
                {
                    AddIssue("schema.stage.knowledge", schema.id + "/" + stage.id,
                        "Stage minimumKnowledge must be finite and nonnegative.");
                    valid = false;
                }
                if (!KnowledgeMath.IsFinite(stage.minimumConfidence) || stage.minimumConfidence < 0f || stage.minimumConfidence > 1f)
                {
                    AddIssue("schema.stage.confidence", schema.id + "/" + stage.id,
                        "Stage minimumConfidence must be finite and between zero and one.");
                    valid = false;
                }
            }
            if (schema.facets.Any(item => !ValidId(item.id)) || schema.facets.GroupBy(item => item.id).Any(group => group.Count() > 1))
            {
                AddIssue("schema.facet.id", schema.id, "Facet stable IDs must be valid and unique.");
                valid = false;
            }
            if (schema.facets.Any(item => item.relatedFacetIds != null && item.relatedFacetIds.Count > 0))
            {
                AddIssue("schema.facet.related", schema.id, "relatedFacetIds is unsupported; use explicit facet requirements or observations.");
                valid = false;
            }
            if (schema.stages.Any(item => !ValidId(item.id)) || schema.stages.GroupBy(item => item.id).Any(group => group.Count() > 1) || schema.stages.Zip(schema.stages.Skip(1), (a, b) => a.order >= b.order).Any(value => value))
            {
                AddIssue("schema.stage.order", schema.id, "Discovery stage IDs and order values must be strictly increasing.");
                valid = false;
            }
            foreach (KnowledgeStageSchema stage in schema.stages)
                if (!ValidateRequirementGroup(stage.requirementGroup, schema.id, "stage/" + stage.id, ref valid)) valid = false;
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
                KnowledgeAccrualPolicy accrual = observation?.accrualPolicy;
                float[] accrualMultipliers = accrual == null ? Array.Empty<float>() : new[] { accrual.diminishingReturns, accrual.firstObservationBonus,
                    accrual.firstSuccessBonus, accrual.firstFailureBonus, accrual.differentSpecimenBonus, accrual.differentContextBonus,
                    accrual.independentSourceConfidenceBonus, accrual.repeatedSourceConfidencePenalty };
                if (accrual != null && (accrual.stateLimit <= 0 || accrual.stateLimit > 4096 || accrual.cooldownTicks < 0 || accrual.dailyCap < 0 || accrual.dailyCap > 100000000 ||
                    accrual.lifetimeCap < 0 || accrual.lifetimeCap > 100000000 ||
                    !KnowledgeMath.IsFinite(accrual.diminishingReturns) || accrual.diminishingReturns < 0f || accrual.diminishingReturns > 1f ||
                    accrualMultipliers.Any(value => !KnowledgeMath.IsFinite(value) || value < 0f || value > 100f)))
                {
                    AddIssue("schema.observation.accrual", observation?.defName ?? schema.id, "Accrual policies must use finite bounded multipliers/caps and stateLimit >= 1; zero is invalid because it cannot retain policy history.");
                    valid = false;
                }
                if (observation?.witnessDistribution?.policy == KnowledgeWitnessDistributionPolicy.PartyShared)
                {
                    AddIssue("schema.observation.witnesses", observation.defName ?? schema.id, "PartyShared witness distribution is unsupported; use WitnessesReduced or Custom.");
                    valid = false;
                }
            }
            foreach (KnowledgeClaimDef claim in schema.claims)
            {
                if (claim == null || !ValidId(claim.StableId) || !KnowledgeMath.IsFinite(claim.halfLifeTicks) || claim.halfLifeTicks < 0f ||
                    !KnowledgeMath.IsFinite(claim.provisionalConfidence) || claim.provisionalConfidence < 0f || claim.provisionalConfidence > 1f ||
                    claim.measurementHistoryLimit < 1 || claim.provenanceLimit < 0 ||
                    !claim.facetId.NullOrEmpty() && schema.Facet(claim.facetId) == null ||
                    claim.stalenessPolicy == KnowledgeClaimStalenessPolicy.Contextual ||
                    !Enum.IsDefined(typeof(KnowledgeClaimValueType), claim.valueType) ||
                    !Enum.IsDefined(typeof(KnowledgeClaimAggregation), claim.aggregation) ||
                    claim.aggregation == KnowledgeClaimAggregation.ConsumerDefined &&
                    (claim.consumerAggregationId.NullOrEmpty() || !KnowledgeClaimService.HasAggregator(claim.consumerAggregationId)) ||
                    !claim.domainId.NullOrEmpty() && ResolveDomainId(claim.domainId) != schema.id)
                {
                    AddIssue("schema.claim.values", claim?.defName ?? schema.id,
                        "Claim values, aggregation providers, domain ownership, and facet references must be valid.");
                    valid = false;
                }
            }
            if (schema.claims.GroupBy(item => item.StableId).Any(group => group.Key.NullOrEmpty() || group.Count() > 1))
            {
                AddIssue("schema.claim.id", schema.id, "Claim stable IDs must be unique and valid.");
                valid = false;
            }
            foreach (KnowledgeSubjectArchetypeDef archetype in schema.archetypes)
            {
                if (archetype == null || !ValidId(archetype.StableId) || schema.archetypes.Count(item => item != null && item.StableId == archetype.StableId) > 1 ||
                    (archetype.applicableFacetIds ?? new List<string>()).Distinct().Count() != (archetype.applicableFacetIds ?? new List<string>()).Count ||
                    (archetype.applicableFacetIds ?? new List<string>()).Any(id => schema.Facet(id) == null) ||
                    (archetype.applicableClaimIds ?? new List<string>()).Any(id => schema.Claim(id) == null) ||
                    !archetype.categoryId.NullOrEmpty() || !archetype.iconPath.NullOrEmpty() || !archetype.templateSubjectId.NullOrEmpty() ||
                    archetype.contextual || (archetype.discoveryStageIds ?? new List<string>()).Count > 0 ||
                    (archetype.observationIds ?? new List<string>()).Count > 0 || (archetype.effectIds ?? new List<string>()).Count > 0 ||
                    (archetype.expertiseTrackIds ?? new List<string>()).Count > 0)
                {
                    AddIssue("schema.archetype", archetype?.defName ?? schema.id, "Archetypes must reference existing, uniquely applicable facets and claims; contextual and restriction fields are unsupported.");
                    valid = false;
                }
            }
            if (schema.archetypes.GroupBy(item => item.StableId).Any(group => group.Key.NullOrEmpty() || group.Count() > 1))
            {
                AddIssue("schema.archetype.id", schema.id, "Archetype stable IDs must be unique and valid.");
                valid = false;
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
                if (reveal != null && !reveal.domainId.NullOrEmpty() && ResolveDomainId(reveal.domainId) != schema.id)
                {
                    AddIssue("schema.reveal.domain", reveal.defName ?? schema.id, "Reveal domainId must match its containing domain.");
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
                if (effect != null && !effect.domainId.NullOrEmpty() && ResolveDomainId(effect.domainId) != schema.id)
                {
                    AddIssue("schema.effect.domain", effect.defName ?? schema.id, "Effect domainId must match its containing domain.");
                    valid = false;
                }
                ValidateRequirementGroup(effect?.requirements, schema.id, "effect/" + (effect?.defName ?? "null"), ref valid);
            }
            foreach (KnowledgeInsightDef insight in schema.insights)
            {
                ValidateRequirementGroup(insight?.requirementGroup, schema.id, "insight/" + (insight?.defName ?? "null"), ref valid);
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
            foreach (KnowledgeMilestoneTrackDef track in schema.milestoneTracks)
            {
                if (track != null && !track.domainId.NullOrEmpty() && ResolveDomainId(track.domainId) != schema.id)
                {
                    AddIssue("schema.milestone.domain", track.defName ?? schema.id, "Milestone track domainId must match its containing domain.");
                    valid = false;
                }
                foreach (KnowledgeMilestoneDef milestone in track?.milestones ?? new List<KnowledgeMilestoneDef>())
                {
                    if (milestone == null || !ValidId(milestone.StableId) || milestone.sustainedTicks < 0 || !milestone.customEvaluatorId.NullOrEmpty())
                    {
                        AddIssue("schema.milestone", track?.defName ?? schema.id, "Milestone IDs and sustained durations must be valid; custom evaluators are unsupported.");
                        valid = false;
                    }
                    ValidateRequirementGroup(milestone?.requirements, schema.id, "milestone/" + (milestone?.StableId ?? "null"), ref valid);
                }
            }
            if (schema.transmission != null && !schema.transmission.domainId.NullOrEmpty() &&
                ResolveDomainId(schema.transmission.domainId) != schema.id)
            {
                AddIssue("schema.transmission.domain", schema.id, "Transmission domainId must match its containing domain.");
                valid = false;
            }
            if (schema.insights.Any(item => !ValidId(item.defName)) || schema.insights.GroupBy(item => item.defName).Any(group => group.Count() > 1))
            {
                AddIssue("schema.insight.id", schema.id, "Insight IDs must be valid and unique.");
                valid = false;
            }
            foreach (KnowledgeRelationshipDef relationship in schema.relationships)
            {
                if (relationship.domainId.NullOrEmpty()) relationship.domainId = schema.id;
                if (relationship.fromDomainId.NullOrEmpty()) relationship.fromDomainId = schema.id;
                if (relationship.toDomainId.NullOrEmpty()) relationship.toDomainId = schema.id;
                if (relationship.domainId != schema.id || !ValidId(relationship.fromDomainId) || !ValidId(relationship.toDomainId) ||
                    !ValidId(relationship.fromSubjectId) || !ValidId(relationship.toSubjectId) ||
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
                 !KnowledgeMath.IsFinite(schema.transmission.confidenceEfficiency) || schema.transmission.confidenceEfficiency < 0f || schema.transmission.confidenceEfficiency > 1f ||
                 schema.transmission.model != KnowledgeSharingModel.Immediate))
            {
                AddIssue("schema.transmission.values", schema.id, "Transmission efficiencies must be finite values from zero to one; transmission model is unsupported, use the domain sharingModel.");
                valid = false;
            }
            return valid;
        }

        private static bool ValidateRequirementGroup(KnowledgeRequirementGroup group, string schemaDomain, string owner, ref bool valid, int depth = 0)
        {
            if (group == null) return true;
            if (depth > 16 || group.minimumCount < 0 || !KnowledgeMath.IsFinite(group.minimumWeight) || group.minimumWeight < 0f ||
                !Enum.IsDefined(typeof(KnowledgeRequirementGroupMode), group.mode))
            {
                AddIssue("schema.requirement.group", owner, "Requirement groups must be bounded and use finite thresholds.");
                valid = false;
            }
            foreach (KnowledgeRequirement requirement in group.requirements ?? new List<KnowledgeRequirement>())
            {
                if (requirement == null || !KnowledgeMath.IsFinite(requirement.minimum) || !KnowledgeMath.IsFinite(requirement.maximum) ||
                    requirement.minimum < 0f || requirement.maximum < 0f || requirement.maximum > 0f && requirement.maximum < requirement.minimum ||
                    !KnowledgeMath.IsFinite(requirement.weight) || requirement.weight < 0f)
                {
                    AddIssue("schema.requirement.values", owner, "Requirement values must be finite and non-negative.");
                    valid = false;
                }
                if (requirement != null && (requirement.kind.ToString() == "Context" || requirement.kind.ToString() == "RelatedClaim"))
                {
                    AddIssue("schema.requirement.unsupported-kind", owner, "Requirement kind '" + requirement.kind + "' is unsupported; use an explicit facet or claim requirement.");
                    valid = false;
                }
                if (requirement != null && (!Enum.IsDefined(typeof(KnowledgeRequirementKind), requirement.kind) ||
                    !Enum.IsDefined(typeof(KnowledgeRequirementComparison), requirement.comparison)))
                {
                    AddIssue("schema.requirement.enum", owner, "Requirement kind and comparison must be supported enum values.");
                    valid = false;
                }
                if (requirement != null && !requirement.domainId.NullOrEmpty() && Schema(requirement.domainId) == null)
                    AddIssue("schema.requirement.optional-domain", owner, "Requirement references an unavailable optional domain '" + requirement.domainId + "'.", false);
            }
            foreach (KnowledgeRequirementGroup child in group.groups ?? new List<KnowledgeRequirementGroup>())
                ValidateRequirementGroup(child, schemaDomain, owner, ref valid, depth + 1);
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
                    IEnumerable<KnowledgeInsightRequirement> requirements = insight.requirements ?? new List<KnowledgeInsightRequirement>();
                    IEnumerable<string> facets = requirements.Select(item => item?.facetId).Where(item => !item.NullOrEmpty()).Distinct();
                    if (!facets.Any()) facets = schema.facets.Select(item => item.id);
                    foreach (string facet in facets)
                    {
                        string dependencyDomain = requirements.FirstOrDefault(item => item?.facetId == facet && !item.domainId.NullOrEmpty())?.domainId ?? schema.id;
                        AddTo(InsightsByDependency, DependencyKey(dependencyDomain, facet), insight);
                    }
                }
                foreach (KnowledgeRelationshipDef relation in schema.relationships)
                    AddTo(RelationshipsByTarget, RelationshipKey(relation.toDomainId.NullOrEmpty() ? schema.id : relation.toDomainId, relation.toSubjectId, relation.facetId), relation);
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

        private static void AddIssue(string code, string owner, string message, bool error = true)
        {
            KnowledgeValidationIssue issue = new KnowledgeValidationIssue(code, owner, message, error);
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

        private static KnowledgeSubjectRegistration CloneSubject(KnowledgeSubjectRegistration value)
        {
            if (value == null) return null;
            return new KnowledgeSubjectRegistration
            {
                id = value.id,
                label = value.label,
                description = value.description,
                unidentifiedLabel = value.unidentifiedLabel,
                unidentifiedDescription = value.unidentifiedDescription,
                iconPath = value.iconPath,
                sourceDef = value.sourceDef,
                templateSubjectId = value.templateSubjectId,
                templateKnowledgeCoefficient = value.templateKnowledgeCoefficient,
                templateConfidenceCoefficient = value.templateConfidenceCoefficient,
                categoryIds = value.categoryIds?.ToList(),
                applicableFacetIds = value.applicableFacetIds?.ToList(),
                applicableClaimIds = value.applicableClaimIds?.ToList(),
                sortOrder = value.sortOrder,
                state = value.state,
                source = value.source
            };
        }

        private static KnowledgeSubjectRegistration MergeSubject(KnowledgeSubjectRegistration preferred, KnowledgeSubjectRegistration fallback)
        {
            KnowledgeSubjectRegistration value = CloneSubject(preferred);
            if (value == null) return fallback;
            if (value.label.NullOrEmpty()) value.label = fallback.label;
            if (value.description.NullOrEmpty()) value.description = fallback.description;
            if (value.sourceDef == null) value.sourceDef = fallback.sourceDef;
            if (value.iconPath.NullOrEmpty()) value.iconPath = fallback.iconPath;
            if (value.categoryIds == null) value.categoryIds = fallback.categoryIds;
            if (value.applicableFacetIds == null) value.applicableFacetIds = fallback.applicableFacetIds;
            if (value.applicableClaimIds == null) value.applicableClaimIds = fallback.applicableClaimIds;
            return value;
        }

        private static void InvokeSubjectChanged(KnowledgeSubjectSnapshot subject)
        {
            Action<KnowledgeSubjectSnapshot> handlers = SubjectChanged;
            if (handlers == null) return;
            foreach (Action<KnowledgeSubjectSnapshot> handler in handlers.GetInvocationList())
                try { handler(subject); }
                catch (Exception exception) { KnowledgeLog.ErrorOnce("subject-changed:" + handler.Method.Name, "A subject change subscriber failed.", exception); }
        }
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
