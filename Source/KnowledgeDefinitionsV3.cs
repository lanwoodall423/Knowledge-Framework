using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using Verse;

namespace KnowledgeFramework
{
    public enum KnowledgeClaimValueType
    {
        Boolean,
        Integer,
        Float,
        Percentage,
        NumericRange,
        StringId,
        DefReference,
        SubjectReference,
        EnumId,
        Direction,
        Vector,
        SetOfIds
    }

    public enum KnowledgeClaimAggregation
    {
        Latest,
        Highest,
        Lowest,
        Mean,
        WeightedMean,
        ObservedRange,
        Union,
        Intersection,
        MostSupported,
        HighestQuality,
        ConsumerDefined
    }

    public enum KnowledgeClaimStalenessPolicy
    {
        Permanent,
        SlowlyStale,
        Seasonal,
        Contextual,
        ConsumerManaged
    }

    public enum KnowledgeContextFallbackMode
    {
        ExactOnly,
        ParentThenGlobal
    }

    public enum KnowledgeRequirementGroupMode
    {
        All,
        Any,
        AtLeast,
        Weighted
    }

    public enum KnowledgeRequirementComparison
    {
        Exists,
        Equal,
        NotEqual,
        GreaterThan,
        GreaterOrEqual,
        LessThan,
        LessOrEqual,
        Contains,
        Intersects
    }

    public enum KnowledgeSubjectState
    {
        Active,
        Archived,
        Hidden,
        MissingContent,
        Retired
    }

    public enum KnowledgeWitnessDistributionPolicy
    {
        ObserverOnly,
        WitnessesFull,
        WitnessesReduced,
        PartyShared,
        ColonyDirect,
        Custom
    }

    public enum KnowledgeMilestonePauseBehavior
    {
        Pause,
        Interrupt,
        Reset
    }

    public enum KnowledgeMilestoneResetBehavior
    {
        Never,
        OnInterruption,
        OnFailure,
        ConsumerManaged
    }

    public enum KnowledgeComparisonRowKind
    {
        Claim,
        Facet,
        Milestone,
        Relation,
        Record,
        Custom
    }

    public enum KnowledgeMilestoneEventKind
    {
        Available,
        Started,
        Interrupted,
        Completed,
        Reset
    }

    public readonly struct KnowledgeNumericRange : IEquatable<KnowledgeNumericRange>
    {
        public readonly float minimum;
        public readonly float maximum;

        public KnowledgeNumericRange(float minimum, float maximum)
        {
            if (!KnowledgeMath.IsFinite(minimum)) minimum = 0f;
            if (!KnowledgeMath.IsFinite(maximum)) maximum = minimum;
            this.minimum = Math.Min(minimum, maximum);
            this.maximum = Math.Max(minimum, maximum);
        }

        public bool Equals(KnowledgeNumericRange other) =>
            Math.Abs(minimum - other.minimum) < 0.0001f && Math.Abs(maximum - other.maximum) < 0.0001f;

        public override bool Equals(object obj) => obj is KnowledgeNumericRange other && Equals(other);
        public override int GetHashCode() => minimum.GetHashCode() * 397 ^ maximum.GetHashCode();
        public override string ToString() => minimum.ToString("0.##") + "-" + maximum.ToString("0.##");
    }

    public readonly struct KnowledgeContextKey : IEquatable<KnowledgeContextKey>
    {
        public readonly string typeId;
        public readonly string stableId;

        public KnowledgeContextKey(string typeId, string stableId)
        {
            this.typeId = typeId?.Trim();
            this.stableId = stableId?.Trim();
        }

        public bool IsEmpty => typeId.NullOrEmpty() || stableId.NullOrEmpty();
        public static KnowledgeContextKey Empty => new KnowledgeContextKey(null, null);

        public bool Equals(KnowledgeContextKey other) => typeId == other.typeId && stableId == other.stableId;
        public override bool Equals(object obj) => obj is KnowledgeContextKey other && Equals(other);
        public override int GetHashCode() => (typeId?.GetHashCode() ?? 0) * 397 ^ (stableId?.GetHashCode() ?? 0);
        public override string ToString() => IsEmpty ? string.Empty : typeId + ":" + stableId;
    }

    /// <summary>A finite, explicitly typed value. Runtime object serialization is intentionally unsupported.</summary>
    public sealed class KnowledgeClaimValue
    {
        public KnowledgeClaimValueType type;
        public bool booleanValue;
        public int integerValue;
        public float numericValue;
        public KnowledgeNumericRange rangeValue;
        public string textValue;
        public float vectorX;
        public float vectorY;
        public float vectorZ;
        public List<string> setValues = new List<string>();

