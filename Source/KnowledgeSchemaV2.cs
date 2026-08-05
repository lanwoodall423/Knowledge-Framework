using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    public sealed class KnowledgeFacetSchema
    {
        public readonly string id;
        public readonly string label;
        public readonly string description;
        public readonly float completenessAmount;
        public readonly bool personallyKnowable;
        public readonly bool documentable;
        public readonly bool shareable;
        public readonly bool forgettable;
        public readonly bool hiddenUntilRevealed;
        public readonly bool approximateWhenUncertain;
        public readonly float revealKnowledge;
        public readonly float revealConfidence;
        public readonly IReadOnlyList<string> relatedFacetIds;
        public readonly IReadOnlyList<string> claimIds;

        internal KnowledgeFacetSchema(KnowledgeFacetDef def)
        {
            id = def.StableId;
            label = def.LabelCap;
            description = def.description ?? string.Empty;
            completenessAmount = KnowledgeMath.PositiveFiniteOr(def.completenessAmount, 100f);
            personallyKnowable = def.personallyKnowable;
            documentable = def.documentable;
            shareable = def.shareable;
            forgettable = def.forgettable;
            hiddenUntilRevealed = def.hiddenUntilRevealed;
            approximateWhenUncertain = def.approximateWhenUncertain;
            revealKnowledge = KnowledgeMath.NonNegativeFiniteOr(def.revealKnowledge, 0f);
            revealConfidence = KnowledgeMath.Clamp01Finite(def.revealConfidence);
            relatedFacetIds = ReadOnly(def.relatedFacetIds);
            claimIds = ReadOnly(def.claimIds);
        }

        internal KnowledgeFacetSchema()
        {
            id = KnowledgeSchema.DefaultFacetId;
            label = "General";
            description = string.Empty;
            completenessAmount = 100f;
            personallyKnowable = true;
            documentable = true;
            shareable = true;
            approximateWhenUncertain = true;
            relatedFacetIds = Array.Empty<string>();
            claimIds = Array.Empty<string>();
        }

        private static IReadOnlyList<string> ReadOnly(IEnumerable<string> values) =>
            new ReadOnlyCollection<string>((values ?? Enumerable.Empty<string>()).Where(value => !value.NullOrEmpty()).Distinct().ToList());
    }

    public sealed class KnowledgeStageSchema
    {
        public readonly string id;
        public readonly string label;
        public readonly string description;
        public readonly int order;
        public readonly float minimumKnowledge;
        public readonly float minimumConfidence;
        public readonly bool documented;
        public readonly KnowledgeRequirementGroup requirementGroup;
        public readonly bool allowRegression;
        public readonly bool contextSensitive;

        internal KnowledgeStageSchema(KnowledgeStageDef def)
        {
            id = def.defName;
            label = def.LabelCap;
            description = def.description ?? string.Empty;
            order = def.order;
            minimumKnowledge = KnowledgeMath.NonNegativeFiniteOr(def.minimumKnowledge, 0f);
            minimumConfidence = KnowledgeMath.Clamp01Finite(def.minimumConfidence);
            documented = def.documented;
            requirementGroup = def.requirementGroup;
            allowRegression = def.allowRegression;
            contextSensitive = def.contextSensitive;
        }
    }

    public sealed class KnowledgeExpertiseTrackSchema
    {
        public readonly string id;
        public readonly string label;
        public readonly string description;
        public readonly KnowledgeRankThresholds ranks;

        internal KnowledgeExpertiseTrackSchema(KnowledgeExpertiseTrackDef def)
        {
            id = def.StableId;
            label = def.LabelCap;
            description = def.description ?? string.Empty;
            ranks = new KnowledgeRankThresholds(def.adept, def.expert, def.master);
        }
    }

    public sealed class KnowledgeSubjectSnapshot
    {
        public readonly string domainId;
        public readonly string id;
        public readonly string label;
        public readonly string description;
        public readonly string unidentifiedLabel;
        public readonly string unidentifiedDescription;
        public readonly string iconPath;
        public readonly Def sourceDef;
        public readonly string templateSubjectId;
        public readonly float templateKnowledgeCoefficient;
        public readonly float templateConfidenceCoefficient;
        public readonly IReadOnlyList<string> categoryIds;
        public readonly int sortOrder;
        public readonly bool dynamic;
        public readonly string archetypeId;
        public readonly IReadOnlyList<string> applicableFacetIds;
        public readonly IReadOnlyList<string> applicableClaimIds;
        public readonly KnowledgeSubjectState state;

        internal KnowledgeSubjectSnapshot(string domainId, KnowledgeSubjectDef def)
        {
            this.domainId = domainId;
            id = def.StableId;
            label = def.LabelCap;
            description = def.description ?? string.Empty;
            unidentifiedLabel = def.unidentifiedLabel;
            unidentifiedDescription = def.unidentifiedDescription;
            iconPath = def.iconPath;
            sourceDef = def;
            templateSubjectId = def.templateSubjectId;
            templateKnowledgeCoefficient = KnowledgeMath.Clamp01Finite(def.templateKnowledgeCoefficient);
            templateConfidenceCoefficient = KnowledgeMath.Clamp01Finite(def.templateConfidenceCoefficient);
            categoryIds = ReadOnly(def.categoryIds);
            sortOrder = def.sortOrder;
            archetypeId = def.archetypeId;
            applicableFacetIds = ReadOnly(def.applicableFacetIds);
            applicableClaimIds = ReadOnly(def.applicableClaimIds);
            state = def.state;
        }

        internal KnowledgeSubjectSnapshot(string domainId, KnowledgeSubjectRegistration value)
        {
            this.domainId = domainId;
            id = value.id;
            label = value.label ?? value.id;
            description = value.description ?? string.Empty;
            unidentifiedLabel = value.unidentifiedLabel;
            unidentifiedDescription = value.unidentifiedDescription;
            iconPath = value.iconPath;
            sourceDef = value.sourceDef;
            templateSubjectId = value.templateSubjectId;
            templateKnowledgeCoefficient = KnowledgeMath.Clamp01Finite(value.templateKnowledgeCoefficient);
            templateConfidenceCoefficient = KnowledgeMath.Clamp01Finite(value.templateConfidenceCoefficient);
            categoryIds = ReadOnly(value.categoryIds);
            sortOrder = value.sortOrder;
            dynamic = true;
            archetypeId = value.archetypeId;
            applicableFacetIds = ReadOnly(value.applicableFacetIds);
            applicableClaimIds = ReadOnly(value.applicableClaimIds);
            state = value.state;
        }

        internal KnowledgeSubjectSnapshot(KnowledgeSubjectSnapshot value, string domainId, string subjectId)
        {
            this.domainId = domainId;
            id = subjectId;
            label = value.label;
            description = value.description;
            unidentifiedLabel = value.unidentifiedLabel;
            unidentifiedDescription = value.unidentifiedDescription;
            iconPath = value.iconPath;
            sourceDef = value.sourceDef;
            templateSubjectId = value.templateSubjectId;
            templateKnowledgeCoefficient = value.templateKnowledgeCoefficient;
            templateConfidenceCoefficient = value.templateConfidenceCoefficient;
            categoryIds = value.categoryIds;
            sortOrder = value.sortOrder;
            dynamic = value.dynamic;
            archetypeId = value.archetypeId;
            applicableFacetIds = value.applicableFacetIds;
            applicableClaimIds = value.applicableClaimIds;
            state = value.state;
        }

        private static IReadOnlyList<string> ReadOnly(IEnumerable<string> values) =>
            new ReadOnlyCollection<string>((values ?? Enumerable.Empty<string>()).Where(value => !value.NullOrEmpty()).Distinct().ToList());
    }

    /// <summary>Immutable runtime schema built once from Defs or an explicit dynamic registration.</summary>
    public sealed class KnowledgeSchema
    {
        public const string DefaultFacetId = "default";
        public const string DefaultExpertiseTrackId = "default";

        public readonly string id;
        public readonly string label;
        public readonly string description;
        public readonly bool uncertaintyEnabled;
        public readonly bool familiarityEnabled;
        public readonly KnowledgeSharingModel sharingModel;
        public readonly KnowledgeStageAggregationMode stageAggregationMode;
        public readonly int sortOrder;
        public readonly int provenanceLimit;
        public readonly int evidenceAggregateLimit;
        public readonly int priority;
        public readonly string source;
        public readonly IReadOnlyList<KnowledgeFacetSchema> facets;
        public readonly IReadOnlyList<KnowledgeStageSchema> stages;
        public readonly IReadOnlyList<KnowledgeExpertiseTrackSchema> expertiseTracks;
        public readonly IReadOnlyList<KnowledgeObservationDef> observations;
        public readonly IReadOnlyList<KnowledgeRevealDef> reveals;
        public readonly IReadOnlyList<KnowledgeEffectDef> effects;
        public readonly IReadOnlyList<KnowledgeInsightDef> insights;
        public readonly IReadOnlyList<KnowledgeRelationshipDef> relationships;
        public readonly KnowledgeTransmissionDef transmission;
        public readonly IReadOnlyList<KnowledgeClaimDef> claims;
        public readonly IReadOnlyList<KnowledgeSubjectArchetypeDef> archetypes;
        public readonly IReadOnlyList<KnowledgeMilestoneTrackDef> milestoneTracks;
        public readonly IReadOnlyList<KnowledgeExpertiseNamespaceDef> expertiseNamespaces;

        private readonly Dictionary<string, KnowledgeFacetSchema> facetsById;
        private readonly Dictionary<string, KnowledgeStageSchema> stagesById;
        private readonly Dictionary<string, KnowledgeExpertiseTrackSchema> tracksById;
        private readonly Dictionary<string, KnowledgeObservationDef> observationsById;
        private readonly Dictionary<string, KnowledgeClaimDef> claimsById;
        private readonly Dictionary<string, KnowledgeSubjectArchetypeDef> archetypesById;
        private readonly Func<string, KnowledgeSubjectRegistration> dynamicResolver;
        private readonly Func<IEnumerable<KnowledgeSubjectRegistration>> dynamicSource;

        internal KnowledgeSchema(KnowledgeDomainRegistration value, int priority, string source)
        {
            id = value.id;
            label = value.label ?? value.id;
            description = value.description ?? string.Empty;
            uncertaintyEnabled = value.enableUncertainty;
            familiarityEnabled = value.enableFamiliarity;
            sharingModel = value.sharingModel;
            stageAggregationMode = value.stageAggregationMode;
            sortOrder = value.sortOrder;
            provenanceLimit = Math.Max(0, Math.Min(64, value.provenanceLimit));
            evidenceAggregateLimit = Math.Max(8, Math.Min(512, value.evidenceAggregateLimit));
            this.priority = priority;
            this.source = source ?? value.source ?? "dynamic";
            facets = BuildFacets(value.facets);
            stages = ReadOnly((value.stages ?? Array.Empty<KnowledgeStageDef>()).Where(item => item != null)
                .OrderBy(item => item.order).ThenBy(item => item.defName).Select(item => new KnowledgeStageSchema(item)));
            expertiseTracks = ReadOnly((value.expertiseTracks ?? Array.Empty<KnowledgeExpertiseTrackDef>()).Where(item => item != null)
                .Select(item => new KnowledgeExpertiseTrackSchema(item)));
            observations = ReadOnly(value.observations);
            reveals = ReadOnly(value.reveals);
            effects = ReadOnly(value.effects, item => item.priority, item => item.defName);
            insights = ReadOnly(value.insights);
            relationships = ReadOnly(value.relationships);
            transmission = value.transmission;
            claims = ReadOnly(value.claims);
            archetypes = ReadOnly(value.archetypes);
            milestoneTracks = ReadOnly(value.milestoneTracks);
            expertiseNamespaces = ReadOnly(value.expertiseNamespaces);
            dynamicResolver = value.subjectResolver;
            dynamicSource = value.subjectSource;
            facetsById = facets.Where(item => !item.id.NullOrEmpty()).GroupBy(item => item.id)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            stagesById = stages.Where(item => !item.id.NullOrEmpty()).GroupBy(item => item.id)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            tracksById = expertiseTracks.Where(item => !item.id.NullOrEmpty()).GroupBy(item => item.id)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            observationsById = observations.Where(item => item != null).GroupBy(item => item.StableId)
                .Where(group => !group.Key.NullOrEmpty())
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            claimsById = claims.Where(item => item != null).GroupBy(item => item.StableId)
                .Where(group => !group.Key.NullOrEmpty())
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            archetypesById = archetypes.Where(item => item != null).GroupBy(item => item.StableId)
                .Where(group => !group.Key.NullOrEmpty())
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }

        internal static KnowledgeSchema FromDef(KnowledgeDomainDef def)
        {
            return new KnowledgeSchema(new KnowledgeDomainRegistration
            {
                id = def.StableId,
                label = def.LabelCap,
                description = def.description,
                enableUncertainty = def.enableUncertainty,
                enableFamiliarity = def.enableFamiliarity,
                sharingModel = def.sharingModel,
                stageAggregationMode = def.stageAggregationMode,
                sortOrder = def.sortOrder,
                provenanceLimit = def.provenanceLimit,
                evidenceAggregateLimit = def.evidenceAggregateLimit,
                facets = def.facets,
                stages = def.stages,
                expertiseTracks = def.expertiseTracks,
                observations = def.observations,
                reveals = def.reveals,
                effects = def.effects,
                insights = def.insights,
                relationships = def.relationships,
                transmission = def.transmission,
                claims = def.claims,
                archetypes = def.archetypes,
                milestoneTracks = def.milestoneTracks,
                expertiseNamespaces = def.expertiseNamespaces,
                source = def.modContentPack?.PackageId ?? "Defs"
            }, 0, def.modContentPack?.PackageId ?? "Defs");
        }

        public KnowledgeFacetSchema Facet(string facetId)
        {
            string idToFind = facetId.NullOrEmpty() ? DefaultFacetId : facetId;
            return facetsById.TryGetValue(idToFind, out KnowledgeFacetSchema value) ? value : null;
        }

        public KnowledgeStageSchema Stage(string stageId) =>
            !stageId.NullOrEmpty() && stagesById.TryGetValue(stageId, out KnowledgeStageSchema value) ? value : null;

        public KnowledgeExpertiseTrackSchema ExpertiseTrack(string trackId)
        {
            string idToFind = trackId.NullOrEmpty() ? DefaultExpertiseTrackId : trackId;
            return tracksById.TryGetValue(idToFind, out KnowledgeExpertiseTrackSchema value) ? value : null;
        }

        public KnowledgeObservationDef Observation(string observationId) =>
            !observationId.NullOrEmpty() && observationsById.TryGetValue(observationId, out KnowledgeObservationDef value) ? value : null;

        public KnowledgeClaimDef Claim(string claimId) =>
            !claimId.NullOrEmpty() && claimsById.TryGetValue(claimId, out KnowledgeClaimDef value) ? value : null;

        public KnowledgeSubjectArchetypeDef Archetype(string archetypeId) =>
            !archetypeId.NullOrEmpty() && archetypesById.TryGetValue(archetypeId, out KnowledgeSubjectArchetypeDef value) ? value : null;

        internal KnowledgeSubjectRegistration ResolveDynamic(string subjectId) => dynamicResolver?.Invoke(subjectId);
        internal IEnumerable<KnowledgeSubjectRegistration> DynamicSubjects() => dynamicSource?.Invoke() ?? Enumerable.Empty<KnowledgeSubjectRegistration>();

        private static IReadOnlyList<KnowledgeFacetSchema> BuildFacets(IEnumerable<KnowledgeFacetDef> values)
        {
            List<KnowledgeFacetSchema> result = (values ?? Enumerable.Empty<KnowledgeFacetDef>()).Where(item => item != null)
                .Select(item => new KnowledgeFacetSchema(item)).ToList();
            if (result.Count == 0) result.Add(new KnowledgeFacetSchema());
            return new ReadOnlyCollection<KnowledgeFacetSchema>(result);
        }

        private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
            new ReadOnlyCollection<T>((values ?? Enumerable.Empty<T>()).Where(item => item != null).ToList());

        private static IReadOnlyList<T> ReadOnly<T, TOrder, TThen>(IEnumerable<T> values, Func<T, TOrder> order, Func<T, TThen> then) =>
            new ReadOnlyCollection<T>((values ?? Enumerable.Empty<T>()).Where(item => item != null).OrderBy(order).ThenBy(then).ToList());
    }

    public static class KnowledgeMath
    {
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static float NonNegativeFiniteOr(float value, float fallback) => IsFinite(value) && value >= 0f ? value : fallback;
        public static float PositiveFiniteOr(float value, float fallback) => IsFinite(value) && value > 0f ? value : fallback;
        public static float Clamp01Finite(float value) => IsFinite(value) ? Math.Max(0f, Math.Min(1f, value)) : 0f;
        public static float Confidence(float support, float contradiction, bool uncertaintyEnabled)
        {
            if (!uncertaintyEnabled) return support > 0f ? 1f : 0f;
            support = NonNegativeFiniteOr(support, 0f);
            contradiction = NonNegativeFiniteOr(contradiction, 0f);
            support = Math.Min(100000000f, support);
            contradiction = Math.Min(100000000f, contradiction);
            float total = support + contradiction + 1f;
            return total > 0f ? Clamp01Finite(support / total) : 0f;
        }
    }
}
