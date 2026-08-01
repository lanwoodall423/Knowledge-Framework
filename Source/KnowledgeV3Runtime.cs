using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    public interface IKnowledgeContextResolver
    {
        KnowledgeContextKey Parent(KnowledgeContextKey context);
    }

    public static class KnowledgeContextRegistry
    {
        private static readonly Dictionary<string, KnowledgeContextTypeDef> Types = new Dictionary<string, KnowledgeContextTypeDef>(StringComparer.Ordinal);
        private static readonly Dictionary<string, IKnowledgeContextResolver> Resolvers = new Dictionary<string, IKnowledgeContextResolver>(StringComparer.Ordinal);

        public static bool RegisterType(KnowledgeContextTypeDef definition, bool replace = false)
        {
            if (definition == null || definition.StableId.NullOrEmpty() || Types.ContainsKey(definition.StableId) && !replace) return false;
            Types[definition.StableId] = definition;
            return true;
        }

        public static bool RegisterResolver(string typeId, IKnowledgeContextResolver resolver, bool replace = false)
        {
            if (typeId.NullOrEmpty() || resolver == null || Resolvers.ContainsKey(typeId) && !replace) return false;
            Resolvers[typeId] = resolver;
            return true;
        }

        public static KnowledgeContextTypeDef Type(string typeId)
        {
            if (typeId.NullOrEmpty()) return null;
            if (Types.TryGetValue(typeId, out KnowledgeContextTypeDef result)) return result;
            KnowledgeContextTypeDef def = DefDatabase<KnowledgeContextTypeDef>.AllDefsListForReading.FirstOrDefault(item => item.StableId == typeId);
            if (def != null) Types[typeId] = def;
            return def;
        }

        public static KnowledgeContextKey Parent(KnowledgeContextKey context)
        {
            if (context.IsEmpty) return KnowledgeContextKey.Empty;
            if (Resolvers.TryGetValue(context.typeId, out IKnowledgeContextResolver resolver))
            {
                try { return resolver.Parent(context); }
                catch (Exception exception) { KnowledgeLog.ErrorOnce("context-parent:" + context.typeId, "A context parent resolver failed.", exception); return KnowledgeContextKey.Empty; }
            }
            KnowledgeContextTypeDef type = Type(context.typeId);
            if (type != null && !type.parentTypeId.NullOrEmpty()) return new KnowledgeContextKey(type.parentTypeId, context.stableId);
            return KnowledgeContextKey.Empty;
        }

        public static IReadOnlyList<KnowledgeContextKey> Chain(KnowledgeContextKey context, KnowledgeContextFallbackMode fallback)
        {
            List<KnowledgeContextKey> result = new List<KnowledgeContextKey>();
            if (context.IsEmpty) return result;
            HashSet<KnowledgeContextKey> seen = new HashSet<KnowledgeContextKey>();
            KnowledgeContextKey current = context;
            while (!current.IsEmpty && seen.Add(current))
            {
                result.Add(current);
                if (fallback == KnowledgeContextFallbackMode.ExactOnly) break;
                current = Parent(current);
            }
            if (fallback == KnowledgeContextFallbackMode.ParentThenGlobal) result.Add(KnowledgeContextKey.Empty);
            return result;
        }

        public static IReadOnlyList<string> Validate()
        {
            List<string> issues = new List<string>();
            foreach (KnowledgeContextTypeDef type in Types.Values.Concat(DefDatabase<KnowledgeContextTypeDef>.AllDefsListForReading).Where(item => item != null).GroupBy(item => item.StableId).Select(group => group.First()))
            {
                if (type.StableId.NullOrEmpty()) issues.Add("context type missing stable ID");
                HashSet<KnowledgeContextKey> seen = new HashSet<KnowledgeContextKey>();
                KnowledgeContextKey current = new KnowledgeContextKey(type.StableId, "validation");
                while (!current.IsEmpty && seen.Add(current)) current = Parent(current);
                if (!current.IsEmpty) issues.Add("context parent cycle: " + type.StableId);
            }
            return issues;
        }
    }

    public static class KnowledgeClaimService
    {
        private static readonly Dictionary<string, KnowledgeClaimAggregator> CustomAggregators =
            new Dictionary<string, KnowledgeClaimAggregator>(StringComparer.Ordinal);

        public static event Action<KnowledgeClaimChangedEvent> Changed;

        public static bool RegisterAggregator(string id, KnowledgeClaimAggregator aggregator, bool replace = false)
        {
            if (id.NullOrEmpty() || aggregator == null || CustomAggregators.ContainsKey(id) && !replace) return false;
            CustomAggregators[id] = aggregator;
            return true;
        }

        public static KnowledgeClaimSnapshot Snapshot(string domainId, string subjectId, string facetId, string claimId, Pawn pawn = null,
            KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextKey context = default(KnowledgeContextKey),
            KnowledgeContextFallbackMode fallback = KnowledgeContextFallbackMode.ExactOnly)
        {
            domainId = KnowledgeRegistry.ResolveDomainId(domainId);
            subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, subjectId);
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            KnowledgeClaimDef claim = schema?.Claim(claimId);
            facetId = facetId.NullOrEmpty() ? claim?.facetId ?? KnowledgeSchema.DefaultFacetId : facetId;
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (schema == null || claim == null || component == null)
                return Empty(domainId, subjectId, facetId, claimId, pawn, scope, context);
            bool colony = scope == KnowledgeScope.Colony;
            KnowledgeClaimStateRecord record = null;
            KnowledgeContextKey selectedContext = KnowledgeContextKey.Empty;
            foreach (KnowledgeContextKey candidate in KnowledgeContextRegistry.Chain(context, fallback))
            {
                record = component.ClaimV3(domainId, subjectId, facetId, claim.StableId, pawn, colony, candidate, false);
                if (record != null && record.measurements.Count > 0)
                {
                    selectedContext = candidate;
                    break;
                }
            }
            return Aggregate(domainId, subjectId, facetId, claim, claim.StableId, pawn, scope, selectedContext, record);
        }

        public static IReadOnlyList<KnowledgeClaimSnapshot> ForSubject(string domainId, string subjectId, string facetId = null,
            Pawn pawn = null, KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextKey context = default(KnowledgeContextKey),
            KnowledgeContextFallbackMode fallback = KnowledgeContextFallbackMode.ExactOnly)
        {
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            if (schema == null) return Array.Empty<KnowledgeClaimSnapshot>();
            return KnowledgeRegistry.ApplicableClaims(domainId, subjectId, facetId).Select(claim => Snapshot(domainId, subjectId,
                facetId ?? claim.facetId, claim.StableId, pawn, scope, context, fallback)).ToList();
        }

        internal static KnowledgeClaimValue AggregateForVerification(KnowledgeClaimDef claim, IEnumerable<KnowledgeMeasurement> measurements)
        {
            List<KnowledgeMeasurementRecord> records = (measurements ?? Enumerable.Empty<KnowledgeMeasurement>()).Select(item =>
                KnowledgeMeasurementRecord.FromMeasurement(item, item.domainId, item.subjectId, item.facetId, item.observer, item.scope)).Where(item => item != null).ToList();
            return AggregateValue(claim, records);
        }

        internal static bool ValidateMeasurement(KnowledgeMeasurement measurement, KnowledgeObservation observation, out string error)
        {
            error = null;
            if (measurement == null || measurement.value == null) { error = "Claim measurement requires a typed value."; return false; }
            string domainId = KnowledgeRegistry.ResolveDomainId(measurement.domainId ?? observation.domainId);
            string subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, measurement.subjectId ?? observation.subjectId);
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            string facetId = measurement.facetId ?? observation.facetId;
            KnowledgeClaimDef claim = schema?.Claim(measurement.claimId);
            if (schema == null || subjectId.NullOrEmpty() || KnowledgeRegistry.ResolveSubject(domainId, subjectId) == null)
            { error = "Claim measurement references an unknown domain or subject."; return false; }
            if (claim == null) { error = "Unknown claim '" + measurement.claimId + "'."; return false; }
            if (facetId.NullOrEmpty()) facetId = claim.facetId ?? KnowledgeSchema.DefaultFacetId;
            if (schema.Facet(facetId) == null || !KnowledgeRegistry.ApplicableFacets(domainId, subjectId).Any(item => item.id == facetId))
            { error = "Claim measurement references an inapplicable facet."; return false; }
            if (!KnowledgeRegistry.ApplicableClaims(domainId, subjectId, facetId).Any(item => item.StableId == claim.StableId))
            { error = "Claim is not applicable to the subject."; return false; }
            if (!measurement.value.TryValidate(claim.valueType, out error)) return false;
            float[] values = { measurement.quality, measurement.evidenceWeight, measurement.confidenceFactor };
            if (values.Any(value => !KnowledgeMath.IsFinite(value) || value < 0f)) { error = "Claim measurement weights must be finite and non-negative."; return false; }
            if (measurement.confidenceFactor > 1f) { error = "Claim confidence factor must be between zero and one."; return false; }
            if (measurement.scope == KnowledgeScope.Personal && (measurement.observer ?? observation.observer) == null)
            { error = "Personal claim measurements require an observer."; return false; }
            if (!measurement.context.IsEmpty && (measurement.context.typeId.NullOrEmpty() || measurement.context.stableId.NullOrEmpty()))
            { error = "Context keys require stable type and ID values."; return false; }
            return true;
        }

        internal static bool Apply(GameComponent_KnowledgeFramework component, KnowledgeMeasurement measurement, KnowledgeObservation observation)
        {
            string domainId = KnowledgeRegistry.ResolveDomainId(measurement.domainId ?? observation.domainId);
            string subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, measurement.subjectId ?? observation.subjectId);
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            KnowledgeClaimDef claim = schema?.Claim(measurement.claimId);
            if (schema == null || claim == null) return false;
            string facetId = measurement.facetId.NullOrEmpty() ? claim.facetId ?? KnowledgeSchema.DefaultFacetId : measurement.facetId;
            Pawn observer = measurement.observer ?? observation.observer;
            KnowledgeScope scope = measurement.scope;
            bool colony = scope == KnowledgeScope.Colony || observation.targetColony;
            KnowledgeContextKey context = measurement.context.IsEmpty ? KnowledgeV3Runtime.ContextFor(observation) : measurement.context;
            KnowledgeClaimStateRecord record = component.ClaimV3(domainId, subjectId, facetId, claim.StableId, colony ? null : observer, colony, context, true);
            KnowledgeClaimSnapshot oldValue = Snapshot(domainId, subjectId, facetId, claim.StableId, colony ? null : observer,
                colony ? KnowledgeScope.Colony : KnowledgeScope.Personal, context);
            KnowledgeMeasurementRecord persisted = KnowledgeMeasurementRecord.FromMeasurement(measurement, domainId, subjectId, facetId, observer,
                colony ? KnowledgeScope.Colony : KnowledgeScope.Personal);
            if (persisted == null) return false;
            record.measurements.Add(persisted);
            int limit = Math.Max(1, Math.Min(128, claim.measurementHistoryLimit));
            while (record.measurements.Count > limit) record.measurements.RemoveAt(0);
            record.Normalize();
            component.TouchV3();
            KnowledgeClaimSnapshot newValue = Snapshot(domainId, subjectId, facetId, claim.StableId, colony ? null : observer,
                colony ? KnowledgeScope.Colony : KnowledgeScope.Personal, context);
            InvokeSafely(new KnowledgeClaimChangedEvent(domainId, subjectId, facetId, claim.StableId, colony ? null : observer,
                colony ? KnowledgeScope.Colony : KnowledgeScope.Personal, context, oldValue, newValue));
            return true;
        }

        private static KnowledgeClaimSnapshot Aggregate(string domainId, string subjectId, string facetId, KnowledgeClaimDef claim,
            string claimId, Pawn pawn, KnowledgeScope scope, KnowledgeContextKey context, KnowledgeClaimStateRecord record)
        {
            List<KnowledgeMeasurementRecord> measurements = (record?.measurements ?? new List<KnowledgeMeasurementRecord>()).Where(item => item != null).ToList();
            float support = measurements.Where(item => item.disposition != (int)KnowledgeEvidenceDisposition.Contradictory)
                .Sum(Weight);
            float contradiction = measurements.Where(item => item.disposition == (int)KnowledgeEvidenceDisposition.Contradictory).Sum(Weight);
            float stored = KnowledgeMath.Clamp01Finite(support / (support + contradiction + 1f));
            int now = Find.TickManager?.TicksGame ?? measurements.Select(item => item.tick).DefaultIfEmpty(0).Max();
            int lastConfirmed = measurements.Where(item => item.disposition != (int)KnowledgeEvidenceDisposition.Contradictory)
                .Select(item => item.tick).DefaultIfEmpty(0).Max();
            float staleness = Staleness(claim, lastConfirmed, now);
            float effective = KnowledgeMath.Clamp01Finite(stored * (1f - staleness));
            KnowledgeMeasurementRecord best = measurements.OrderByDescending(Weight).ThenByDescending(item => item.tick).FirstOrDefault();
            KnowledgeClaimValue value = AggregateValue(claim, measurements);
            bool documented = measurements.Any(item => item.documented);
            bool revealed = claim.revealedByDefault || measurements.Any(item => item.revealed);
            bool contradictory = contradiction > 0f && support > 0f;
            List<KnowledgeClaimMeasurementSnapshot> provenance = measurements.OrderByDescending(item => item.tick)
                .Take(Math.Max(0, claim.provenanceLimit)).Select(item => new KnowledgeClaimMeasurementSnapshot(item)).ToList();
            return new KnowledgeClaimSnapshot(domainId, subjectId, facetId, claimId, pawn, scope, context, value, stored, effective,
                effective < claim.provisionalConfidence || contradictory, contradictory, measurements.Count, lastConfirmed,
                best?.source, staleness, revealed, documented, provenance);
        }

        private static KnowledgeClaimSnapshot Empty(string domainId, string subjectId, string facetId, string claimId, Pawn pawn,
            KnowledgeScope scope, KnowledgeContextKey context) => new KnowledgeClaimSnapshot(domainId, subjectId, facetId, claimId, pawn, scope,
                context, null, 0f, 0f, true, false, 0, 0, null, 0f, false, false, null);

        private static float Weight(KnowledgeMeasurementRecord item) => Math.Max(0f, item.quality * item.evidenceWeight * item.confidenceFactor);

        private static float Staleness(KnowledgeClaimDef claim, int lastConfirmed, int now)
        {
            if (lastConfirmed <= 0 || claim.stalenessPolicy == KnowledgeClaimStalenessPolicy.Permanent || claim.stalenessPolicy == KnowledgeClaimStalenessPolicy.ConsumerManaged) return 0f;
            float halfLife = KnowledgeMath.PositiveFiniteOr(claim.halfLifeTicks, 600000f);
            float age = Math.Max(0, now - lastConfirmed);
            if (claim.stalenessPolicy == KnowledgeClaimStalenessPolicy.Seasonal) age = age % Math.Max(1f, halfLife * 2f);
            return KnowledgeMath.Clamp01Finite(1f - (float)Math.Pow(0.5d, age / halfLife));
        }

        private static KnowledgeClaimValue AggregateValue(KnowledgeClaimDef claim, List<KnowledgeMeasurementRecord> records)
        {
            List<KnowledgeMeasurementRecord> values = records.Where(item => item.disposition != (int)KnowledgeEvidenceDisposition.Contradictory).ToList();
            if (values.Count == 0) values = records;
            if (values.Count == 0) return null;
            if (claim.aggregation == KnowledgeClaimAggregation.ConsumerDefined && !claim.consumerAggregationId.NullOrEmpty() &&
                CustomAggregators.TryGetValue(claim.consumerAggregationId, out KnowledgeClaimAggregator custom))
            {
                try { return custom(values.Select(item => new KnowledgeClaimMeasurementSnapshot(item)).ToList())?.Clone(); }
                catch (Exception exception) { KnowledgeLog.ErrorOnce("claim-aggregate:" + claim.consumerAggregationId, "A claim aggregator failed.", exception); }
            }
            KnowledgeMeasurementRecord latest = values.OrderByDescending(item => item.tick).ThenByDescending(Weight).First();
            KnowledgeMeasurementRecord latestOverall = records.OrderByDescending(item => item.tick).ThenByDescending(Weight).First();
            bool numeric = claim.valueType == KnowledgeClaimValueType.Integer || claim.valueType == KnowledgeClaimValueType.Float ||
                claim.valueType == KnowledgeClaimValueType.Percentage;
            if (claim.aggregation == KnowledgeClaimAggregation.Latest) return latestOverall.ToValue();
            if (claim.aggregation == KnowledgeClaimAggregation.HighestQuality)
                return records.OrderByDescending(Weight).ThenByDescending(item => item.tick).First().ToValue();
            if (claim.aggregation == KnowledgeClaimAggregation.MostSupported)
                return values.GroupBy(item => item.ToValue().StableKey()).OrderByDescending(group => group.Sum(Weight)).ThenByDescending(group => group.Count())
                    .Select(group => values.First(item => item.ToValue().StableKey() == group.Key).ToValue()).First();
            if (claim.valueType == KnowledgeClaimValueType.SetOfIds && (claim.aggregation == KnowledgeClaimAggregation.Union || claim.aggregation == KnowledgeClaimAggregation.Intersection))
            {
                IEnumerable<string> set = claim.aggregation == KnowledgeClaimAggregation.Union
                    ? values.SelectMany(item => item.ToValue().setValues ?? new List<string>()).Distinct()
                    : values.Select(item => new HashSet<string>(item.ToValue().setValues ?? new List<string>())).Aggregate((left, right) =>
                    { left.IntersectWith(right); return left; });
                return KnowledgeClaimValue.Set(set);
            }
            if (numeric)
            {
                float[] numbers = values.Select(item => item.ToValue().type == KnowledgeClaimValueType.Integer ? item.ToValue().integerValue : item.ToValue().numericValue).ToArray();
                if (claim.aggregation == KnowledgeClaimAggregation.Highest) return NumericValue(claim.valueType, numbers.Max());
                if (claim.aggregation == KnowledgeClaimAggregation.Lowest) return NumericValue(claim.valueType, numbers.Min());
                if (claim.aggregation == KnowledgeClaimAggregation.ObservedRange) return KnowledgeClaimValue.Range(numbers.Min(), numbers.Max());
                if (claim.aggregation == KnowledgeClaimAggregation.Mean || claim.aggregation == KnowledgeClaimAggregation.WeightedMean)
                {
                    float total = 0f;
                    float weight = 0f;
                    for (int i = 0; i < values.Count; i++) { float current = values[i].ToValue().type == KnowledgeClaimValueType.Integer ? values[i].ToValue().integerValue : values[i].ToValue().numericValue; float currentWeight = claim.aggregation == KnowledgeClaimAggregation.WeightedMean ? Weight(values[i]) : 1f; total += current * currentWeight; weight += currentWeight; }
                    return NumericValue(claim.valueType, weight <= 0f ? 0f : total / weight);
                }
            }
            return latest.ToValue();
        }

        private static KnowledgeClaimValue NumericValue(KnowledgeClaimValueType type, float value) => type == KnowledgeClaimValueType.Integer
            ? KnowledgeClaimValue.Integer((int)Math.Round(value)) : type == KnowledgeClaimValueType.Percentage
                ? KnowledgeClaimValue.Percentage(value) : KnowledgeClaimValue.Float(value);

        private static void InvokeSafely(KnowledgeClaimChangedEvent value)
        {
            Action<KnowledgeClaimChangedEvent> handlers = Changed;
            if (handlers == null) return;
            foreach (Action<KnowledgeClaimChangedEvent> handler in handlers.GetInvocationList())
                try { handler(value); }
                catch (Exception exception) { KnowledgeLog.ErrorOnce("claim-event:" + handler.Method.Name, "A claim subscriber failed.", exception); }
        }
    }

    internal static class KnowledgeRequirementService
    {
        internal static bool Evaluate(KnowledgeRequirementGroup group, string defaultDomainId, string subjectId, Pawn pawn,
            KnowledgeScope scope, KnowledgeContextKey context, out List<string> unmet)
        {
            unmet = new List<string>();
            if (group == null) return true;
            List<bool> results = new List<bool>();
            foreach (KnowledgeRequirement requirement in group.requirements ?? new List<KnowledgeRequirement>())
            {
                bool met = Evaluate(requirement, defaultDomainId, subjectId, pawn, scope, context);
                results.Add(met);
                if (!met) unmet.Add(requirement?.label ?? requirement?.kind.ToString() ?? "requirement");
            }
            foreach (KnowledgeRequirementGroup child in group.groups ?? new List<KnowledgeRequirementGroup>())
            {
                bool met = Evaluate(child, defaultDomainId, subjectId, pawn, scope, context, out List<string> childUnmet);
                results.Add(met);
                if (!met) unmet.AddRange(childUnmet);
            }
            if (results.Count == 0) return true;
            int metCount = results.Count(value => value);
            bool result;
            switch (group.mode)
            {
                case KnowledgeRequirementGroupMode.Any: result = metCount > 0; break;
                case KnowledgeRequirementGroupMode.AtLeast: result = metCount >= Math.Max(1, group.minimumCount); break;
                case KnowledgeRequirementGroupMode.Weighted:
                    float total = (group.requirements ?? new List<KnowledgeRequirement>()).Where((item, index) => index < results.Count)
                        .Select((item, index) => item.weight * (results[index] ? 1f : 0f)).Sum();
                    float possible = (group.requirements ?? new List<KnowledgeRequirement>()).Sum(item => Math.Max(0f, item?.weight ?? 0f));
                    result = total >= (group.minimumWeight <= 0f ? possible : group.minimumWeight); break;
                default: result = metCount == results.Count; break;
            }
            return result;
        }

        internal static bool Evaluate(KnowledgeRequirement requirement, string defaultDomainId, string defaultSubjectId, Pawn pawn,
            KnowledgeScope defaultScope, KnowledgeContextKey defaultContext)
        {
            if (requirement == null) return false;
            string domainId = requirement.domainId.NullOrEmpty() ? defaultDomainId : requirement.domainId;
            string subjectId = requirement.subjectId.NullOrEmpty() ? defaultSubjectId : requirement.subjectId;
            KnowledgeScope scope = requirement.colony ? KnowledgeScope.Colony : defaultScope;
            KnowledgeContextKey context = requirement.contextTypeId.NullOrEmpty() ? defaultContext : new KnowledgeContextKey(requirement.contextTypeId, requirement.contextId);
            KnowledgeFacetSnapshotV2 facet = KnowledgeQuery.Facet(domainId, subjectId, requirement.facetId, pawn, scope, true, true, context,
                requirement.allowContextFallback ? KnowledgeContextFallbackMode.ParentThenGlobal : KnowledgeContextFallbackMode.ExactOnly);
            bool result;
            switch (requirement.kind)
            {
                case KnowledgeRequirementKind.Knowledge: result = CompareNumber(facet.amount, requirement.minimum, requirement.comparison); break;
                case KnowledgeRequirementKind.Confidence: result = CompareNumber(facet.confidence, requirement.minimum, requirement.comparison); break;
                case KnowledgeRequirementKind.Completeness: result = CompareNumber(facet.completeness, requirement.minimum, requirement.comparison); break;
                case KnowledgeRequirementKind.EvidenceCount:
                case KnowledgeRequirementKind.EventCount:
                    result = requirement.eventId.NullOrEmpty() ? CompareNumber(facet.evidenceCount, requirement.minimum, requirement.comparison) : CompareNumber(facet.EventCount(requirement.eventId), requirement.minimum, requirement.comparison); break;
                case KnowledgeRequirementKind.SuccessCount: result = CompareNumber(facet.successCount, requirement.minimum, requirement.comparison); break;
                case KnowledgeRequirementKind.FailureCount: result = CompareNumber(facet.failureCount, requirement.minimum, requirement.comparison); break;
                case KnowledgeRequirementKind.Familiarity: result = CompareNumber(KnowledgeQuery.Subject(domainId, subjectId, pawn, scope).familiarity, requirement.minimum, requirement.comparison); break;
                case KnowledgeRequirementKind.DiscoveryStage:
                    KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
                    KnowledgeStageSchema actual = schema?.Stage(KnowledgeQuery.Subject(domainId, subjectId, pawn, scope).stageId);
                    KnowledgeStageSchema expected = schema?.Stage(requirement.stageId);
                    result = actual != null && expected != null && actual.order >= expected.order; break;
                case KnowledgeRequirementKind.Expertise: result = CompareNumber(KnowledgeQuery.Expertise(domainId, pawn, requirement.trackId).amount, requirement.minimum, requirement.comparison); break;
                case KnowledgeRequirementKind.RelatedKnowledge: result = CompareNumber(facet.derivedAmount, requirement.minimum, requirement.comparison); break;
                case KnowledgeRequirementKind.ClaimExists: result = KnowledgeClaimService.Snapshot(domainId, subjectId, requirement.facetId, requirement.claimId, pawn, scope, context).observationCount > 0; break;
                case KnowledgeRequirementKind.ClaimConfidence: result = CompareNumber(KnowledgeClaimService.Snapshot(domainId, subjectId, requirement.facetId, requirement.claimId, pawn, scope, context).effectiveConfidence, requirement.minimum, requirement.comparison); break;
                case KnowledgeRequirementKind.ClaimValue: result = CompareClaimValue(KnowledgeClaimService.Snapshot(domainId, subjectId, requirement.facetId, requirement.claimId, pawn, scope, context).value, requirement.value, requirement.comparison); break;
                case KnowledgeRequirementKind.Documentation: result = KnowledgeQuery.Subject(domainId, subjectId, pawn, scope).documented; break;
                case KnowledgeRequirementKind.Insight: result = GameComponent_KnowledgeFramework.Current?.InsightActivated(domainId, subjectId, requirement.insightId, pawn, scope == KnowledgeScope.Colony) == true; break;
                case KnowledgeRequirementKind.Milestone: result = KnowledgeMilestoneService.IsCompleted(domainId, subjectId, requirement.milestoneTrackId, requirement.milestoneId, pawn, context); break;
                case KnowledgeRequirementKind.Custom: result = KnowledgeV3CustomRequirements.Evaluate(requirement.customId, domainId, subjectId, pawn, scope, context); break;
                default: result = false; break;
            }
            return requirement.negate ? !result : result;
        }

        internal static bool CompareNumber(float actual, float expected, KnowledgeRequirementComparison comparison)
        {
            switch (comparison)
            {
                case KnowledgeRequirementComparison.Equal: return Math.Abs(actual - expected) < 0.0001f;
                case KnowledgeRequirementComparison.NotEqual: return Math.Abs(actual - expected) >= 0.0001f;
                case KnowledgeRequirementComparison.GreaterThan: return actual > expected;
                case KnowledgeRequirementComparison.LessThan: return actual < expected;
                case KnowledgeRequirementComparison.LessOrEqual: return actual <= expected;
                default: return actual >= expected;
            }
        }

        internal static bool CompareClaimValue(KnowledgeClaimValue actual, KnowledgeClaimValue expected, KnowledgeRequirementComparison comparison)
        {
            if (comparison == KnowledgeRequirementComparison.Exists) return actual != null;
            if (actual == null || expected == null || actual.type != expected.type) return comparison == KnowledgeRequirementComparison.NotEqual;
            if (actual.type == KnowledgeClaimValueType.Integer || actual.type == KnowledgeClaimValueType.Float || actual.type == KnowledgeClaimValueType.Percentage)
            {
                float left = actual.type == KnowledgeClaimValueType.Integer ? actual.integerValue : actual.numericValue;
                float right = expected.type == KnowledgeClaimValueType.Integer ? expected.integerValue : expected.numericValue;
                return CompareNumber(left, right, comparison);
            }
            if (actual.type == KnowledgeClaimValueType.SetOfIds)
            {
                HashSet<string> left = new HashSet<string>(actual.setValues ?? new List<string>());
                HashSet<string> right = new HashSet<string>(expected.setValues ?? new List<string>());
                if (comparison == KnowledgeRequirementComparison.Contains) return right.IsSubsetOf(left);
                if (comparison == KnowledgeRequirementComparison.Intersects) return left.Overlaps(right);
            }
            bool equal = actual.StableKey() == expected.StableKey();
            return comparison == KnowledgeRequirementComparison.NotEqual ? !equal : equal;
        }
    }

    internal static class KnowledgeV3CustomRequirements
    {
        private static readonly Dictionary<string, Func<string, string, Pawn, KnowledgeScope, KnowledgeContextKey, bool>> Evaluators =
            new Dictionary<string, Func<string, string, Pawn, KnowledgeScope, KnowledgeContextKey, bool>>(StringComparer.Ordinal);

        internal static bool Register(string id, Func<string, string, Pawn, KnowledgeScope, KnowledgeContextKey, bool> evaluator, bool replace = false)
        {
            if (id.NullOrEmpty() || evaluator == null || Evaluators.ContainsKey(id) && !replace) return false;
            Evaluators[id] = evaluator;
            return true;
        }

        internal static bool Evaluate(string id, string domainId, string subjectId, Pawn pawn, KnowledgeScope scope, KnowledgeContextKey context)
        {
            if (id.NullOrEmpty() || !Evaluators.TryGetValue(id, out Func<string, string, Pawn, KnowledgeScope, KnowledgeContextKey, bool> evaluator)) return false;
            try { return evaluator(domainId, subjectId, pawn, scope, context); }
            catch (Exception exception) { KnowledgeLog.ErrorOnce("requirement-custom:" + id, "A custom requirement failed.", exception); return false; }
        }
    }

    internal static class KnowledgeV3Runtime
    {
        internal const int ExpandedTransactionLimit = 1024;

        internal static KnowledgeContextKey ContextFor(KnowledgeObservation observation)
        {
            if (observation == null) return KnowledgeContextKey.Empty;
            if (!observation.context.IsEmpty) return observation.context;
            if (!observation.contextTypeId.NullOrEmpty() && !observation.contextId.NullOrEmpty()) return new KnowledgeContextKey(observation.contextTypeId, observation.contextId);
            if (!observation.contextId.NullOrEmpty()) return new KnowledgeContextKey("legacy", observation.contextId);
            return KnowledgeContextKey.Empty;
        }

        internal static bool PrepareTransaction(KnowledgeTransaction input, out KnowledgeTransaction prepared, out string error)
        {
            prepared = new KnowledgeTransaction { source = input?.source, transactionId = input?.transactionId, notify = input?.notify ?? true };
            error = null;
            if (input == null) { error = "The transaction is null."; return false; }
            List<KnowledgeObservation> expanded = new List<KnowledgeObservation>();
            HashSet<string> plannedAccrual = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < input.Observations.Count; i++)
            {
                KnowledgeObservation observation = input.Observations[i];
                KnowledgeSchema schema = KnowledgeRegistry.Schema(observation?.domainId);
                if (observation == null) { error = "Observation " + i + " is null."; return false; }
                List<KnowledgeObservation> recipe = ExpandRecipe(observation, schema);
                foreach (KnowledgeObservation candidate in recipe)
                {
                    if (candidate.witnessDistribution == null) candidate.witnessDistribution = schema?.Observation(candidate.observationId)?.witnessDistribution;
                    if (!PrepareMeasurements(candidate, out error)) return false;
                    if (!ApplyNoveltyPreview(candidate, schema, plannedAccrual, out error)) return false;
                    expanded.Add(candidate);
                    if (expanded.Count > ExpandedTransactionLimit) { error = "Expanded observation recipe exceeds the safe transaction limit of " + ExpandedTransactionLimit + "."; return false; }
                    foreach (KnowledgeObservation witness in ExpandWitnesses(candidate, schema))
                    {
                        if (!PrepareMeasurements(witness, out error)) return false;
                        if (!ApplyNoveltyPreview(witness, schema, plannedAccrual, out error)) return false;
                        expanded.Add(witness);
                        if (expanded.Count > ExpandedTransactionLimit) { error = "Witness distribution exceeds the safe transaction limit of " + ExpandedTransactionLimit + "."; return false; }
                    }
                }
            }
            prepared.AddRange(expanded);
            KnowledgeDiagnostics.RecordFanout(Math.Max(0, expanded.Count - input.Observations.Count), Math.Max(0, expanded.Count - input.Observations.Count));
            return true;
        }

        internal static void ApplyObservation(GameComponent_KnowledgeFramework component, ValidatedObservation value, bool colony)
        {
            KnowledgeObservation input = value.input;
            KnowledgeContextKey context = ContextFor(input);
            if (!context.IsEmpty)
            {
                KnowledgeContextFacetStateRecord facet = component.ContextFacetV3(value.domainId, value.subjectId, value.facetId, colony ? null : input.observer,
                    colony, context, true);
                facet.amount = Math.Min(100000000f, facet.amount + value.knowledge);
                if (input.disposition == KnowledgeEvidenceDisposition.Supporting) facet.supportingEvidence = Math.Min(100000000f, facet.supportingEvidence + value.evidenceWeight);
                else if (input.disposition == KnowledgeEvidenceDisposition.Contradictory) facet.contradictoryEvidence = Math.Min(100000000f, facet.contradictoryEvidence + value.evidenceWeight);
                facet.evidenceCount = Math.Min(100000000, facet.evidenceCount + 1);
                if (input.success) facet.successCount = Math.Min(100000000, facet.successCount + 1); else facet.failureCount = Math.Min(100000000, facet.failureCount + 1);
                facet.lastTick = Find.TickManager?.TicksGame ?? 0;
                facet.revision = component.GlobalRevision;
            }
            foreach (KnowledgeMeasurement measurement in input.claimMeasurements ?? Array.Empty<KnowledgeMeasurement>())
            {
                KnowledgeMeasurement valueToApply = measurement.Clone();
                if (valueToApply.observer == null) valueToApply.observer = input.observer;
                if (valueToApply.domainId.NullOrEmpty()) valueToApply.domainId = value.domainId;
                if (valueToApply.subjectId.NullOrEmpty()) valueToApply.subjectId = value.subjectId;
                if (valueToApply.facetId.NullOrEmpty()) valueToApply.facetId = value.facetId;
                if (valueToApply.source.NullOrEmpty()) valueToApply.source = input.source;
                if (valueToApply.sourceInstanceId.NullOrEmpty()) valueToApply.sourceInstanceId = input.sourceInstanceId;
                if (valueToApply.reasonId.NullOrEmpty()) valueToApply.reasonId = input.reasonId;
                if (valueToApply.methodId.NullOrEmpty()) valueToApply.methodId = input.methodId;
                if (valueToApply.summary.NullOrEmpty()) valueToApply.summary = input.summary;
                if (valueToApply.context.IsEmpty) valueToApply.context = context;
                if (valueToApply.scope == KnowledgeScope.Personal && colony) valueToApply.scope = KnowledgeScope.Colony;
                KnowledgeClaimService.Apply(component, valueToApply, input);
            }
            KnowledgeAccrualService.Commit(component, input, value.schema);
            if (!input.sharedExpertiseNamespaceId.NullOrEmpty() && value.expertise > 0f)
                KnowledgeSharedExpertiseService.Apply(input.sharedExpertiseNamespaceId, value.domainId, value.trackId, input.observer,
                    value.expertise, input.sharedExpertiseWeight);
        }

        private static List<KnowledgeObservation> ExpandRecipe(KnowledgeObservation input, KnowledgeSchema schema)
        {
            KnowledgeObservationDef definition = schema?.Observation(input.observationId);
            List<KnowledgeObservationOutcome> outcomes = input.success ? definition?.successOutcomes : definition?.failureOutcomes;
            if (outcomes == null || outcomes.Count == 0) return new List<KnowledgeObservation> { CloneObservation(input) };
            List<KnowledgeObservation> result = new List<KnowledgeObservation>();
            foreach (KnowledgeObservationOutcome outcome in outcomes.Where(item => item != null))
            {
                KnowledgeObservation value = CloneObservation(input);
                value.observationId = input.observationId;
                value.facetId = outcome.facetId ?? input.facetId;
                value.directKnowledge = KnowledgeMath.NonNegativeFiniteOr(outcome.knowledge, 0f);
                value.directFamiliarity = KnowledgeMath.NonNegativeFiniteOr(outcome.familiarity, 0f);
                value.directExpertise = KnowledgeMath.NonNegativeFiniteOr(outcome.expertise, 0f);
                value.expertiseTrackId = outcome.expertiseTrackId ?? input.expertiseTrackId;
                KnowledgeExpertiseOutcome sharedOutcome = (definition?.expertiseOutcomes ?? new List<KnowledgeExpertiseOutcome>())
                    .FirstOrDefault(item => item != null && !item.namespaceId.NullOrEmpty());
                if (sharedOutcome != null)
                {
                    value.sharedExpertiseNamespaceId = sharedOutcome.namespaceId;
                    value.sharedExpertiseWeight = sharedOutcome.namespaceWeight;
                }
                value.targetColony |= outcome.targetColony;
                value.documented |= outcome.document;
                value.disposition = outcome.disposition;
                value.quality *= KnowledgeMath.Clamp01Finite(outcome.confidenceFactor);
                value.sourceReliability *= Math.Min(10f, KnowledgeMath.NonNegativeFiniteOr(outcome.evidenceWeight, 1f));
                value.claimMeasurements = (outcome.claimMeasurements ?? new List<KnowledgeMeasurement>()).Select(item => item?.Clone()).Where(item => item != null).ToList();
                value.suppressConfiguredKnowledge = true;
                result.Add(value);
            }
            string expertiseFacetId = input.facetId.NullOrEmpty()
                ? (outcomes ?? new List<KnowledgeObservationOutcome>()).Select(item => item?.facetId).FirstOrDefault(item => !item.NullOrEmpty())
                    ?? definition?.facetIds?.FirstOrDefault()
                    ?? schema?.facets.FirstOrDefault()?.id
                : input.facetId;
            foreach (KnowledgeExpertiseOutcome outcome in definition?.expertiseOutcomes ?? new List<KnowledgeExpertiseOutcome>())
            {
                if (outcome == null || outcome.expertise <= 0f) continue;
                result.Add(new KnowledgeObservation
                {
                    observer = input.observer,
                    domainId = input.domainId,
                    subjectId = input.subjectId,
                    facetId = expertiseFacetId,
                    expertiseTrackId = outcome.trackId,
                    directExpertise = outcome.expertise,
                    source = input.source,
                    sourceInstanceId = input.sourceInstanceId,
                    reasonId = input.reasonId,
                    methodId = input.methodId,
                    context = ContextFor(input),
                    success = input.success,
                    quality = input.quality,
                    novelty = input.novelty,
                    repetition = input.repetition,
                    sourceReliability = input.sourceReliability,
                    environmentalDifficulty = input.environmentalDifficulty,
                    suppressConfiguredKnowledge = true,
                    witnessDistribution = new KnowledgeWitnessDistribution(),
                    sharedExpertiseNamespaceId = outcome.namespaceId,
                    sharedExpertiseWeight = outcome.namespaceWeight,
                    claimMeasurements = outcome.namespaceId.NullOrEmpty() ? null : new List<KnowledgeMeasurement>()
                });
            }
            return result;
        }

        private static IEnumerable<KnowledgeObservation> ExpandWitnesses(KnowledgeObservation input, KnowledgeSchema schema)
        {
            KnowledgeWitnessDistribution distribution = input.witnessDistribution;
            if (distribution == null || distribution.policy == KnowledgeWitnessDistributionPolicy.ObserverOnly || input.witnesses == null) return Enumerable.Empty<KnowledgeObservation>();
            List<Pawn> recipients = input.witnesses.Where(item => item != null && (distribution.includeObserver || item != input.observer)).Distinct()
                .Take(Math.Max(0, Math.Min(64, distribution.maximumRecipients))).ToList();
            if (distribution.policy == KnowledgeWitnessDistributionPolicy.ColonyDirect)
            {
                return new[] { CloneForColony(input) };
            }
            if (distribution.policy == KnowledgeWitnessDistributionPolicy.Custom && !distribution.customId.NullOrEmpty())
                recipients = KnowledgeWitnessService.Recipients(distribution.customId, input, recipients).ToList();
            float efficiency = distribution.policy == KnowledgeWitnessDistributionPolicy.WitnessesFull ? 1f : Math.Max(0f, Math.Min(1f, distribution.efficiency));
            return recipients.Select(witness => CloneForWitness(input, witness, efficiency, distribution.expertiseEfficiency, distribution.confidenceEfficiency)).ToList();
        }

        private static KnowledgeObservation CloneForWitness(KnowledgeObservation input, Pawn witness, float efficiency, float expertiseEfficiency, float confidenceEfficiency)
        {
            KnowledgeObservation value = CloneObservation(input);
            value.observer = witness;
            value.directKnowledge *= efficiency;
            value.directExpertise *= Math.Max(0f, Math.Min(1f, expertiseEfficiency));
            value.directFamiliarity *= efficiency;
            value.quality *= Math.Max(0f, Math.Min(1f, confidenceEfficiency));
            value.shareable = false;
            value.witnesses = null;
            value.witnessDistribution = new KnowledgeWitnessDistribution();
            if (value.claimMeasurements != null)
                value.claimMeasurements = value.claimMeasurements.Select(item =>
                {
                    KnowledgeMeasurement measurement = item.Clone();
                    measurement.observer = witness;
                    measurement.quality *= confidenceEfficiency;
                    measurement.evidenceWeight *= efficiency;
                    return measurement;
                }).ToList();
            return value;
        }

        private static KnowledgeObservation CloneForColony(KnowledgeObservation input)
        {
            KnowledgeObservation value = CloneObservation(input);
            value.observer = null;
            value.targetColony = true;
            value.shareable = false;
            value.witnesses = null;
            value.witnessDistribution = new KnowledgeWitnessDistribution();
            if (value.claimMeasurements != null) value.claimMeasurements = value.claimMeasurements.Select(item => { KnowledgeMeasurement copy = item.Clone(); copy.scope = KnowledgeScope.Colony; copy.observer = null; return copy; }).ToList();
            return value;
        }

        private static KnowledgeObservation CloneObservation(KnowledgeObservation input)
        {
            return new KnowledgeObservation
            {
                observer = input.observer,
                domainId = input.domainId,
                subjectId = input.subjectId,
                facetId = input.facetId,
                observationId = input.observationId,
                methodId = input.methodId,
                quality = input.quality,
                novelty = input.novelty,
                repetition = input.repetition,
                success = input.success,
                environmentalDifficulty = input.environmentalDifficulty,
                sourceReliability = input.sourceReliability,
                disposition = input.disposition,
                witnesses = input.witnesses?.ToList(),
                shareable = input.shareable,
                documented = input.documented,
                targetColony = input.targetColony,
                reasonId = input.reasonId,
                source = input.source,
                sourceInstanceId = input.sourceInstanceId,
                contextId = input.contextId,
                contextTypeId = input.contextTypeId,
                context = input.context,
                specimenId = input.specimenId,
                summary = input.summary,
                metadata = input.metadata,
                claimMeasurements = input.claimMeasurements?.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                directKnowledge = input.directKnowledge,
                directExpertise = input.directExpertise,
                directFamiliarity = input.directFamiliarity,
                expertiseTrackId = input.expertiseTrackId,
                suppressConfiguredKnowledge = input.suppressConfiguredKnowledge,
                notify = input.notify,
                witnessDistribution = input.witnessDistribution,
                sharedExpertiseNamespaceId = input.sharedExpertiseNamespaceId,
                sharedExpertiseWeight = input.sharedExpertiseWeight
            };
        }

        private static bool PrepareMeasurements(KnowledgeObservation input, out string error)
        {
            error = null;
            KnowledgeContextKey context = ContextFor(input);
            if (!context.IsEmpty && KnowledgeContextRegistry.Chain(context, KnowledgeContextFallbackMode.ParentThenGlobal).Count > 65)
            { error = "Context parent chain exceeds the safe limit."; return false; }
            foreach (KnowledgeMeasurement measurement in input.claimMeasurements ?? Array.Empty<KnowledgeMeasurement>())
            {
                if (measurement.context.IsEmpty) measurement.context = context;
                if (measurement.observer == null) measurement.observer = input.observer;
                if (measurement.scope == KnowledgeScope.Personal && input.targetColony) measurement.scope = KnowledgeScope.Colony;
                if (!KnowledgeClaimService.ValidateMeasurement(measurement, input, out error)) return false;
            }
            return true;
        }

        private static bool ApplyNoveltyPreview(KnowledgeObservation input, KnowledgeSchema schema, HashSet<string> planned, out string error)
        {
            error = null;
            KnowledgeObservationDef definition = schema?.Observation(input.observationId);
            KnowledgeAccrualPolicy policy = definition?.accrualPolicy;
            if (policy == null) return true;
            if (!KnowledgeAccrualService.Preview(input, policy, planned, out float factor, out error)) return false;
            input.novelty *= factor;
            return KnowledgeMath.IsFinite(input.novelty);
        }
    }

    public static class KnowledgeWitnessService
    {
        private static readonly Dictionary<string, Func<KnowledgeObservation, IReadOnlyList<Pawn>, IReadOnlyList<Pawn>>> Filters =
            new Dictionary<string, Func<KnowledgeObservation, IReadOnlyList<Pawn>, IReadOnlyList<Pawn>>>(StringComparer.Ordinal);

        public static bool RegisterFilter(string id, Func<KnowledgeObservation, IReadOnlyList<Pawn>, IReadOnlyList<Pawn>> filter, bool replace = false)
        {
            if (id.NullOrEmpty() || filter == null || Filters.ContainsKey(id) && !replace) return false;
            Filters[id] = filter;
            return true;
        }

        internal static IReadOnlyList<Pawn> Recipients(string id, KnowledgeObservation input, IReadOnlyList<Pawn> candidates)
        {
            if (!Filters.TryGetValue(id, out Func<KnowledgeObservation, IReadOnlyList<Pawn>, IReadOnlyList<Pawn>> filter)) return candidates;
            try { return (filter(input, candidates) ?? Array.Empty<Pawn>()).Where(item => item != null).Distinct().Take(64).ToList(); }
            catch (Exception exception) { KnowledgeLog.ErrorOnce("witness-filter:" + id, "A witness filter failed.", exception); return Array.Empty<Pawn>(); }
        }
    }

    internal static class KnowledgeAccrualService
    {
        internal static bool Preview(KnowledgeObservation input, KnowledgeAccrualPolicy policy, HashSet<string> planned, out float factor, out string error)
        {
            factor = 1f;
            error = null;
            if (policy == null) return true;
            if (policy.stateLimit < 0 || policy.stateLimit > 4096 || policy.cooldownTicks < 0 || policy.dailyCap < 0 || policy.lifetimeCap < 0 ||
                !KnowledgeMath.IsFinite(policy.diminishingReturns) || policy.diminishingReturns < 0f || policy.diminishingReturns > 1f)
            { error = "Accrual policy has invalid or unbounded limits."; return false; }
            string key = Key(input, policy);
            if (key.NullOrEmpty()) return true;
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            KnowledgeAccrualStateRecord record = component?.AccrualV3(key, false);
            int existing = record?.count ?? 0;
            int projected = existing + (planned.Contains(key) ? 1 : 0);
            if (policy.uniquePerSourceInstance && !input.sourceInstanceId.NullOrEmpty() && (existing > 0 || planned.Contains(key)) ||
                policy.uniquePerPawnAndSourceInstance && !input.sourceInstanceId.NullOrEmpty() && (existing > 0 || planned.Contains(key)) ||
                policy.uniquePerSubjectAndContext && (existing > 0 || planned.Contains(key)))
            { factor = 0f; planned.Add(key); return true; }
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (record != null && policy.cooldownTicks > 0 && tick - record.lastTick < policy.cooldownTicks) factor = 0f;
            if (policy.lifetimeCap > 0 && projected >= policy.lifetimeCap) factor = 0f;
            if (policy.dailyCap > 0 && record != null && record.day == tick / 60000 && record.dailyCount >= policy.dailyCap) factor = 0f;
            if (existing == 0) factor *= Math.Max(0f, policy.firstObservationBonus);
            if (input.success && existing == 0) factor *= Math.Max(0f, policy.firstSuccessBonus);
            if (!input.success && existing == 0) factor *= Math.Max(0f, policy.firstFailureBonus);
            if (existing > 0 && policy.diminishingReturns > 0f) factor *= (float)Math.Pow(1f - policy.diminishingReturns, existing);
            planned.Add(key);
            return KnowledgeMath.IsFinite(factor);
        }

        internal static void Commit(GameComponent_KnowledgeFramework component, KnowledgeObservation input, KnowledgeSchema schema)
        {
            KnowledgeObservationDef definition = schema?.Observation(input.observationId);
            KnowledgeAccrualPolicy policy = definition?.accrualPolicy;
            if (policy == null) return;
            string key = Key(input, policy);
            if (key.NullOrEmpty()) return;
            KnowledgeAccrualStateRecord record = component.AccrualV3(key, true);
            int tick = Find.TickManager?.TicksGame ?? 0;
            int day = tick / 60000;
            if (record.day != day) record.dailyCount = 0;
            record.day = day;
            record.count = Math.Min(100000000, record.count + 1);
            record.dailyCount = Math.Min(100000000, record.dailyCount + 1);
            record.lastTick = tick;
            record.lastSource = input.source;
            record.sourceInstanceId = input.sourceInstanceId;
            record.specimenId = input.specimenId;
            record.contextKey = KnowledgeV3Runtime.ContextFor(input).ToString();
            component.TouchV3();
        }

        private static string Key(KnowledgeObservation input, KnowledgeAccrualPolicy policy)
        {
            if (input == null) return null;
            if ((policy.uniquePerSourceInstance || policy.uniquePerPawnAndSourceInstance) && input.sourceInstanceId.NullOrEmpty()) return null;
            List<string> parts = new List<string> { input.domainId, input.subjectId };
            if (policy.uniquePerPawnAndSourceInstance) parts.Add((input.observer?.thingIDNumber ?? 0).ToString());
            if (policy.uniquePerSourceInstance) parts.Add(input.sourceInstanceId ?? string.Empty);
            if (policy.uniquePerSubjectAndContext) parts.Add(KnowledgeV3Runtime.ContextFor(input).ToString());
            if (!policy.uniquePerSourceInstance && !policy.uniquePerPawnAndSourceInstance && !policy.uniquePerSubjectAndContext) return null;
            return string.Join("\n", parts.ToArray());
        }
    }
}