        public static KnowledgeClaimValue Boolean(bool value) => new KnowledgeClaimValue
        {
            type = KnowledgeClaimValueType.Boolean,
            booleanValue = value
        };

        public static KnowledgeClaimValue Integer(int value) => new KnowledgeClaimValue
        {
            type = KnowledgeClaimValueType.Integer,
            integerValue = value
        };

        public static KnowledgeClaimValue Float(float value) => new KnowledgeClaimValue
        {
            type = KnowledgeClaimValueType.Float,
            numericValue = value
        };

        public static KnowledgeClaimValue Percentage(float value) => new KnowledgeClaimValue
        {
            type = KnowledgeClaimValueType.Percentage,
            numericValue = value
        };

        public static KnowledgeClaimValue Range(float minimum, float maximum) => new KnowledgeClaimValue
        {
            type = KnowledgeClaimValueType.NumericRange,
            rangeValue = new KnowledgeNumericRange(minimum, maximum)
        };

        public static KnowledgeClaimValue Text(KnowledgeClaimValueType valueType, string value) => new KnowledgeClaimValue
        {
            type = valueType,
            textValue = value
        };

        public static KnowledgeClaimValue Vector(KnowledgeClaimValueType valueType, float x, float y, float z = 0f) => new KnowledgeClaimValue
        {
            type = valueType,
            vectorX = x,
            vectorY = y,
            vectorZ = z
        };

        public static KnowledgeClaimValue Set(IEnumerable<string> values) => new KnowledgeClaimValue
        {
            type = KnowledgeClaimValueType.SetOfIds,
            setValues = (values ?? Enumerable.Empty<string>()).Where(value => !value.NullOrEmpty()).Distinct().OrderBy(value => value).ToList()
        };

        public KnowledgeClaimValue Clone()
        {
            return new KnowledgeClaimValue
            {
                type = type,
                booleanValue = booleanValue,
                integerValue = integerValue,
                numericValue = numericValue,
                rangeValue = rangeValue,
                textValue = textValue,
                vectorX = vectorX,
                vectorY = vectorY,
                vectorZ = vectorZ,
                setValues = (setValues ?? new List<string>()).Where(value => !value.NullOrEmpty()).Distinct().OrderBy(value => value).ToList()
            };
        }

        public bool TryValidate(KnowledgeClaimValueType expected, out string error)
        {
            error = null;
            if (type != expected)
            {
                error = "Claim value type is " + type + ", expected " + expected + ".";
                return false;
            }
            if ((type == KnowledgeClaimValueType.Float || type == KnowledgeClaimValueType.Percentage) &&
                !KnowledgeMath.IsFinite(numericValue)) error = "Claim numeric value is not finite.";
            if (type == KnowledgeClaimValueType.Percentage && (numericValue < 0f || numericValue > 100f))
                error = "Percentage claims must be between zero and one hundred.";
            if (type == KnowledgeClaimValueType.NumericRange &&
                (!KnowledgeMath.IsFinite(rangeValue.minimum) || !KnowledgeMath.IsFinite(rangeValue.maximum)))
                error = "Claim range values must be finite.";
            if ((type == KnowledgeClaimValueType.Direction || type == KnowledgeClaimValueType.Vector) &&
                (!KnowledgeMath.IsFinite(vectorX) || !KnowledgeMath.IsFinite(vectorY) || !KnowledgeMath.IsFinite(vectorZ)))
                error = "Claim vector values must be finite.";
            if ((type == KnowledgeClaimValueType.StringId || type == KnowledgeClaimValueType.DefReference ||
                 type == KnowledgeClaimValueType.SubjectReference || type == KnowledgeClaimValueType.EnumId) && textValue.NullOrEmpty())
                error = "Stable ID claim values cannot be empty.";
            if (type == KnowledgeClaimValueType.SetOfIds && (setValues ?? new List<string>()).Any(value => value.NullOrEmpty()))
                error = "Set-valued claims may only contain stable IDs.";
            return error.NullOrEmpty();
        }

