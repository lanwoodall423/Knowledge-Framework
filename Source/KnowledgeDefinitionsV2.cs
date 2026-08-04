using System;
using System.Collections.Generic;
using Verse;

namespace KnowledgeFramework
{
    public enum KnowledgeScope
    {
        Personal,
        Colony
    }

    public enum KnowledgeSharingModel
    {
        Immediate,
        Reportable,
        Documented,
        Custom
    }

    /// <summary>
    /// Defines how a subject-wide discovery stage combines its applicable facets.
    /// LegacySumMax preserves the V1/V2 raw sum/highest-confidence calculation.
    /// Balanced normalizes facet progress to a 0-100 scale and uses a conservative,
    /// evidence-aware confidence aggregate.
    /// </summary>
    public enum KnowledgeStageAggregationMode
    {
        LegacySumMax,
        Balanced
    }

    public enum KnowledgeEvidenceDisposition
    {
        Supporting,
        Contradictory,
        Neutral
    }

    public enum KnowledgeEffectComposition
    {
        Add,
        Multiply,
        Minimum,
        Maximum,
        Override,
        Allow,
        Deny,
        Reveal,
        Action,
        Prediction
    }

    public enum KnowledgeInsightScope
    {
        Personal,
        Colony,
        Both
    }

    public enum KnowledgeRequirementKind
    {
        Knowledge,
        Confidence,
        EvidenceCount,
        SuccessCount,
        FailureCount,
        Familiarity,
        DiscoveryStage,
        Expertise,
        RelatedKnowledge,
        Insight,
        Custom,
        Completeness,
        ClaimExists,
        ClaimConfidence,
        ClaimValue,
        Documentation,
        Milestone,
        EventCount,
        [Obsolete("Context requirements are unsupported; use an explicit facet or claim requirement.")]
        Context,
        [Obsolete("RelatedClaim requirements are unsupported; use ClaimExists, ClaimConfidence, or ClaimValue.")]
        RelatedClaim
    }

    public enum KnowledgeRegistrationConflict
    {
        Reject,
        ReplaceIfHigherPriority,
        Replace
    }

    /// <summary>Configurable discovery stage. Domains choose and order their own stages.</summary>
    public sealed class KnowledgeStageDef : Def
    {
        public int order;
        public float minimumKnowledge;
        public float minimumConfidence;
        public bool documented;
        public KnowledgeRequirementGroup requirementGroup;
        public bool allowRegression;
        public bool contextSensitive;
    }

    /// <summary>A separately knowable part of a subject.</summary>
    public sealed class KnowledgeFacetDef : Def
    {
        public string stableId;
        public float completenessAmount = 100f;
        public bool personallyKnowable = true;
        public bool documentable = true;
        public bool shareable = true;
        public bool forgettable;
        public bool hiddenUntilRevealed;
        public bool approximateWhenUncertain = true;
        public float revealKnowledge;
        public float revealConfidence;
        [Obsolete("relatedFacetIds is unsupported; use explicit facet requirements or observations.")]
        public List<string> relatedFacetIds;
        public List<string> claimIds;

        public string StableId => stableId.NullOrEmpty() ? defName : stableId;
    }

    /// <summary>A procedural competence track. Expertise is never stored as subject knowledge.</summary>
    public sealed class KnowledgeExpertiseTrackDef : Def
    {
        public string stableId;
        public float adept = 100f;
        public float expert = 300f;
        public float master = 700f;

        public string StableId => stableId.NullOrEmpty() ? defName : stableId;
    }

    /// <summary>Declares how an observation is converted into evidence.</summary>
    public sealed class KnowledgeObservationDef : Def
    {
        public string stableId;
        public float baseKnowledge = 1f;
        public float baseExpertise;
        public float baseFamiliarity;
        public float failureKnowledgeFactor = 0.5f;
        public float failureExpertiseFactor = 1f;
        public bool countFailureAsEvidence = true;
        public bool shareable = true;
        public bool retainProvenance;
        public List<string> facetIds;
        public List<KnowledgeObservationOutcome> successOutcomes;
        public List<KnowledgeObservationOutcome> failureOutcomes;
        public List<KnowledgeExpertiseOutcome> expertiseOutcomes;
        public KnowledgeWitnessDistribution witnessDistribution;
        public KnowledgeAccrualPolicy accrualPolicy;
        public bool propagateContext = true;

