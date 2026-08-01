using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace KnowledgeFramework
{
    internal sealed class KnowledgeMeasurementRecord : IExposable
    {
        public string domainId;
        public string subjectId;
        public string facetId;
        public string claimId;
        public Pawn observer;
        public int scope;
        public string contextTypeId;
        public string contextId;
        public int valueType;
        public bool booleanValue;
        public int integerValue;
        public float numericValue;
        public float rangeMinimum;
        public float rangeMaximum;
        public string textValue;
        public float vectorX;
        public float vectorY;
        public float vectorZ;
        public List<string> setValues = new List<string>();
        public float quality = 1f;
        public float evidenceWeight = 1f;
        public float confidenceFactor = 1f;
        public int disposition;
        public string source;
        public string sourceInstanceId;
        public string methodId;
        public string reasonId;
        public string specimenId;
        public string summary;
        public List<Pawn> witnesses = new List<Pawn>();
        public int tick;
        public bool documented;
        public bool revealed;

        public void ExposeData()
        {
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref facetId, "facetId");
            Scribe_Values.Look(ref claimId, "claimId");
            Scribe_References.Look(ref observer, "observer");
            Scribe_Values.Look(ref scope, "scope");
            Scribe_Values.Look(ref contextTypeId, "contextTypeId");
            Scribe_Values.Look(ref contextId, "contextId");
            Scribe_Values.Look(ref valueType, "valueType");
            Scribe_Values.Look(ref booleanValue, "booleanValue");
            Scribe_Values.Look(ref integerValue, "integerValue");
            Scribe_Values.Look(ref numericValue, "numericValue");
            Scribe_Values.Look(ref rangeMinimum, "rangeMinimum");
            Scribe_Values.Look(ref rangeMaximum, "rangeMaximum");
            Scribe_Values.Look(ref textValue, "textValue");
            Scribe_Values.Look(ref vectorX, "vectorX");
            Scribe_Values.Look(ref vectorY, "vectorY");
            Scribe_Values.Look(ref vectorZ, "vectorZ");
            Scribe_Collections.Look(ref setValues, "setValues", LookMode.Value);
            Scribe_Values.Look(ref quality, "quality");
            Scribe_Values.Look(ref evidenceWeight, "evidenceWeight");
            Scribe_Values.Look(ref confidenceFactor, "confidenceFactor");
            Scribe_Values.Look(ref disposition, "disposition");
            Scribe_Values.Look(ref source, "source");
            Scribe_Values.Look(ref sourceInstanceId, "sourceInstanceId");
            Scribe_Values.Look(ref methodId, "methodId");
            Scribe_Values.Look(ref reasonId, "reasonId");
            Scribe_Values.Look(ref specimenId, "specimenId");
            Scribe_Values.Look(ref summary, "summary");
            Scribe_Collections.Look(ref witnesses, "witnesses", LookMode.Reference);
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref documented, "documented");
            Scribe_Values.Look(ref revealed, "revealed");
            Normalize();
        }

        public void Normalize()
        {
            domainId = domainId?.Trim();
            subjectId = subjectId?.Trim();
            facetId = facetId.NullOrEmpty() ? KnowledgeSchema.DefaultFacetId : facetId.Trim();
            claimId = claimId?.Trim();
            scope = Math.Max(0, Math.Min(1, scope));
            valueType = Math.Max(0, Math.Min(Enum.GetValues(typeof(KnowledgeClaimValueType)).Length - 1, valueType));
            numericValue = KnowledgeMath.IsFinite(numericValue) ? numericValue : 0f;
            rangeMinimum = KnowledgeMath.IsFinite(rangeMinimum) ? rangeMinimum : 0f;
            rangeMaximum = KnowledgeMath.IsFinite(rangeMaximum) ? rangeMaximum : rangeMinimum;
            vectorX = KnowledgeMath.IsFinite(vectorX) ? vectorX : 0f;
            vectorY = KnowledgeMath.IsFinite(vectorY) ? vectorY : 0f;
            vectorZ = KnowledgeMath.IsFinite(vectorZ) ? vectorZ : 0f;
            quality = Math.Max(0f, KnowledgeMath.NonNegativeFiniteOr(quality, 1f));
            evidenceWeight = KnowledgeMath.NonNegativeFiniteOr(evidenceWeight, 1f);
            confidenceFactor = KnowledgeMath.Clamp01Finite(confidenceFactor);
            disposition = Math.Max(0, Math.Min(2, disposition));
            if (setValues == null) setValues = new List<string>();
            setValues = setValues.Where(value => !value.NullOrEmpty()).Distinct().Take(128).OrderBy(value => value).ToList();
            if (witnesses == null) witnesses = new List<Pawn>();
            witnesses = witnesses.Where(item => item != null).Distinct().Take(32).ToList();
            tick = Math.Max(0, tick);
        }

        public static KnowledgeMeasurementRecord FromMeasurement(KnowledgeMeasurement value, string domainId, string subjectId,
            string facetId, Pawn defaultObserver, KnowledgeScope defaultScope)
        {
            KnowledgeClaimValue claim = value?.value?.Clone();
            if (value == null || claim == null) return null;
            KnowledgeMeasurementRecord result = new KnowledgeMeasurementRecord
            {
                domainId = value.domainId ?? domainId,
                subjectId = value.subjectId ?? subjectId,
                facetId = value.facetId ?? facetId,
                claimId = value.claimId,
                observer = value.observer ?? defaultObserver,
                scope = (int)value.scope,
                contextTypeId = value.context.typeId,
                contextId = value.context.stableId,
                valueType = (int)claim.type,
                booleanValue = claim.booleanValue,
                integerValue = claim.integerValue,
                numericValue = claim.numericValue,
                rangeMinimum = claim.rangeValue.minimum,
                rangeMaximum = claim.rangeValue.maximum,
                textValue = claim.textValue,
                vectorX = claim.vectorX,
                vectorY = claim.vectorY,
                vectorZ = claim.vectorZ,
                setValues = (claim.setValues ?? new List<string>()).ToList(),
                quality = value.quality,
                evidenceWeight = value.evidenceWeight,
                confidenceFactor = value.confidenceFactor,
                disposition = (int)value.disposition,
                source = value.source,
                sourceInstanceId = value.sourceInstanceId,
                methodId = value.methodId,
                reasonId = value.reasonId,
                specimenId = value.specimenId,
                summary = value.summary,
                witnesses = (value.witnesses ?? Array.Empty<Pawn>()).Where(item => item != null).Distinct().ToList(),
                tick = value.tick < 0 ? Find.TickManager?.TicksGame ?? 0 : value.tick,
                documented = value.documented,
                revealed = value.revealed
            };
            if (value.scope == default(KnowledgeScope) && defaultScope == KnowledgeScope.Colony) result.scope = (int)defaultScope;
            result.Normalize();
            return result;
        }

        public KnowledgeClaimValue ToValue()
        {
            KnowledgeClaimValue result = new KnowledgeClaimValue
            {
                type = (KnowledgeClaimValueType)valueType,
                booleanValue = booleanValue,
                integerValue = integerValue,
                numericValue = numericValue,
                rangeValue = new KnowledgeNumericRange(rangeMinimum, rangeMaximum),
                textValue = textValue,
                vectorX = vectorX,
                vectorY = vectorY,
                vectorZ = vectorZ,
                setValues = (setValues ?? new List<string>()).ToList()
            };
            return result;
        }

        public KnowledgeMeasurement ToMeasurement()
        {
            return new KnowledgeMeasurement
            {
                domainId = domainId,
                subjectId = subjectId,
                facetId = facetId,
                claimId = claimId,
                observer = observer,
                scope = (KnowledgeScope)scope,
                value = ToValue(),
                context = new KnowledgeContextKey(contextTypeId, contextId),
                quality = quality,
                evidenceWeight = evidenceWeight,
                confidenceFactor = confidenceFactor,
                disposition = (KnowledgeEvidenceDisposition)disposition,
                source = source,
                sourceInstanceId = sourceInstanceId,
                methodId = methodId,
                reasonId = reasonId,
                specimenId = specimenId,
                summary = summary,
                witnesses = witnesses,
                tick = tick,
                documented = documented,
                revealed = revealed
            };
        }
    }

    internal sealed class KnowledgeClaimStateRecord : IExposable
    {
        public string domainId;
        public string subjectId;
        public string facetId;
        public string claimId;
        public Pawn pawn;
        public bool colony;
        public string contextTypeId;
        public string contextId;
        public List<KnowledgeMeasurementRecord> measurements = new List<KnowledgeMeasurementRecord>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref facetId, "facetId");
            Scribe_Values.Look(ref claimId, "claimId");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref colony, "colony");
            Scribe_Values.Look(ref contextTypeId, "contextTypeId");
            Scribe_Values.Look(ref contextId, "contextId");
            Scribe_Collections.Look(ref measurements, "measurements", LookMode.Deep);
            Normalize();
        }

        public void Normalize()
        {
            domainId = domainId?.Trim();
            subjectId = subjectId?.Trim();
            facetId = facetId.NullOrEmpty() ? KnowledgeSchema.DefaultFacetId : facetId.Trim();
            claimId = claimId?.Trim();
            if (measurements == null) measurements = new List<KnowledgeMeasurementRecord>();
            measurements.RemoveAll(item => item == null || item.claimId.NullOrEmpty());
            foreach (KnowledgeMeasurementRecord item in measurements) item.Normalize();
            measurements = measurements.OrderBy(item => item.tick).Take(64).ToList();
        }
    }

    internal sealed class KnowledgeContextFacetStateRecord : IExposable
    {
        public string domainId;
        public string subjectId;
        public string facetId;
        public Pawn pawn;
        public bool colony;
        public string contextTypeId;
        public string contextId;
        public float amount;
        public float supportingEvidence;
        public float contradictoryEvidence;
        public int evidenceCount;
        public int successCount;
        public int failureCount;
        public int lastTick;
        public int revision;

        public void ExposeData()
        {
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref facetId, "facetId");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref colony, "colony");
            Scribe_Values.Look(ref contextTypeId, "contextTypeId");
            Scribe_Values.Look(ref contextId, "contextId");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref supportingEvidence, "supportingEvidence");
            Scribe_Values.Look(ref contradictoryEvidence, "contradictoryEvidence");
            Scribe_Values.Look(ref evidenceCount, "evidenceCount");
            Scribe_Values.Look(ref successCount, "successCount");
            Scribe_Values.Look(ref failureCount, "failureCount");
            Scribe_Values.Look(ref lastTick, "lastTick");
            Scribe_Values.Look(ref revision, "revision");
            Normalize();
        }

        public void Normalize()
        {
            amount = KnowledgeMath.NonNegativeFiniteOr(amount, 0f);
            supportingEvidence = KnowledgeMath.NonNegativeFiniteOr(supportingEvidence, 0f);
            contradictoryEvidence = KnowledgeMath.NonNegativeFiniteOr(contradictoryEvidence, 0f);
            evidenceCount = Mathf.Clamp(evidenceCount, 0, 100000000);
            successCount = Mathf.Clamp(successCount, 0, evidenceCount);
            failureCount = Mathf.Clamp(failureCount, 0, evidenceCount);
            lastTick = Math.Max(0, lastTick);
            revision = Math.Max(0, revision);
        }
    }

    internal sealed class KnowledgeMilestoneStateRecord : IExposable
    {
        public string domainId;
        public string subjectId;
        public string trackId;
        public string milestoneId;
        public Pawn pawn;
        public string contextTypeId;
        public string contextId;
        public bool available;
        public bool started;
        public bool interrupted;
        public bool completed;
        public int startTick;
        public int completionTick;
        public float progress;
        public float bestHistoricalValue;
        public string interruptionReason;
        public Pawn completingPawn;
        public int revision;

        public void ExposeData()
        {
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref trackId, "trackId");
            Scribe_Values.Look(ref milestoneId, "milestoneId");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref contextTypeId, "contextTypeId");
            Scribe_Values.Look(ref contextId, "contextId");
            Scribe_Values.Look(ref available, "available");
            Scribe_Values.Look(ref started, "started");
            Scribe_Values.Look(ref interrupted, "interrupted");
            Scribe_Values.Look(ref completed, "completed");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref completionTick, "completionTick");
            Scribe_Values.Look(ref progress, "progress");
            Scribe_Values.Look(ref bestHistoricalValue, "bestHistoricalValue");
            Scribe_Values.Look(ref interruptionReason, "interruptionReason");
            Scribe_References.Look(ref completingPawn, "completingPawn");
            Scribe_Values.Look(ref revision, "revision");
            Normalize();
        }

        public void Normalize()
        {
            progress = KnowledgeMath.Clamp01Finite(progress);
            bestHistoricalValue = KnowledgeMath.NonNegativeFiniteOr(bestHistoricalValue, 0f);
            startTick = Math.Max(0, startTick);
            completionTick = Math.Max(0, completionTick);
            revision = Math.Max(0, revision);
        }
    }

    internal sealed class KnowledgeSubjectRelationStateRecord : IExposable
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
        public string contextTypeId;
        public string contextId;
        public string source;
        public int tick;
        public Dictionary<string, string> metadata = new Dictionary<string, string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref fromSubjectId, "fromSubjectId");
            Scribe_Values.Look(ref toDomainId, "toDomainId");
            Scribe_Values.Look(ref toSubjectId, "toSubjectId");
            Scribe_Values.Look(ref relationTypeId, "relationTypeId");
            Scribe_Values.Look(ref role, "role");
            Scribe_Values.Look(ref order, "order");
            Scribe_Values.Look(ref revealed, "revealed");
            Scribe_Values.Look(ref confidence, "confidence");
            Scribe_Values.Look(ref contextTypeId, "contextTypeId");
            Scribe_Values.Look(ref contextId, "contextId");
            Scribe_Values.Look(ref source, "source");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Collections.Look(ref metadata, "metadata", LookMode.Value, LookMode.Value);
            Normalize();
        }

        public void Normalize()
        {
            confidence = KnowledgeMath.Clamp01Finite(confidence);
            tick = Math.Max(0, tick);
            if (metadata == null) metadata = new Dictionary<string, string>();
            metadata = metadata.Where(pair => !pair.Key.NullOrEmpty()).Take(32).ToDictionary(pair => pair.Key, pair => pair.Value ?? string.Empty);
        }

        public KnowledgeSubjectRelation ToRelation()
        {
            return new KnowledgeSubjectRelation
            {
                domainId = domainId,
                fromSubjectId = fromSubjectId,
                toDomainId = toDomainId,
                toSubjectId = toSubjectId,
                relationTypeId = relationTypeId,
                role = role,
                order = order,
                revealed = revealed,
                confidence = confidence,
                context = new KnowledgeContextKey(contextTypeId, contextId),
                source = source,
                tick = tick,
                metadata = new Dictionary<string, string>(metadata ?? new Dictionary<string, string>())
            };
        }

        public static KnowledgeSubjectRelationStateRecord FromRelation(KnowledgeSubjectRelation value)
        {
            if (value == null) return null;
            return new KnowledgeSubjectRelationStateRecord
            {
                domainId = value.domainId,
                fromSubjectId = value.fromSubjectId,
                toDomainId = value.toDomainId,
                toSubjectId = value.toSubjectId,
                relationTypeId = value.relationTypeId,
                role = value.role,
                order = value.order,
                revealed = value.revealed,
                confidence = value.confidence,
                contextTypeId = value.context.typeId,
                contextId = value.context.stableId,
                source = value.source,
                tick = value.tick,
                metadata = value.metadata == null ? new Dictionary<string, string>() : new Dictionary<string, string>(value.metadata)
            };
        }
    }

    internal sealed class KnowledgeAccrualStateRecord : IExposable
    {
        public string key;
        public string domainId;
        public string subjectId;
        public int pawnId;
        public string sourceInstanceId;
        public string specimenId;
        public string contextKey;
        public int count;
        public int dailyCount;
        public int day;
        public int lastTick;
        public string lastSource;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref sourceInstanceId, "sourceInstanceId");
            Scribe_Values.Look(ref specimenId, "specimenId");
            Scribe_Values.Look(ref contextKey, "contextKey");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref dailyCount, "dailyCount");
            Scribe_Values.Look(ref day, "day");
            Scribe_Values.Look(ref lastTick, "lastTick");
            Scribe_Values.Look(ref lastSource, "lastSource");
            count = Mathf.Clamp(count, 0, 100000000);
            dailyCount = Mathf.Clamp(dailyCount, 0, 100000000);
            day = Math.Max(0, day);
            lastTick = Math.Max(0, lastTick);
        }
    }

    internal sealed class KnowledgeSubjectOverrideRecord : IExposable
    {
        public string domainId;
        public string id;
        public string label;
        public string description;
        public string unidentifiedLabel;
        public string unidentifiedDescription;
        public string archetypeId;
        public List<string> categoryIds = new List<string>();
        public List<string> applicableFacetIds = new List<string>();
        public List<string> applicableClaimIds = new List<string>();
        public string templateSubjectId;
        public float templateKnowledgeCoefficient;
        public float templateConfidenceCoefficient;
        public string iconPath;
        public Def sourceDef;
        public int sortOrder;
        public int state;
        public string source;

        public void ExposeData()
        {
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref description, "description");
            Scribe_Values.Look(ref unidentifiedLabel, "unidentifiedLabel");
            Scribe_Values.Look(ref unidentifiedDescription, "unidentifiedDescription");
            Scribe_Values.Look(ref archetypeId, "archetypeId");
            Scribe_Collections.Look(ref categoryIds, "categoryIds", LookMode.Value);
            Scribe_Collections.Look(ref applicableFacetIds, "applicableFacetIds", LookMode.Value);
            Scribe_Collections.Look(ref applicableClaimIds, "applicableClaimIds", LookMode.Value);
            Scribe_Values.Look(ref templateSubjectId, "templateSubjectId");
            Scribe_Values.Look(ref templateKnowledgeCoefficient, "templateKnowledgeCoefficient");
            Scribe_Values.Look(ref templateConfidenceCoefficient, "templateConfidenceCoefficient");
            Scribe_Values.Look(ref iconPath, "iconPath");
            Scribe_Defs.Look(ref sourceDef, "sourceDef");
            Scribe_Values.Look(ref sortOrder, "sortOrder");
            Scribe_Values.Look(ref state, "state");
            Scribe_Values.Look(ref source, "source");
            categoryIds = (categoryIds ?? new List<string>()).Where(value => !value.NullOrEmpty()).Distinct().Take(64).ToList();
            applicableFacetIds = (applicableFacetIds ?? new List<string>()).Where(value => !value.NullOrEmpty()).Distinct().Take(128).ToList();
            applicableClaimIds = (applicableClaimIds ?? new List<string>()).Where(value => !value.NullOrEmpty()).Distinct().Take(128).ToList();
            templateKnowledgeCoefficient = KnowledgeMath.Clamp01Finite(templateKnowledgeCoefficient);
            templateConfidenceCoefficient = KnowledgeMath.Clamp01Finite(templateConfidenceCoefficient);
            state = Math.Max(0, Math.Min(Enum.GetValues(typeof(KnowledgeSubjectState)).Length - 1, state));
        }

        public KnowledgeSubjectRegistration ToRegistration()
        {
            return new KnowledgeSubjectRegistration
            {
                id = id,
                label = label,
                description = description,
                unidentifiedLabel = unidentifiedLabel,
                unidentifiedDescription = unidentifiedDescription,
                archetypeId = archetypeId,
                categoryIds = categoryIds,
                applicableFacetIds = applicableFacetIds,
                applicableClaimIds = applicableClaimIds,
                templateSubjectId = templateSubjectId,
                templateKnowledgeCoefficient = templateKnowledgeCoefficient,
                templateConfidenceCoefficient = templateConfidenceCoefficient,
                iconPath = iconPath,
                sourceDef = sourceDef,
                sortOrder = sortOrder,
                state = (KnowledgeSubjectState)state,
                source = source
            };
        }

        public static KnowledgeSubjectOverrideRecord FromRegistration(string domainId, KnowledgeSubjectRegistration value)
        {
            return new KnowledgeSubjectOverrideRecord
            {
                domainId = domainId,
                id = value.id,
                label = value.label,
                description = value.description,
                unidentifiedLabel = value.unidentifiedLabel,
                unidentifiedDescription = value.unidentifiedDescription,
                archetypeId = value.archetypeId,
                categoryIds = value.categoryIds?.ToList() ?? new List<string>(),
                applicableFacetIds = value.applicableFacetIds?.ToList() ?? new List<string>(),
                applicableClaimIds = value.applicableClaimIds?.ToList() ?? new List<string>(),
                templateSubjectId = value.templateSubjectId,
                templateKnowledgeCoefficient = value.templateKnowledgeCoefficient,
                templateConfidenceCoefficient = value.templateConfidenceCoefficient,
                iconPath = value.iconPath,
                sourceDef = value.sourceDef,
                sortOrder = value.sortOrder,
                state = (int)value.state,
                source = value.source
            };
        }
    }

    internal sealed class KnowledgeSharedExpertiseStateRecord : IExposable
    {
        public string namespaceId;
        public string domainId;
        public string trackId;
        public Pawn pawn;
        public float amount;
        public int revision;

        public void ExposeData()
        {
            Scribe_Values.Look(ref namespaceId, "namespaceId");
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref trackId, "trackId");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref revision, "revision");
            amount = KnowledgeMath.NonNegativeFiniteOr(amount, 0f);
            revision = Math.Max(0, revision);
        }
    }

    internal sealed class KnowledgeMigrationStateRecord : IExposable
    {
        public string consumerId;
        public int version;
        public bool committed;
        public int tick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref consumerId, "consumerId");
            Scribe_Values.Look(ref version, "version");
            Scribe_Values.Look(ref committed, "committed");
            Scribe_Values.Look(ref tick, "tick");
            version = Math.Max(0, version);
            tick = Math.Max(0, tick);
        }
    }

    internal readonly struct ClaimRuntimeKey : IEquatable<ClaimRuntimeKey>
    {
        public readonly string domainId;
        public readonly string subjectId;
        public readonly string facetId;
        public readonly string claimId;
        public readonly int pawnId;
        public readonly bool colony;
        public readonly string contextTypeId;
        public readonly string contextId;

        public ClaimRuntimeKey(string domainId, string subjectId, string facetId, string claimId, Pawn pawn, bool colony, KnowledgeContextKey context)
        {
            this.domainId = domainId;
            this.subjectId = subjectId;
            this.facetId = facetId;
            this.claimId = claimId;
            pawnId = pawn?.thingIDNumber ?? 0;
            this.colony = colony;
            contextTypeId = context.typeId;
            contextId = context.stableId;
        }

        public bool Equals(ClaimRuntimeKey other) => pawnId == other.pawnId && colony == other.colony && domainId == other.domainId &&
            subjectId == other.subjectId && facetId == other.facetId && claimId == other.claimId && contextTypeId == other.contextTypeId && contextId == other.contextId;
        public override bool Equals(object obj) => obj is ClaimRuntimeKey other && Equals(other);
        public override int GetHashCode() => (((((domainId?.GetHashCode() ?? 0) * 397 ^ (subjectId?.GetHashCode() ?? 0)) * 397 ^
            (facetId?.GetHashCode() ?? 0)) * 397 ^ (claimId?.GetHashCode() ?? 0)) * 397 ^ pawnId) * 397 ^ (colony ? 1 : 0) ^
            (contextTypeId?.GetHashCode() ?? 0) * 397 ^ (contextId?.GetHashCode() ?? 0);
    }

    internal readonly struct ContextFacetRuntimeKey : IEquatable<ContextFacetRuntimeKey>
    {
        public readonly string domainId;
        public readonly string subjectId;
        public readonly string facetId;
        public readonly int pawnId;
        public readonly bool colony;
        public readonly string contextTypeId;
        public readonly string contextId;

        public ContextFacetRuntimeKey(string domainId, string subjectId, string facetId, Pawn pawn, bool colony, KnowledgeContextKey context)
        {
            this.domainId = domainId;
            this.subjectId = subjectId;
            this.facetId = facetId;
            pawnId = pawn?.thingIDNumber ?? 0;
            this.colony = colony;
            contextTypeId = context.typeId;
            contextId = context.stableId;
        }

        public bool Equals(ContextFacetRuntimeKey other) => pawnId == other.pawnId && colony == other.colony && domainId == other.domainId &&
            subjectId == other.subjectId && facetId == other.facetId && contextTypeId == other.contextTypeId && contextId == other.contextId;
        public override bool Equals(object obj) => obj is ContextFacetRuntimeKey other && Equals(other);
        public override int GetHashCode() => (((((domainId?.GetHashCode() ?? 0) * 397 ^ (subjectId?.GetHashCode() ?? 0)) * 397 ^
            (facetId?.GetHashCode() ?? 0)) * 397 ^ pawnId) * 397 ^ (colony ? 1 : 0)) * 397 ^
            (contextTypeId?.GetHashCode() ?? 0) ^ (contextId?.GetHashCode() ?? 0);
    }

    internal readonly struct MilestoneRuntimeKey : IEquatable<MilestoneRuntimeKey>
    {
        public readonly string domainId;
        public readonly string subjectId;
        public readonly string trackId;
        public readonly string milestoneId;
        public readonly int pawnId;
        public readonly string contextTypeId;
        public readonly string contextId;

        public MilestoneRuntimeKey(string domainId, string subjectId, string trackId, string milestoneId, Pawn pawn, KnowledgeContextKey context)
        {
            this.domainId = domainId;
            this.subjectId = subjectId;
            this.trackId = trackId;
            this.milestoneId = milestoneId;
            pawnId = pawn?.thingIDNumber ?? 0;
            contextTypeId = context.typeId;
            contextId = context.stableId;
        }

        public bool Equals(MilestoneRuntimeKey other) => pawnId == other.pawnId && domainId == other.domainId && subjectId == other.subjectId &&
            trackId == other.trackId && milestoneId == other.milestoneId && contextTypeId == other.contextTypeId && contextId == other.contextId;
        public override bool Equals(object obj) => obj is MilestoneRuntimeKey other && Equals(other);
        public override int GetHashCode() => (((((domainId?.GetHashCode() ?? 0) * 397 ^ (subjectId?.GetHashCode() ?? 0)) * 397 ^
            (trackId?.GetHashCode() ?? 0)) * 397 ^ (milestoneId?.GetHashCode() ?? 0)) * 397 ^ pawnId) * 397 ^
            (contextTypeId?.GetHashCode() ?? 0) ^ (contextId?.GetHashCode() ?? 0);
    }

    public sealed partial class GameComponent_KnowledgeFramework
    {
        internal const int CurrentV3SchemaVersion = 3;
        private List<KnowledgeClaimStateRecord> claimsV3 = new List<KnowledgeClaimStateRecord>();
        private List<KnowledgeContextFacetStateRecord> contextFacetsV3 = new List<KnowledgeContextFacetStateRecord>();
        private List<KnowledgeMilestoneStateRecord> milestonesV3 = new List<KnowledgeMilestoneStateRecord>();
        private List<KnowledgeSubjectRelationStateRecord> relationsV3 = new List<KnowledgeSubjectRelationStateRecord>();
        private List<KnowledgeAccrualStateRecord> accrualV3 = new List<KnowledgeAccrualStateRecord>();
        private List<KnowledgeSubjectOverrideRecord> subjectOverridesV3 = new List<KnowledgeSubjectOverrideRecord>();
        private List<KnowledgeSharedExpertiseStateRecord> sharedExpertiseV3 = new List<KnowledgeSharedExpertiseStateRecord>();
        private List<KnowledgeMigrationStateRecord> consumerMigrationsV3 = new List<KnowledgeMigrationStateRecord>();
        private Dictionary<ClaimRuntimeKey, KnowledgeClaimStateRecord> claimsV3Index;
        private Dictionary<ContextFacetRuntimeKey, KnowledgeContextFacetStateRecord> contextFacetsV3Index;
        private Dictionary<MilestoneRuntimeKey, KnowledgeMilestoneStateRecord> milestonesV3Index;
        private Dictionary<string, KnowledgeAccrualStateRecord> accrualV3Index;
        private Dictionary<string, KnowledgeSharedExpertiseStateRecord> sharedExpertiseV3Index;
        private Dictionary<string, KnowledgeMigrationStateRecord> consumerMigrationsV3Index;

        private void InitializeV3()
        {
            claimsV3Index = new Dictionary<ClaimRuntimeKey, KnowledgeClaimStateRecord>();
            contextFacetsV3Index = new Dictionary<ContextFacetRuntimeKey, KnowledgeContextFacetStateRecord>();
            milestonesV3Index = new Dictionary<MilestoneRuntimeKey, KnowledgeMilestoneStateRecord>();
            accrualV3Index = new Dictionary<string, KnowledgeAccrualStateRecord>(StringComparer.Ordinal);
            sharedExpertiseV3Index = new Dictionary<string, KnowledgeSharedExpertiseStateRecord>(StringComparer.Ordinal);
            consumerMigrationsV3Index = new Dictionary<string, KnowledgeMigrationStateRecord>(StringComparer.Ordinal);
        }

        private void ExposeV3Data()
        {
            Scribe_Collections.Look(ref claimsV3, "knowledgeFrameworkV3Claims", LookMode.Deep);
            Scribe_Collections.Look(ref contextFacetsV3, "knowledgeFrameworkV3ContextFacets", LookMode.Deep);
            Scribe_Collections.Look(ref milestonesV3, "knowledgeFrameworkV3Milestones", LookMode.Deep);
            Scribe_Collections.Look(ref relationsV3, "knowledgeFrameworkV3Relations", LookMode.Deep);
            Scribe_Collections.Look(ref accrualV3, "knowledgeFrameworkV3Accrual", LookMode.Deep);
            Scribe_Collections.Look(ref subjectOverridesV3, "knowledgeFrameworkV3SubjectOverrides", LookMode.Deep);
            Scribe_Collections.Look(ref sharedExpertiseV3, "knowledgeFrameworkV3SharedExpertise", LookMode.Deep);
            Scribe_Collections.Look(ref consumerMigrationsV3, "knowledgeFrameworkV3Migrations", LookMode.Deep);
        }

        private void RebuildV3Indexes()
        {
            InitializeV3();
            claimsV3 = claimsV3 ?? new List<KnowledgeClaimStateRecord>();
            contextFacetsV3 = contextFacetsV3 ?? new List<KnowledgeContextFacetStateRecord>();
            milestonesV3 = milestonesV3 ?? new List<KnowledgeMilestoneStateRecord>();
            relationsV3 = relationsV3 ?? new List<KnowledgeSubjectRelationStateRecord>();
            accrualV3 = accrualV3 ?? new List<KnowledgeAccrualStateRecord>();
            subjectOverridesV3 = subjectOverridesV3 ?? new List<KnowledgeSubjectOverrideRecord>();
            sharedExpertiseV3 = sharedExpertiseV3 ?? new List<KnowledgeSharedExpertiseStateRecord>();
            consumerMigrationsV3 = consumerMigrationsV3 ?? new List<KnowledgeMigrationStateRecord>();
            foreach (KnowledgeClaimStateRecord item in claimsV3.Where(item => item != null))
            {
                item.Normalize();
                ClaimRuntimeKey key = new ClaimRuntimeKey(item.domainId, item.subjectId, item.facetId, item.claimId, item.pawn, item.colony,
                    new KnowledgeContextKey(item.contextTypeId, item.contextId));
                if (claimsV3Index.TryGetValue(key, out KnowledgeClaimStateRecord existing)) existing.measurements.AddRange(item.measurements);
                else claimsV3Index[key] = item;
            }
            foreach (KnowledgeClaimStateRecord item in claimsV3Index.Values)
            {
                item.measurements = item.measurements.Where(value => value != null).OrderBy(value => value.tick).Take(64).ToList();
                item.Normalize();
            }
            claimsV3 = claimsV3Index.Values.ToList();
            foreach (KnowledgeContextFacetStateRecord item in contextFacetsV3.Where(item => item != null))
            {
                item.Normalize();
                ContextFacetRuntimeKey key = new ContextFacetRuntimeKey(item.domainId, item.subjectId, item.facetId, item.pawn, item.colony,
                    new KnowledgeContextKey(item.contextTypeId, item.contextId));
                if (!contextFacetsV3Index.TryGetValue(key, out KnowledgeContextFacetStateRecord existing)) contextFacetsV3Index[key] = item;
                else
                {
                    existing.amount = Math.Max(existing.amount, item.amount);
                    existing.supportingEvidence = Math.Max(existing.supportingEvidence, item.supportingEvidence);
                    existing.contradictoryEvidence = Math.Max(existing.contradictoryEvidence, item.contradictoryEvidence);
                    existing.evidenceCount = Math.Max(existing.evidenceCount, item.evidenceCount);
                    existing.successCount = Math.Max(existing.successCount, item.successCount);
                    existing.failureCount = Math.Max(existing.failureCount, item.failureCount);
                    existing.lastTick = Math.Max(existing.lastTick, item.lastTick);
                }
            }
            foreach (KnowledgeMilestoneStateRecord item in milestonesV3.Where(item => item != null))
            {
                item.Normalize();
                milestonesV3Index[new MilestoneRuntimeKey(item.domainId, item.subjectId, item.trackId, item.milestoneId, item.pawn,
                    new KnowledgeContextKey(item.contextTypeId, item.contextId))] = item;
            }
            foreach (KnowledgeAccrualStateRecord item in accrualV3.Where(item => item != null && !item.key.NullOrEmpty())) accrualV3Index[item.key] = item;
            foreach (KnowledgeSharedExpertiseStateRecord item in sharedExpertiseV3.Where(item => item != null && !item.namespaceId.NullOrEmpty()))
                sharedExpertiseV3Index[SharedExpertiseKey(item.namespaceId, item.domainId, item.trackId, item.pawn)] = item;
            foreach (KnowledgeMigrationStateRecord item in consumerMigrationsV3.Where(item => item != null && !item.consumerId.NullOrEmpty()))
                consumerMigrationsV3Index[item.consumerId] = item;
            subjectOverridesV3.RemoveAll(item => item == null || item.domainId.NullOrEmpty() || item.id.NullOrEmpty());
            foreach (KnowledgeSubjectOverrideRecord item in subjectOverridesV3) KnowledgeRegistry.RestoreSubjectOverride(item.ToRegistration(), item.domainId);
            knowledgeFrameworkSchemaVersion = Math.Max(knowledgeFrameworkSchemaVersion, CurrentV3SchemaVersion);
        }

        internal KnowledgeClaimStateRecord ClaimV3(string domainId, string subjectId, string facetId, string claimId, Pawn pawn,
            bool colony, KnowledgeContextKey context, bool create)
        {
            ClaimRuntimeKey key = new ClaimRuntimeKey(domainId, subjectId, facetId, claimId, pawn, colony, context);
            if (claimsV3Index.TryGetValue(key, out KnowledgeClaimStateRecord result) || !create) return result;
            result = new KnowledgeClaimStateRecord { domainId = domainId, subjectId = subjectId, facetId = facetId, claimId = claimId,
                pawn = pawn, colony = colony, contextTypeId = context.typeId, contextId = context.stableId };
            claimsV3.Add(result);
            claimsV3Index[key] = result;
            return result;
        }

        internal KnowledgeContextFacetStateRecord ContextFacetV3(string domainId, string subjectId, string facetId, Pawn pawn,
            bool colony, KnowledgeContextKey context, bool create)
        {
            ContextFacetRuntimeKey key = new ContextFacetRuntimeKey(domainId, subjectId, facetId, pawn, colony, context);
            if (contextFacetsV3Index.TryGetValue(key, out KnowledgeContextFacetStateRecord result) || !create) return result;
            result = new KnowledgeContextFacetStateRecord { domainId = domainId, subjectId = subjectId, facetId = facetId,
                pawn = pawn, colony = colony, contextTypeId = context.typeId, contextId = context.stableId };
            contextFacetsV3.Add(result);
            contextFacetsV3Index[key] = result;
            return result;
        }

        internal KnowledgeMilestoneStateRecord MilestoneV3(string domainId, string subjectId, string trackId, string milestoneId,
            Pawn pawn, KnowledgeContextKey context, bool create)
        {
            MilestoneRuntimeKey key = new MilestoneRuntimeKey(domainId, subjectId, trackId, milestoneId, pawn, context);
            if (milestonesV3Index.TryGetValue(key, out KnowledgeMilestoneStateRecord result) || !create) return result;
            result = new KnowledgeMilestoneStateRecord { domainId = domainId, subjectId = subjectId, trackId = trackId,
                milestoneId = milestoneId, pawn = pawn, contextTypeId = context.typeId, contextId = context.stableId };
            milestonesV3.Add(result);
            milestonesV3Index[key] = result;
            return result;
        }

        internal KnowledgeAccrualStateRecord AccrualV3(string key, bool create)
        {
            if (accrualV3Index.TryGetValue(key, out KnowledgeAccrualStateRecord result) || !create) return result;
            result = new KnowledgeAccrualStateRecord { key = key };
            accrualV3.Add(result);
            accrualV3Index[key] = result;
            return result;
        }

        internal KnowledgeSharedExpertiseStateRecord SharedExpertiseV3(string namespaceId, string domainId, string trackId, Pawn pawn, bool create)
        {
            string key = SharedExpertiseKey(namespaceId, domainId, trackId, pawn);
            if (sharedExpertiseV3Index.TryGetValue(key, out KnowledgeSharedExpertiseStateRecord result) || !create) return result;
            result = new KnowledgeSharedExpertiseStateRecord { namespaceId = namespaceId, domainId = domainId, trackId = trackId, pawn = pawn };
            sharedExpertiseV3.Add(result);
            sharedExpertiseV3Index[key] = result;
            return result;
        }

        internal IEnumerable<KnowledgeClaimStateRecord> ClaimRecordsV3(string domainId, string subjectId = null, Pawn pawn = null, bool colony = false) =>
            claimsV3.Where(item => item != null && item.domainId == domainId && item.colony == colony && (subjectId.NullOrEmpty() || item.subjectId == subjectId) &&
                (colony || pawn == null || item.pawn == pawn));

        internal IEnumerable<KnowledgeContextFacetStateRecord> ContextFacetRecordsV3(string domainId, string subjectId, string facetId,
            Pawn pawn, bool colony) => contextFacetsV3.Where(item => item != null && item.domainId == domainId && item.subjectId == subjectId &&
            item.facetId == facetId && item.colony == colony && (colony || pawn == null || item.pawn == pawn));

        internal IEnumerable<KnowledgeMilestoneStateRecord> MilestoneRecordsV3(string domainId, string subjectId = null, Pawn pawn = null) =>
            milestonesV3.Where(item => item != null && item.domainId == domainId && (subjectId.NullOrEmpty() || item.subjectId == subjectId) &&
                (pawn == null || item.pawn == pawn));

        internal IEnumerable<KnowledgeSubjectRelationStateRecord> RelationRecordsV3(string domainId, string subjectId = null) =>
            relationsV3.Where(item => item != null && (domainId.NullOrEmpty() || item.domainId == domainId || item.toDomainId == domainId) &&
                (subjectId.NullOrEmpty() || item.fromSubjectId == subjectId || item.toSubjectId == subjectId));

        internal IEnumerable<KnowledgeAccrualStateRecord> AccrualRecordsV3() => accrualV3;
        internal IEnumerable<KnowledgeSharedExpertiseStateRecord> SharedExpertiseRecordsV3(string namespaceId, Pawn pawn = null) =>
            sharedExpertiseV3.Where(item => item != null && item.namespaceId == namespaceId && (pawn == null || item.pawn == pawn));

        internal void AddRelationV3(KnowledgeSubjectRelation value)
        {
            KnowledgeSubjectRelationStateRecord record = KnowledgeSubjectRelationStateRecord.FromRelation(value);
            if (record == null) return;
            relationsV3.Add(record);
            TouchV3();
        }

        internal bool RemoveRelationV3(string domainId, string fromSubjectId, string toDomainId, string toSubjectId, string relationTypeId,
            KnowledgeContextKey context)
        {
            int removed = relationsV3.RemoveAll(item => item.domainId == domainId && item.fromSubjectId == fromSubjectId && item.toDomainId == toDomainId &&
                item.toSubjectId == toSubjectId && item.relationTypeId == relationTypeId && item.contextTypeId == context.typeId && item.contextId == context.stableId);
            if (removed > 0) TouchV3();
            return removed > 0;
        }

        internal void PersistSubjectOverrideV3(string domainId, KnowledgeSubjectRegistration value)
        {
            KnowledgeSubjectOverrideRecord record = KnowledgeSubjectOverrideRecord.FromRegistration(domainId, value);
            KnowledgeSubjectOverrideRecord existing = subjectOverridesV3.FirstOrDefault(item => item.domainId == domainId && item.id == value.id);
            if (existing == null) subjectOverridesV3.Add(record);
            else
            {
                int index = subjectOverridesV3.IndexOf(existing);
                subjectOverridesV3[index] = record;
            }
            TouchV3();
        }

        internal bool HasConsumerMigrationV3(string consumerId, int version) => consumerMigrationsV3Index.TryGetValue(consumerId, out KnowledgeMigrationStateRecord value) && value.committed && value.version >= version;

        internal void CommitConsumerMigrationV3(string consumerId, int version)
        {
            if (consumerId.NullOrEmpty()) return;
            KnowledgeMigrationStateRecord record = consumerMigrationsV3Index.TryGetValue(consumerId, out KnowledgeMigrationStateRecord value) ? value :
                new KnowledgeMigrationStateRecord { consumerId = consumerId };
            record.version = Math.Max(record.version, version);
            record.committed = true;
            record.tick = Find.TickManager?.TicksGame ?? 0;
            if (!consumerMigrationsV3Index.ContainsKey(consumerId))
            {
                consumerMigrationsV3.Add(record);
                consumerMigrationsV3Index[consumerId] = record;
            }
            TouchV3();
        }

        internal int TouchV3() => ++globalRevision;

        internal long ApproximateV3PersistentBytes() => claimsV3.Sum(item => 192L + item.measurements.Count * 192L) +
            contextFacetsV3.Count * 128L + milestonesV3.Count * 160L + relationsV3.Count * 192L + accrualV3.Count * 128L +
            subjectOverridesV3.Count * 256L + sharedExpertiseV3.Count * 96L + consumerMigrationsV3.Count * 64L;

        internal int V3ClaimCount => claimsV3.Count;
        internal int V3MeasurementCount => claimsV3.Sum(item => item?.measurements?.Count ?? 0);
        internal int V3ContextCount => claimsV3.Select(item => item?.contextTypeId + "\n" + item?.contextId).Where(item => !item.NullOrEmpty()).Distinct().Count();
        internal int V3MilestoneCount => milestonesV3.Count;
        internal int V3RelationCount => relationsV3.Count;
        internal int V3AccrualCount => accrualV3.Count;
        internal int V3SubjectOverrideCount => subjectOverridesV3.Count;
        internal int V3OrphanCount => claimsV3.Count(item => item == null || item.pawn == null && !item.colony ||
            KnowledgeRegistry.Schema(item?.domainId) == null || KnowledgeRegistry.ResolveSubject(item?.domainId, item?.subjectId) == null) +
            relationsV3.Count(item => item == null || KnowledgeRegistry.Schema(item.domainId) == null ||
                KnowledgeRegistry.ResolveSubject(item.domainId, item.fromSubjectId) == null ||
                KnowledgeRegistry.Schema(item.toDomainId) == null || KnowledgeRegistry.ResolveSubject(item.toDomainId, item.toSubjectId) == null);

        internal void RemoveDomainDataV3(string domainId)
        {
            claimsV3.RemoveAll(item => item?.domainId == domainId);
            contextFacetsV3.RemoveAll(item => item?.domainId == domainId);
            milestonesV3.RemoveAll(item => item?.domainId == domainId);
            relationsV3.RemoveAll(item => item?.domainId == domainId || item?.toDomainId == domainId);
            accrualV3.RemoveAll(item => item?.domainId == domainId);
            subjectOverridesV3.RemoveAll(item => item?.domainId == domainId);
            sharedExpertiseV3.RemoveAll(item => item?.domainId == domainId);
            RebuildV3Indexes();
        }

        private static string SharedExpertiseKey(string namespaceId, string domainId, string trackId, Pawn pawn) => namespaceId + "\n" + domainId + "\n" + trackId + "\n" + (pawn?.thingIDNumber ?? 0);
    }

    internal static class KnowledgeV3PersistenceBridge
    {
        internal static void PersistSubjectOverride(string domainId, KnowledgeSubjectRegistration value) =>
            GameComponent_KnowledgeFramework.Current?.PersistSubjectOverrideV3(domainId, value);
    }
}