        public string StableKey()
        {
            switch (type)
            {
                case KnowledgeClaimValueType.Boolean: return booleanValue ? "true" : "false";
                case KnowledgeClaimValueType.Integer: return integerValue.ToString();
                case KnowledgeClaimValueType.Float:
                case KnowledgeClaimValueType.Percentage: return numericValue.ToString("R");
                case KnowledgeClaimValueType.NumericRange: return rangeValue.ToString();
                case KnowledgeClaimValueType.Direction:
                case KnowledgeClaimValueType.Vector: return vectorX.ToString("R") + "," + vectorY.ToString("R") + "," + vectorZ.ToString("R");
                case KnowledgeClaimValueType.SetOfIds: return string.Join(",", (setValues ?? new List<string>()).OrderBy(value => value).ToArray());
                default: return textValue ?? string.Empty;
            }
        }

        public override string ToString() => StableKey();
    }

    public sealed class KnowledgeClaimDef : Def
    {
        public string domainId;
        public string stableId;
        public string facetId;
        public KnowledgeClaimValueType valueType;
        public KnowledgeClaimAggregation aggregation;
        public KnowledgeClaimStalenessPolicy stalenessPolicy;
        public float halfLifeTicks = 600000f;
        public float provisionalConfidence = 0.5f;
        public bool revealedByDefault = true;
        public bool documentable = true;
        public int provenanceLimit = 16;
        public int measurementHistoryLimit = 64;
        public string consumerAggregationId;

        public string StableId => stableId.NullOrEmpty() ? defName : stableId;
    }

    public sealed class KnowledgeSubjectArchetypeDef : Def
    {
        public string stableId;
        public string categoryId;
        public string iconPath;
        public string templateSubjectId;
        public bool contextual;
        public List<string> applicableFacetIds;
        public List<string> applicableClaimIds;
        public List<string> discoveryStageIds;
        public List<string> observationIds;
        public List<string> effectIds;
        public List<string> expertiseTrackIds;
        public string comparisonSchemaId;

        public string StableId => stableId.NullOrEmpty() ? defName : stableId;
    }

    public sealed class KnowledgeRequirement
    {
        public string domainId;
        public KnowledgeRequirementKind kind;
        public KnowledgeRequirementComparison comparison = KnowledgeRequirementComparison.GreaterOrEqual;
        public string subjectId;
        public string facetId;
        public string claimId;
        public string trackId;
        public string eventId;
        public string insightId;
        public string stageId;
        public string milestoneTrackId;
        public string milestoneId;
        public string contextTypeId;
        public string contextId;
        public string customId;
        public float minimum;
        public float maximum;
        public float weight = 1f;
        public bool colony;
        public bool allowContextFallback = true;
        public bool negate;
        public KnowledgeClaimValue value;
        public string label;
    }

    public sealed class KnowledgeRequirementGroup
    {
        public KnowledgeRequirementGroupMode mode = KnowledgeRequirementGroupMode.All;
        public int minimumCount = 1;
        public float minimumWeight;
        public List<KnowledgeRequirement> requirements = new List<KnowledgeRequirement>();
        public List<KnowledgeRequirementGroup> groups = new List<KnowledgeRequirementGroup>();
    }

    public sealed class KnowledgeObservationOutcome
    {
        public string facetId;
        public float knowledge;
        public float familiarity;
        public float evidenceWeight = 1f;
        public KnowledgeEvidenceDisposition disposition = KnowledgeEvidenceDisposition.Supporting;
        public float confidenceFactor = 1f;
        public string expertiseTrackId;
        public float expertise;
        public bool targetColony;
        public bool document;
        public List<KnowledgeMeasurement> claimMeasurements = new List<KnowledgeMeasurement>();
    }

    public sealed class KnowledgeExpertiseOutcome
    {
        public string trackId;
        public float expertise;
        public string namespaceId;
        public float namespaceWeight = 1f;
    }

    public sealed class KnowledgeWitnessDistribution
    {
        public KnowledgeWitnessDistributionPolicy policy = KnowledgeWitnessDistributionPolicy.ObserverOnly;
        public float efficiency = 0.5f;
        public float expertiseEfficiency = 0.5f;
        public float confidenceEfficiency = 0.75f;
        public bool includeObserver;
        public int maximumRecipients = 32;
        public string customId;
    }

    public sealed class KnowledgeAccrualPolicy
    {
        public bool uniquePerSourceInstance;
        public bool uniquePerPawnAndSourceInstance;
        public bool uniquePerSubjectAndContext;
        public int cooldownTicks;
        public int dailyCap;
        public int lifetimeCap;
        public float diminishingReturns;
        public float firstObservationBonus = 1f;
        public float firstSuccessBonus = 1f;
        public float firstFailureBonus = 1f;
        public float differentSpecimenBonus = 1f;
        public float differentContextBonus = 1f;
        public float independentSourceConfidenceBonus = 1f;
        public float repeatedSourceConfidencePenalty = 1f;
        public int stateLimit = 1024;
    }