        public string StableId => stableId.NullOrEmpty() ? defName : stableId;
    }

    public sealed class KnowledgeRevealDef : Def
    {
        public string domainId;
        public string facetId;
        public float minimumKnowledge;
        public float minimumConfidence;
        public string minimumStageId;
        public bool colony;
    }

    /// <summary>A formally composed gameplay result channel.</summary>
    public sealed class KnowledgeEffectDef : Def
    {
        public string domainId;
        public string channelId;
        public string facetId;
        public KnowledgeEffectComposition composition;
        public float value;
        public int priority;
        public float minimumKnowledge;
        public float minimumConfidence;
        public string minimumStageId;
        public bool useColony;
        public string resultId;
        public KnowledgeRequirementGroup requirements;
    }

    public sealed class KnowledgeInsightRequirement
    {
        public KnowledgeRequirementKind kind;
        public string subjectId;
        public string facetId;
        public string trackId;
        public string eventId;
        public string insightId;
        public string stageId;
        public string customId;
        public float minimum;
        public bool colony;
        public string domainId;
        public string claimId;
        public string contextTypeId;
        public string contextId;
        public KnowledgeClaimValue value;
        public KnowledgeRequirementComparison comparison = KnowledgeRequirementComparison.GreaterOrEqual;
        public KnowledgeRequirementGroup group;
    }

    public sealed class KnowledgeInsightOutcome
    {
        public string facetId;
        public float knowledge;
        public float familiarity;
        public float expertise;
        public string expertiseTrackId;
        public string stageId;
        public bool document;
        public string customId;
        public List<KnowledgeMeasurement> claimMeasurements;
        public string sharedExpertiseNamespaceId;
        public float sharedExpertiseWeight = 1f;
    }

    public sealed class KnowledgeInsightDef : Def
    {
        public string domainId;
        public KnowledgeInsightScope scope;
        public bool activateAutomatically = true;
        public bool preventRepeat = true;
        public List<KnowledgeInsightRequirement> requirements;
        public List<KnowledgeInsightOutcome> outcomes;
        public KnowledgeRequirementGroup requirementGroup;
    }

    /// <summary>A directional, non-recursive source of provisional derived knowledge.</summary>
    public sealed class KnowledgeRelationshipDef : Def
    {
        public string domainId;
        public string fromDomainId;
        public string toDomainId;
        public string fromSubjectId;
        public string toSubjectId;
        public string facetId;
        public float coefficient;
        public float confidenceCoefficient = 0.5f;
        public bool provisional = true;
        public KnowledgeRequirementGroup requirements;
    }

    public sealed class KnowledgeTransmissionDef : Def
    {
        public string domainId;
        [Obsolete("Transmission model is unsupported; configure KnowledgeDomainDef.sharingModel.")]
        public KnowledgeSharingModel model;
        public float knowledgeEfficiency = 1f;
        public float confidenceEfficiency = 0.8f;
        public bool preserveSourceAttribution = true;
        public bool survivesObserverLoss = true;
    }

    /// <summary>Static subject content. Dynamic subjects use KnowledgeSubjectRegistration instead.</summary>
    public sealed class KnowledgeSubjectDef : Def
    {
        public string domainId;
        public string stableId;
        public string unidentifiedLabel;
        public string unidentifiedDescription;
        public string iconPath;
        public string templateSubjectId;
        public float templateKnowledgeCoefficient;
        public float templateConfidenceCoefficient = 0.5f;
        public List<string> categoryIds;
        public int sortOrder;
        public string archetypeId;
        public List<string> applicableFacetIds;
        public List<string> applicableClaimIds;
        public KnowledgeSubjectState state;

        public string StableId => stableId.NullOrEmpty() ? defName : stableId;
    }