    public sealed class KnowledgeContextTypeDef : Def
    {
        public string stableId;
        public string parentTypeId;
        public bool allowFallback = true;
        public bool contextualByDefault;

        public string StableId => stableId.NullOrEmpty() ? defName : stableId;
    }

    public sealed class KnowledgeMilestoneTrackDef : Def
    {
        public string domainId;
        public string stableId;
        public bool ordered = true;
        public bool repeatable;
        public List<KnowledgeMilestoneDef> milestones = new List<KnowledgeMilestoneDef>();

        public string StableId => stableId.NullOrEmpty() ? defName : stableId;
    }

    public sealed class KnowledgeMilestoneDef
    {
        public string stableId;
        public string label;
        public string description;
        public int order;
        public bool repeatable;
        public bool permanent;
        public int sustainedTicks;
        public KnowledgeMilestonePauseBehavior pauseBehavior;
        public KnowledgeMilestoneResetBehavior resetBehavior;
        public KnowledgeRequirementGroup requirements;
        public string customEvaluatorId;

        public string StableId => stableId.NullOrEmpty() ? label : stableId;
    }

    public sealed class KnowledgeSubjectRelationTypeDef : Def
    {
        public string stableId;
        public bool parentage;
        public bool symmetric;
        public string inverseTypeId;
        public int metadataLimit = 16;

        public string StableId => stableId.NullOrEmpty() ? defName : stableId;
    }

    public sealed class KnowledgeExpertiseNamespaceDef : Def
    {
        public string stableId;
        public float adept = 100f;
        public float expert = 300f;
        public float master = 700f;
        public int contributionLimit = 64;

        public string StableId => stableId.NullOrEmpty() ? defName : stableId;
    }

    public sealed class KnowledgeMeasurement
    {
        public string domainId;
        public string subjectId;
        public string facetId;
        public string claimId;
        public Pawn observer;
        public KnowledgeScope scope = KnowledgeScope.Personal;
        public KnowledgeClaimValue value;
        public KnowledgeContextKey context;
        public float quality = 1f;
        public float evidenceWeight = 1f;
        public float confidenceFactor = 1f;
        public KnowledgeEvidenceDisposition disposition = KnowledgeEvidenceDisposition.Supporting;
        public string source;
        public string sourceInstanceId;
        public string methodId;
        public string reasonId;
        public string specimenId;
        public string summary;
        public IReadOnlyList<Pawn> witnesses;
        public int tick = -1;
        public bool documented;
        public bool revealed;

        public KnowledgeMeasurement Clone()
        {
            return new KnowledgeMeasurement
            {
                domainId = domainId,
                subjectId = subjectId,
                facetId = facetId,
                claimId = claimId,
                observer = observer,
                scope = scope,
                value = value?.Clone(),
                context = context,
                quality = quality,
                evidenceWeight = evidenceWeight,
                confidenceFactor = confidenceFactor,
                disposition = disposition,
                source = source,
                sourceInstanceId = sourceInstanceId,
                methodId = methodId,
                reasonId = reasonId,
                specimenId = specimenId,
                summary = summary,
                witnesses = witnesses == null ? null : witnesses.Where(item => item != null).Distinct().ToList(),
                tick = tick,
                documented = documented,
                revealed = revealed
            };
        }
    }

    public sealed class KnowledgeClaimMeasurementSnapshot
    {
        public readonly KnowledgeClaimValue value;
        public readonly KnowledgeEvidenceDisposition disposition;
        public readonly float quality;
        public readonly float evidenceWeight;
        public readonly string source;
        public readonly string sourceInstanceId;
        public readonly string summary;
        public readonly int tick;
        public readonly KnowledgeContextKey context;

        internal KnowledgeClaimMeasurementSnapshot(KnowledgeMeasurementRecord record)
        {
            value = record.ToValue();
            disposition = (KnowledgeEvidenceDisposition)record.disposition;
            quality = record.quality;
            evidenceWeight = record.evidenceWeight;
            source = record.source;
            sourceInstanceId = record.sourceInstanceId;
            summary = record.summary;
            tick = record.tick;
            context = new KnowledgeContextKey(record.contextTypeId, record.contextId);
        }
    }

    public delegate KnowledgeClaimValue KnowledgeClaimAggregator(IReadOnlyList<KnowledgeClaimMeasurementSnapshot> measurements);

    public sealed class KnowledgeClaimSnapshot
    {
        public readonly string domainId;
        public readonly string subjectId;
        public readonly string facetId;
        public readonly string claimId;
        public readonly Pawn pawn;
        public readonly KnowledgeScope scope;
        public readonly KnowledgeContextKey context;
        public readonly KnowledgeClaimValue value;
        public readonly float storedConfidence;
        public readonly float effectiveConfidence;
        public readonly bool provisional;
        public readonly bool contradictory;
        public readonly int observationCount;
        public readonly int lastConfirmedTick;
        public readonly string bestQualitySource;
        public readonly float staleness;
        public readonly bool revealed;
        public readonly bool documented;
        public readonly IReadOnlyList<KnowledgeClaimMeasurementSnapshot> provenance;

        internal KnowledgeClaimSnapshot(string domainId, string subjectId, string facetId, string claimId, Pawn pawn,
            KnowledgeScope scope, KnowledgeContextKey context, KnowledgeClaimValue value, float storedConfidence,
            float effectiveConfidence, bool provisional, bool contradictory, int observationCount, int lastConfirmedTick,
            string bestQualitySource, float staleness, bool revealed, bool documented,
            IEnumerable<KnowledgeClaimMeasurementSnapshot> provenance)
        {
            this.domainId = domainId;
            this.subjectId = subjectId;
            this.facetId = facetId;
            this.claimId = claimId;
            this.pawn = pawn;
            this.scope = scope;
            this.context = context;
            this.value = value?.Clone();
            this.storedConfidence = storedConfidence;
            this.effectiveConfidence = effectiveConfidence;
            this.provisional = provisional;
            this.contradictory = contradictory;
            this.observationCount = observationCount;
            this.lastConfirmedTick = lastConfirmedTick;
            this.bestQualitySource = bestQualitySource;
            this.staleness = staleness;
            this.revealed = revealed;
            this.documented = documented;
            this.provenance = new ReadOnlyCollection<KnowledgeClaimMeasurementSnapshot>(
                (provenance ?? Enumerable.Empty<KnowledgeClaimMeasurementSnapshot>()).ToList());
        }
    }

    public sealed class KnowledgeClaimChangedEvent
    {
        public readonly string domainId;
        public readonly string subjectId;
        public readonly string facetId;
        public readonly string claimId;
        public readonly Pawn pawn;
        public readonly KnowledgeScope scope;
        public readonly KnowledgeContextKey context;
        public readonly KnowledgeClaimSnapshot oldValue;
        public readonly KnowledgeClaimSnapshot newValue;

        internal KnowledgeClaimChangedEvent(string domainId, string subjectId, string facetId, string claimId, Pawn pawn,
            KnowledgeScope scope, KnowledgeContextKey context, KnowledgeClaimSnapshot oldValue, KnowledgeClaimSnapshot newValue)
        {
            this.domainId = domainId;
            this.subjectId = subjectId;
            this.facetId = facetId;
            this.claimId = claimId;
            this.pawn = pawn;
            this.scope = scope;
            this.context = context;
            this.oldValue = oldValue;
            this.newValue = newValue;
        }
    }

    public sealed class KnowledgeSubjectUpdate
    {
        public string label;
        public string description;
        public string unidentifiedLabel;
        public string unidentifiedDescription;
        public string archetypeId;
        public IReadOnlyList<string> categoryIds;
        public IReadOnlyList<string> applicableFacetIds;
        public IReadOnlyList<string> applicableClaimIds;
        public string templateSubjectId;
        public Def sourceDef;
        public string iconPath;
        public int? sortOrder;
        public string source;
    }

    public sealed class KnowledgeSubjectRelation
    {
        public string domainId;
        public string fromSubjectId;
        public string toDomainId;
        public string toSubjectId;
        public string relationTypeId;
        public string role;
        public int order;
        public bool revealed;
        public float confidence;
        public KnowledgeContextKey context;
        public string source;
        public int tick;
        public Dictionary<string, string> metadata = new Dictionary<string, string>();
    }

    public sealed class KnowledgeMilestoneState
    {
        public readonly string domainId;
        public readonly string subjectId;
        public readonly string trackId;
        public readonly string milestoneId;
        public readonly Pawn pawn;
        public readonly KnowledgeContextKey context;
        public readonly bool available;
        public readonly bool started;
        public readonly bool interrupted;
        public readonly bool completed;
        public readonly int startTick;
        public readonly int completionTick;
        public readonly float progress;
        public readonly float bestHistoricalValue;
        public readonly string interruptionReason;
        public readonly Pawn completingPawn;