    /// <summary>Top-level Def schema. Referenced content can also be registered dynamically.</summary>
    public sealed class KnowledgeDomainDef : Def
    {
        public string stableId;
        public bool enableUncertainty;
        public bool enableFamiliarity;
        public KnowledgeSharingModel sharingModel = KnowledgeSharingModel.Reportable;
        // Legacy is the safe default for existing V2 Def consumers. New V3 Defs
        // should opt into Balanced explicitly until their compatibility boundary
        // is known to the framework.
        public KnowledgeStageAggregationMode stageAggregationMode = KnowledgeStageAggregationMode.LegacySumMax;
        public int sortOrder;
        public int provenanceLimit = 16;
        public int evidenceAggregateLimit = 128;
        public List<KnowledgeFacetDef> facets;
        public List<KnowledgeStageDef> stages;
        public List<KnowledgeExpertiseTrackDef> expertiseTracks;
        public List<KnowledgeObservationDef> observations;
        public List<KnowledgeRevealDef> reveals;
        public List<KnowledgeEffectDef> effects;
        public List<KnowledgeInsightDef> insights;
        public List<KnowledgeRelationshipDef> relationships;
        public KnowledgeTransmissionDef transmission;
        public List<KnowledgeClaimDef> claims;
        public List<KnowledgeSubjectArchetypeDef> archetypes;
        public List<KnowledgeMilestoneTrackDef> milestoneTracks;
        public List<KnowledgeExpertiseNamespaceDef> expertiseNamespaces;

        public string StableId => stableId.NullOrEmpty() ? defName : stableId;
    }

    public sealed class KnowledgeSubjectRegistration
    {
        public string id;
        public string label;
        public string description;
        public string unidentifiedLabel;
        public string unidentifiedDescription;
        public string iconPath;
        public Def sourceDef;
        public string templateSubjectId;
        public float templateKnowledgeCoefficient;
        public float templateConfidenceCoefficient = 0.5f;
        public IReadOnlyList<string> categoryIds;
        public int sortOrder;
        public string source;
        public string archetypeId;
        public IReadOnlyList<string> applicableFacetIds;
        public IReadOnlyList<string> applicableClaimIds;
        public KnowledgeSubjectState state;
    }

    public sealed class KnowledgeDomainRegistration
    {
        public string id;
        public string label;
        public string description;
        public bool enableUncertainty;
        public bool enableFamiliarity;
        public KnowledgeSharingModel sharingModel = KnowledgeSharingModel.Reportable;
        // Dynamic registrations are compatibility-ambiguous, so unspecified
        // registrations retain the historical V1/V2 stage calculation.
        public KnowledgeStageAggregationMode stageAggregationMode = KnowledgeStageAggregationMode.LegacySumMax;
        public int sortOrder;
        public int provenanceLimit = 16;
        public int evidenceAggregateLimit = 128;
        public IReadOnlyList<KnowledgeFacetDef> facets;
        public IReadOnlyList<KnowledgeStageDef> stages;
        public IReadOnlyList<KnowledgeExpertiseTrackDef> expertiseTracks;
        public IReadOnlyList<KnowledgeObservationDef> observations;
        public IReadOnlyList<KnowledgeRevealDef> reveals;
        public IReadOnlyList<KnowledgeEffectDef> effects;
        public IReadOnlyList<KnowledgeInsightDef> insights;
        public IReadOnlyList<KnowledgeRelationshipDef> relationships;
        public KnowledgeTransmissionDef transmission;
        public IReadOnlyList<KnowledgeClaimDef> claims;
        public IReadOnlyList<KnowledgeSubjectArchetypeDef> archetypes;
        public IReadOnlyList<KnowledgeMilestoneTrackDef> milestoneTracks;
        public IReadOnlyList<KnowledgeExpertiseNamespaceDef> expertiseNamespaces;
        public Func<string, KnowledgeSubjectRegistration> subjectResolver;
        public Func<IEnumerable<KnowledgeSubjectRegistration>> subjectSource;
        public string source;
    }

    public sealed class KnowledgeRegistrationOptions
    {
        public int priority;
        public string source;
        public KnowledgeRegistrationConflict conflict = KnowledgeRegistrationConflict.Reject;
    }

    public sealed class KnowledgeValidationIssue
    {
        public readonly string code;
        public readonly string ownerId;
        public readonly string message;
        public readonly bool error;

        public KnowledgeValidationIssue(string code, string ownerId, string message, bool error = true)
        {
            this.code = code;
            this.ownerId = ownerId;
            this.message = message;
            this.error = error;
        }

        public override string ToString() => code + " [" + ownerId + "]: " + message;
    }
}