        internal KnowledgeMilestoneState(string domainId, string subjectId, string trackId, string milestoneId, Pawn pawn,
            KnowledgeContextKey context, bool available, bool started, bool interrupted, bool completed, int startTick,
            int completionTick, float progress, float bestHistoricalValue, string interruptionReason, Pawn completingPawn)
        {
            this.domainId = domainId;
            this.subjectId = subjectId;
            this.trackId = trackId;
            this.milestoneId = milestoneId;
            this.pawn = pawn;
            this.context = context;
            this.available = available;
            this.started = started;
            this.interrupted = interrupted;
            this.completed = completed;
            this.startTick = startTick;
            this.completionTick = completionTick;
            this.progress = progress;
            this.bestHistoricalValue = bestHistoricalValue;
            this.interruptionReason = interruptionReason;
            this.completingPawn = completingPawn;
        }
    }

    public sealed class KnowledgeMilestoneChangedEvent
    {
        public readonly KnowledgeMilestoneEventKind kind;
        public readonly KnowledgeMilestoneState state;
        public readonly string reason;

        internal KnowledgeMilestoneChangedEvent(KnowledgeMilestoneEventKind kind, KnowledgeMilestoneState state, string reason)
        {
            this.kind = kind;
            this.state = state;
            this.reason = reason;
        }
    }

    public sealed class KnowledgeMilestoneConditionSample
    {
        public string domainId;
        public string subjectId;
        public string trackId;
        public string milestoneId;
        public Pawn pawn;
        public KnowledgeContextKey context;
        public bool conditionMet;
        public float value;
        public int elapsedTicks;
        public Pawn completingPawn;
        public string interruptionReason;
    }

    public sealed class KnowledgeSharedExpertiseSnapshot
    {
        public readonly string namespaceId;
        public readonly Pawn pawn;
        public readonly float total;
        public readonly KnowledgeRank rank;
        public readonly float progress;
        public readonly IReadOnlyList<KnowledgeSharedExpertiseContribution> contributions;

        internal KnowledgeSharedExpertiseSnapshot(string namespaceId, Pawn pawn, float total, KnowledgeRank rank, float progress,
            IEnumerable<KnowledgeSharedExpertiseContribution> contributions)
        {
            this.namespaceId = namespaceId;
            this.pawn = pawn;
            this.total = total;
            this.rank = rank;
            this.progress = progress;
            this.contributions = new ReadOnlyCollection<KnowledgeSharedExpertiseContribution>(
                (contributions ?? Enumerable.Empty<KnowledgeSharedExpertiseContribution>()).ToList());
        }
    }

    public sealed class KnowledgeSharedExpertiseContribution
    {
        public readonly string domainId;
        public readonly string trackId;
        public readonly float amount;

        internal KnowledgeSharedExpertiseContribution(string domainId, string trackId, float amount)
        {
            this.domainId = domainId;
            this.trackId = trackId;
            this.amount = amount;
        }
    }

    public sealed class KnowledgeComparisonRow
    {
        public string id;
        public string label;
        public KnowledgeComparisonRowKind kind;
        public KnowledgeClaimValue firstValue;
        public KnowledgeClaimValue secondValue;
        public bool firstKnown;
        public bool secondKnown;
        public float firstConfidence;
        public float secondConfidence;
        public string summary;
    }

    public sealed class KnowledgeComparisonSchema
    {
        public string id;
        public string label;
        public List<string> claimIds = new List<string>();
        public List<string> facetIds = new List<string>();
        public List<string> milestoneTrackIds = new List<string>();
        public List<string> relationTypeIds = new List<string>();
    }

    public sealed class KnowledgeStructuredComparisonSnapshot
    {
        public readonly string domainId;
        public readonly IReadOnlyList<string> subjectIds;
        public readonly IReadOnlyList<KnowledgeComparisonRow> rows;

        internal KnowledgeStructuredComparisonSnapshot(string domainId, IEnumerable<string> subjectIds, IEnumerable<KnowledgeComparisonRow> rows)
        {
            this.domainId = domainId;
            this.subjectIds = new ReadOnlyCollection<string>((subjectIds ?? Enumerable.Empty<string>()).ToList());
            this.rows = new ReadOnlyCollection<KnowledgeComparisonRow>((rows ?? Enumerable.Empty<KnowledgeComparisonRow>()).ToList());
        }
    }
}
