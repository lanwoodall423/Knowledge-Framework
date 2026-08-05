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
            if (contextTypeId.NullOrEmpty() != contextId.NullOrEmpty()) contextTypeId = contextId = null;
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
            if (contextTypeId.NullOrEmpty() != contextId.NullOrEmpty()) contextTypeId = contextId = null;
            if (measurements == null) measurements = new List<KnowledgeMeasurementRecord>();
            measurements.RemoveAll(item => item == null || item.claimId.NullOrEmpty());
            foreach (KnowledgeMeasurementRecord item in measurements) item.Normalize();
            // Keep a bounded legacy-safe list here; RebuildV3Indexes applies the
            // schema-specific history limit after deduplicating records.
            measurements = measurements.OrderBy(item => item.tick).Take(4096).ToList();
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

        public KnowledgeContextKey Context => new KnowledgeContextKey(contextTypeId, contextId);

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
            if (contextTypeId.NullOrEmpty() != contextId.NullOrEmpty()) contextTypeId = contextId = null;
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
            if (contextTypeId.NullOrEmpty() != contextId.NullOrEmpty()) contextTypeId = contextId = null;
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
            if (contextTypeId.NullOrEmpty() != contextId.NullOrEmpty()) contextTypeId = contextId = null;
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
        // Retain the pre-namespaced key when migration cannot prove every
        // optional dimension. This makes legacy lookup deterministic and
        // idempotent across repeated rebuilds.
        public string legacyKey;
        public string domainId;
        public string subjectId;
        public string observationId;
        public string policyNamespace;
        public string facetId;
        public bool colony;
        public int pawnId;
        public string sourceInstanceId;
        public List<string> sourceInstanceIds = new List<string>();
        public string specimenId;
        public string contextKey;
        public List<string> contextKeys = new List<string>();
        public int count;
        public int dailyCount;
        public int successCount;
        public int failureCount;
        public int day;
        public int lastTick;
        public string lastSource;
        // 0 identifies the pre-namespaced accrual format. Legacy records keep
        // this value until a runtime lookup can translate them safely.
        public int keyFormatVersion;
        // Legacy records did not retain success/failure history. A false value
        // prevents first-outcome rewards from being granted during migration.
        public bool outcomeHistoryComplete;
        // The pre-V4 key did not encode scope or ownership. Such a record is
        // kept as a shared compatibility state instead of being assigned to an
        // arbitrary personal/colony namespace.
        public bool ownershipMetadataComplete;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref legacyKey, "legacyKey");
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref observationId, "observationId");
            Scribe_Values.Look(ref policyNamespace, "policyNamespace");
            Scribe_Values.Look(ref facetId, "facetId");
            Scribe_Values.Look(ref colony, "colony");
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref sourceInstanceId, "sourceInstanceId");
            Scribe_Collections.Look(ref sourceInstanceIds, "sourceInstanceIds", LookMode.Value);
            Scribe_Values.Look(ref specimenId, "specimenId");
            Scribe_Values.Look(ref contextKey, "contextKey");
            Scribe_Collections.Look(ref contextKeys, "contextKeys", LookMode.Value);
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref dailyCount, "dailyCount");
            Scribe_Values.Look(ref successCount, "successCount");
            Scribe_Values.Look(ref failureCount, "failureCount");
            Scribe_Values.Look(ref day, "day");
            Scribe_Values.Look(ref lastTick, "lastTick");
            Scribe_Values.Look(ref lastSource, "lastSource");
            Scribe_Values.Look(ref keyFormatVersion, "keyFormatVersion");
            Scribe_Values.Look(ref outcomeHistoryComplete, "outcomeHistoryComplete");
            Scribe_Values.Look(ref ownershipMetadataComplete, "ownershipMetadataComplete");
            count = Mathf.Clamp(count, 0, 100000000);
            dailyCount = Mathf.Clamp(dailyCount, 0, 100000000);
            successCount = Mathf.Clamp(successCount, 0, count);
            failureCount = Mathf.Clamp(failureCount, 0, count);
            day = Math.Max(0, day);
            lastTick = Math.Max(0, lastTick);
            sourceInstanceIds = (sourceInstanceIds ?? new List<string>()).Where(value => !value.NullOrEmpty()).Distinct().Take(4096).ToList();
            contextKeys = (contextKeys ?? new List<string>()).Where(value => !value.NullOrEmpty()).Distinct().Take(4096).ToList();
            keyFormatVersion = Math.Max(0, keyFormatVersion);
        }
    }

    internal sealed class KnowledgeStageStateRecord : IExposable
    {
        public string domainId;
        public string subjectId;
        public string stageId;
        public Pawn pawn;
        public bool colony;
        public string contextTypeId;
        public string contextId;
        public int revision;
        public int lastTick;

        public KnowledgeContextKey Context => new KnowledgeContextKey(contextTypeId, contextId);

        public void ExposeData()
        {
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref stageId, "stageId");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref colony, "colony");
            Scribe_Values.Look(ref contextTypeId, "contextTypeId");
            Scribe_Values.Look(ref contextId, "contextId");
            Scribe_Values.Look(ref revision, "revision");
            Scribe_Values.Look(ref lastTick, "lastTick");
            Normalize();
        }

        public void Normalize()
        {
            domainId = domainId?.Trim();
            subjectId = subjectId?.Trim();
            stageId = stageId?.Trim();
            contextTypeId = contextTypeId?.Trim();
            contextId = contextId?.Trim();
            domainId = KnowledgeRegistry.ResolveDomainId(domainId) ?? domainId;
            subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, subjectId) ?? subjectId;
            if (contextTypeId.NullOrEmpty() != contextId.NullOrEmpty())
            {
                contextTypeId = null;
                contextId = null;
            }
            revision = Math.Max(0, revision);
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
            Normalize();
        }

        public void Normalize()
        {
            namespaceId = namespaceId?.Trim();
            domainId = domainId?.Trim();
            trackId = trackId?.Trim();
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
            Normalize();
        }

        public void Normalize()
        {
            consumerId = consumerId?.Trim();
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

    internal readonly struct StageRuntimeKey : IEquatable<StageRuntimeKey>
    {
        public readonly string domainId;
        public readonly string subjectId;
        public readonly int pawnId;
        public readonly bool colony;
        public readonly string contextTypeId;
        public readonly string contextId;

        public StageRuntimeKey(string domainId, string subjectId, Pawn pawn, bool colony, KnowledgeContextKey context)
        {
            this.domainId = domainId;
            this.subjectId = subjectId;
            pawnId = pawn?.thingIDNumber ?? 0;
            this.colony = colony;
            contextTypeId = context.typeId;
            contextId = context.stableId;
        }

        public bool Equals(StageRuntimeKey other) => pawnId == other.pawnId && colony == other.colony && domainId == other.domainId &&
            subjectId == other.subjectId && contextTypeId == other.contextTypeId && contextId == other.contextId;
        public override bool Equals(object obj) => obj is StageRuntimeKey other && Equals(other);
        public override int GetHashCode() => (((((domainId?.GetHashCode() ?? 0) * 397 ^ (subjectId?.GetHashCode() ?? 0)) * 397 ^
            pawnId) * 397 ^ (colony ? 1 : 0)) * 397 ^ (contextTypeId?.GetHashCode() ?? 0)) * 397 ^ (contextId?.GetHashCode() ?? 0);
    }

    public sealed partial class GameComponent_KnowledgeFramework
    {
        // Version 4 adds contextual stage records and the legacy accrual
        // compatibility metadata. Existing lists remain load-compatible.
        internal const int CurrentV3SchemaVersion = 4;
        private List<KnowledgeClaimStateRecord> claimsV3 = new List<KnowledgeClaimStateRecord>();
        private List<KnowledgeContextFacetStateRecord> contextFacetsV3 = new List<KnowledgeContextFacetStateRecord>();
        private List<KnowledgeMilestoneStateRecord> milestonesV3 = new List<KnowledgeMilestoneStateRecord>();
        private List<KnowledgeStageStateRecord> stagesV3 = new List<KnowledgeStageStateRecord>();
        private List<KnowledgeSubjectRelationStateRecord> relationsV3 = new List<KnowledgeSubjectRelationStateRecord>();
        private List<KnowledgeAccrualStateRecord> accrualV3 = new List<KnowledgeAccrualStateRecord>();
        private List<KnowledgeSubjectOverrideRecord> subjectOverridesV3 = new List<KnowledgeSubjectOverrideRecord>();
        private List<KnowledgeSharedExpertiseStateRecord> sharedExpertiseV3 = new List<KnowledgeSharedExpertiseStateRecord>();
        private List<KnowledgeMigrationStateRecord> consumerMigrationsV3 = new List<KnowledgeMigrationStateRecord>();
        private Dictionary<ClaimRuntimeKey, KnowledgeClaimStateRecord> claimsV3Index;
        private Dictionary<ContextFacetRuntimeKey, KnowledgeContextFacetStateRecord> contextFacetsV3Index;
        private Dictionary<MilestoneRuntimeKey, KnowledgeMilestoneStateRecord> milestonesV3Index;
        private Dictionary<StageRuntimeKey, KnowledgeStageStateRecord> stagesV3Index;
        private Dictionary<string, KnowledgeAccrualStateRecord> accrualV3Index;
        private Dictionary<string, KnowledgeSharedExpertiseStateRecord> sharedExpertiseV3Index;
        private Dictionary<string, KnowledgeMigrationStateRecord> consumerMigrationsV3Index;

        private void InitializeV3()
        {
            claimsV3Index = new Dictionary<ClaimRuntimeKey, KnowledgeClaimStateRecord>();
            contextFacetsV3Index = new Dictionary<ContextFacetRuntimeKey, KnowledgeContextFacetStateRecord>();
            milestonesV3Index = new Dictionary<MilestoneRuntimeKey, KnowledgeMilestoneStateRecord>();
            stagesV3Index = new Dictionary<StageRuntimeKey, KnowledgeStageStateRecord>();
            accrualV3Index = new Dictionary<string, KnowledgeAccrualStateRecord>(StringComparer.Ordinal);
            sharedExpertiseV3Index = new Dictionary<string, KnowledgeSharedExpertiseStateRecord>(StringComparer.Ordinal);
            consumerMigrationsV3Index = new Dictionary<string, KnowledgeMigrationStateRecord>(StringComparer.Ordinal);
        }

        private void ExposeV3Data()
        {
            Scribe_Collections.Look(ref claimsV3, "knowledgeFrameworkV3Claims", LookMode.Deep);
            Scribe_Collections.Look(ref contextFacetsV3, "knowledgeFrameworkV3ContextFacets", LookMode.Deep);
            Scribe_Collections.Look(ref milestonesV3, "knowledgeFrameworkV3Milestones", LookMode.Deep);
            Scribe_Collections.Look(ref stagesV3, "knowledgeFrameworkV3Stages", LookMode.Deep);
            Scribe_Collections.Look(ref relationsV3, "knowledgeFrameworkV3Relations", LookMode.Deep);
            Scribe_Collections.Look(ref accrualV3, "knowledgeFrameworkV3Accrual", LookMode.Deep);
            Scribe_Collections.Look(ref subjectOverridesV3, "knowledgeFrameworkV3SubjectOverrides", LookMode.Deep);
            Scribe_Collections.Look(ref sharedExpertiseV3, "knowledgeFrameworkV3SharedExpertise", LookMode.Deep);
            Scribe_Collections.Look(ref consumerMigrationsV3, "knowledgeFrameworkV3Migrations", LookMode.Deep);
        }

        internal void RebuildV3Indexes()
        {
            InitializeV3();
            claimsV3 = claimsV3 ?? new List<KnowledgeClaimStateRecord>();
            contextFacetsV3 = contextFacetsV3 ?? new List<KnowledgeContextFacetStateRecord>();
            milestonesV3 = milestonesV3 ?? new List<KnowledgeMilestoneStateRecord>();
            stagesV3 = stagesV3 ?? new List<KnowledgeStageStateRecord>();
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
                int limit = Math.Max(1, Math.Min(4096, KnowledgeRegistry.Schema(item.domainId)?.Claim(item.claimId)?.measurementHistoryLimit ?? 64));
                List<KnowledgeMeasurementRecord> normalizedMeasurements = item.measurements.Where(value => value != null).GroupBy(MeasurementIdentity)
                    .Select(group => group.OrderByDescending(value => value.tick).First()).OrderBy(value => value.tick).ToList();
                item.measurements = normalizedMeasurements.Skip(Math.Max(0, normalizedMeasurements.Count - limit)).ToList();
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
                    existing.revision = Math.Max(existing.revision, item.revision);
                    existing.Normalize();
                }
            }
            contextFacetsV3 = contextFacetsV3Index.Values.ToList();
            foreach (KnowledgeMilestoneStateRecord item in milestonesV3.Where(item => item != null))
            {
                item.Normalize();
                MilestoneRuntimeKey key = new MilestoneRuntimeKey(item.domainId, item.subjectId, item.trackId, item.milestoneId, item.pawn,
                    new KnowledgeContextKey(item.contextTypeId, item.contextId));
                if (milestonesV3Index.TryGetValue(key, out KnowledgeMilestoneStateRecord existing)) MergeMilestone(existing, item);
                else milestonesV3Index[key] = item;
            }
            milestonesV3 = milestonesV3Index.Values.ToList();
            foreach (KnowledgeStageStateRecord item in stagesV3.Where(item => item != null))
            {
                item.Normalize();
                if (item.domainId.NullOrEmpty() || item.subjectId.NullOrEmpty() || item.stageId.NullOrEmpty()) continue;
                // StageV3 is exclusively contextual state. Do not allow an
                // old/corrupt record for a global stage to become contextual
                // proof after a save reload.
                if (KnowledgeRegistry.Schema(item.domainId)?.Stage(item.stageId)?.contextSensitive != true) continue;
                StageRuntimeKey key = new StageRuntimeKey(item.domainId, item.subjectId, item.pawn, item.colony, item.Context);
                if (stagesV3Index.TryGetValue(key, out KnowledgeStageStateRecord existing)) MergeStage(existing, item);
                else stagesV3Index[key] = item;
            }
            stagesV3 = stagesV3Index.Values.OrderBy(item => item.domainId, StringComparer.Ordinal)
                .ThenBy(item => item.subjectId, StringComparer.Ordinal).ThenBy(item => item.colony)
                .ThenBy(item => item.pawn?.thingIDNumber ?? 0).ThenBy(item => item.Context.ToString(), StringComparer.Ordinal).ToList();
            Dictionary<string, KnowledgeSubjectRelationStateRecord> normalizedRelations = new Dictionary<string, KnowledgeSubjectRelationStateRecord>(StringComparer.Ordinal);
            foreach (KnowledgeSubjectRelationStateRecord item in relationsV3.Where(item => item != null))
            {
                item.Normalize();
                string key = string.Join("\n", item.domainId, item.fromSubjectId, item.toDomainId, item.toSubjectId, item.relationTypeId,
                    item.contextTypeId, item.contextId);
                if (!normalizedRelations.TryGetValue(key, out KnowledgeSubjectRelationStateRecord existing) ||
                    item.tick > existing.tick || item.tick == existing.tick && string.CompareOrdinal(item.source, existing.source) > 0)
                    normalizedRelations[key] = item;
            }
            relationsV3 = normalizedRelations.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value).ToList();
            Dictionary<string, KnowledgeAccrualStateRecord> normalizedAccrual = new Dictionary<string, KnowledgeAccrualStateRecord>(StringComparer.Ordinal);
            foreach (KnowledgeAccrualStateRecord item in accrualV3.Where(item => item != null))
            {
                NormalizeAccrual(item);
                if (item.key.NullOrEmpty()) continue;
                if (normalizedAccrual.TryGetValue(item.key, out KnowledgeAccrualStateRecord existing)) MergeAccrual(existing, item);
                else normalizedAccrual[item.key] = item;
            }
            accrualV3Index = normalizedAccrual;
            accrualV3 = normalizedAccrual.Values.OrderBy(item => item.key, StringComparer.Ordinal).ToList();
            KnowledgeAccrualService.EnforceStateLimits(this);
            Dictionary<string, KnowledgeSharedExpertiseStateRecord> normalizedExpertise = new Dictionary<string, KnowledgeSharedExpertiseStateRecord>(StringComparer.Ordinal);
            foreach (KnowledgeSharedExpertiseStateRecord item in sharedExpertiseV3.Where(item => item != null && !item.namespaceId.NullOrEmpty()))
            {
                item.Normalize();
                string key = SharedExpertiseKey(item.namespaceId, item.domainId, item.trackId, item.pawn);
                if (normalizedExpertise.TryGetValue(key, out KnowledgeSharedExpertiseStateRecord existing)) existing.amount = Math.Max(existing.amount, item.amount);
                else normalizedExpertise[key] = item;
            }
            sharedExpertiseV3Index = normalizedExpertise;
            sharedExpertiseV3 = normalizedExpertise.Values.OrderBy(item => SharedExpertiseKey(item.namespaceId, item.domainId, item.trackId, item.pawn), StringComparer.Ordinal).ToList();
            foreach (KnowledgeMigrationStateRecord item in consumerMigrationsV3.Where(item => item != null && !item.consumerId.NullOrEmpty()))
            {
                item.Normalize();
                if (consumerMigrationsV3Index.TryGetValue(item.consumerId, out KnowledgeMigrationStateRecord existing))
                    MergeMigration(existing, item);
                else consumerMigrationsV3Index[item.consumerId] = item;
            }
            consumerMigrationsV3 = consumerMigrationsV3Index.Values.OrderBy(item => item.consumerId, StringComparer.Ordinal).ToList();
            subjectOverridesV3 = subjectOverridesV3.Where(item => item != null && !item.domainId.NullOrEmpty() && !item.id.NullOrEmpty())
                .GroupBy(item => item.domainId + "\n" + item.id, StringComparer.Ordinal)
                .Select(group => group.Last()).OrderBy(item => item.domainId, StringComparer.Ordinal)
                .ThenBy(item => item.id, StringComparer.Ordinal).ToList();
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

        internal KnowledgeStageStateRecord StageV3(string domainId, string subjectId, Pawn pawn, bool colony,
            KnowledgeContextKey context, bool create)
        {
            domainId = KnowledgeRegistry.ResolveDomainId(domainId) ?? domainId;
            subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, subjectId) ?? subjectId;
            StageRuntimeKey key = new StageRuntimeKey(domainId, subjectId, pawn, colony, context);
            if (stagesV3Index.TryGetValue(key, out KnowledgeStageStateRecord result) || !create) return result;
            result = new KnowledgeStageStateRecord { domainId = domainId, subjectId = subjectId, pawn = pawn, colony = colony,
                contextTypeId = context.typeId, contextId = context.stableId };
            stagesV3.Add(result);
            stagesV3Index[key] = result;
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

        internal KnowledgeAccrualStateRecord AccrualLegacyV3(string legacyKey) =>
            AccrualCompatibilityV3(new[] { legacyKey });

        internal KnowledgeAccrualStateRecord AccrualCompatibilityV3(IEnumerable<string> legacyKeys)
        {
            HashSet<string> keys = new HashSet<string>((legacyKeys ?? Array.Empty<string>()).Where(item => !item.NullOrEmpty()), StringComparer.Ordinal);
            List<KnowledgeAccrualStateRecord> matches = accrualV3.Where(item => item != null &&
                (keys.Contains(item.key) || keys.Contains(item.legacyKey))).OrderBy(item => item.key, StringComparer.Ordinal).ToList();
            if (matches.Count == 0) return null;
            KnowledgeAccrualStateRecord result = matches[0];
            foreach (KnowledgeAccrualStateRecord duplicate in matches.Skip(1).ToList())
            {
                MergeAccrual(result, duplicate);
                accrualV3.Remove(duplicate);
                if (!duplicate.key.NullOrEmpty()) accrualV3Index.Remove(duplicate.key);
            }
            if (!result.key.NullOrEmpty()) accrualV3Index[result.key] = result;
            return result;
        }

        internal KnowledgeAccrualStateRecord MigrateAccrualV3(KnowledgeAccrualStateRecord record, string modernKey)
        {
            if (record == null || modernKey.NullOrEmpty()) return record;
            if (record.key == modernKey) return record;
            if (record.keyFormatVersion < 2 && record.legacyKey.NullOrEmpty()) record.legacyKey = record.key;
            if (accrualV3Index.TryGetValue(modernKey, out KnowledgeAccrualStateRecord existing) && existing != record)
            {
                MergeAccrual(existing, record);
                accrualV3.Remove(record);
                return existing;
            }
            accrualV3Index.Remove(record.key);
            record.key = modernKey;
            record.keyFormatVersion = 2;
            record.ownershipMetadataComplete = true;
            accrualV3Index[modernKey] = record;
            return record;
        }

        internal void RemoveAccrualV3(string key)
        {
            if (key.NullOrEmpty()) return;
            accrualV3.RemoveAll(item => item?.key == key);
            accrualV3Index.Remove(key);
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

        internal IEnumerable<KnowledgeContextKey> ContextKeysV3(string domainId, string subjectId, Pawn pawn, bool colony)
        {
            HashSet<KnowledgeContextKey> result = new HashSet<KnowledgeContextKey>();
            foreach (KnowledgeContextFacetStateRecord item in contextFacetsV3.Where(value => value != null && value.domainId == domainId &&
                value.subjectId == subjectId && value.colony == colony && (colony || pawn == null || value.pawn == pawn)))
            {
                KnowledgeContextKey context = new KnowledgeContextKey(item.contextTypeId, item.contextId);
                if (!context.IsPartial) result.Add(context);
            }
            foreach (KnowledgeClaimStateRecord item in claimsV3.Where(value => value != null && value.domainId == domainId &&
                value.subjectId == subjectId && value.colony == colony && (colony || pawn == null || value.pawn == pawn)))
            {
                KnowledgeContextKey context = new KnowledgeContextKey(item.contextTypeId, item.contextId);
                if (!context.IsPartial) result.Add(context);
            }
            foreach (KnowledgeStageStateRecord item in stagesV3.Where(value => value != null && value.domainId == domainId &&
                value.subjectId == subjectId && value.colony == colony && (colony || pawn == null || value.pawn == pawn)))
                if (!item.Context.IsPartial) result.Add(item.Context);
            foreach (KnowledgeMilestoneStateRecord item in milestonesV3.Where(value => value != null && value.domainId == domainId &&
                value.subjectId == subjectId && value.pawn == (colony ? null : pawn)))
            {
                KnowledgeContextKey context = new KnowledgeContextKey(item.contextTypeId, item.contextId);
                if (!context.IsPartial) result.Add(context);
            }
            foreach (KnowledgeSubjectRelationStateRecord item in relationsV3.Where(value => value != null && value.domainId == domainId &&
                (value.fromSubjectId == subjectId || value.toSubjectId == subjectId)))
            {
                KnowledgeContextKey context = new KnowledgeContextKey(item.contextTypeId, item.contextId);
                if (!context.IsPartial) result.Add(context);
            }
            return result;
        }

        internal IEnumerable<KnowledgeMilestoneStateRecord> MilestoneRecordsV3(string domainId, string subjectId = null, Pawn pawn = null) =>
            milestonesV3.Where(item => item != null && item.domainId == domainId && (subjectId.NullOrEmpty() || item.subjectId == subjectId) &&
                (pawn == null || item.pawn == pawn));

        internal IEnumerable<KnowledgeMilestoneStateRecord> MilestoneRecordsForScopeV3(string domainId, string subjectId, Pawn pawn, bool colony) =>
            milestonesV3.Where(item => item != null && item.domainId == domainId && item.subjectId == subjectId &&
                 item.pawn == (colony ? null : pawn));

        internal IEnumerable<KnowledgeStageStateRecord> StageRecordsV3(string domainId, string subjectId = null, Pawn pawn = null,
            bool colony = false) => stagesV3.Where(item => item != null && item.domainId == domainId && item.colony == colony &&
                (subjectId.NullOrEmpty() || item.subjectId == subjectId) && (colony || pawn == null || item.pawn == pawn));

        internal void RemoveStageV3(string domainId, string subjectId, Pawn pawn, bool colony, KnowledgeContextKey context)
        {
            domainId = KnowledgeRegistry.ResolveDomainId(domainId) ?? domainId;
            subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, subjectId) ?? subjectId;
            StageRuntimeKey key = new StageRuntimeKey(domainId, subjectId, pawn, colony, context);
            stagesV3.RemoveAll(item => item != null && new StageRuntimeKey(item.domainId, item.subjectId, item.pawn, item.colony, item.Context).Equals(key));
            stagesV3Index.Remove(key);
        }

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
            contextFacetsV3.Count * 128L + milestonesV3.Count * 160L + stagesV3.Count * 128L + relationsV3.Count * 192L + accrualV3.Count * 128L +
            subjectOverridesV3.Count * 256L + sharedExpertiseV3.Count * 96L + consumerMigrationsV3.Count * 64L;

        internal int V3ClaimCount => claimsV3.Count;
        internal int V3MeasurementCount => claimsV3.Sum(item => item?.measurements?.Count ?? 0);
        internal int V3ContextCount => claimsV3.Select(item => item?.contextTypeId + "\n" + item?.contextId).Where(item => !item.NullOrEmpty()).Distinct().Count();
        internal int V3MilestoneCount => milestonesV3.Count;
        internal int V3StageCount => stagesV3.Count;
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
            stagesV3.RemoveAll(item => item?.domainId == domainId);
            relationsV3.RemoveAll(item => item?.domainId == domainId || item?.toDomainId == domainId);
            string canonicalDomain = KnowledgeRegistry.ResolveDomainId(domainId) ?? domainId;
            accrualV3.RemoveAll(item =>
            {
                if (item == null) return false;
                string recordDomain = KnowledgeRegistry.ResolveDomainId(item.domainId) ?? item.domainId;
                string keyDomain = item.key.NullOrEmpty() ? null : item.key.Split(new[] { '\n' }, 2)[0];
                string legacyDomain = item.legacyKey.NullOrEmpty() ? null : item.legacyKey.Split(new[] { '\n' }, 2)[0];
                return recordDomain == canonicalDomain || KnowledgeRegistry.ResolveDomainId(keyDomain) == canonicalDomain ||
                    KnowledgeRegistry.ResolveDomainId(legacyDomain) == canonicalDomain;
            });
            subjectOverridesV3.RemoveAll(item => item?.domainId == domainId);
            sharedExpertiseV3.RemoveAll(item => item?.domainId == domainId);
            RebuildV3Indexes();
        }

        private static string SharedExpertiseKey(string namespaceId, string domainId, string trackId, Pawn pawn) => namespaceId + "\n" + domainId + "\n" + trackId + "\n" + (pawn?.thingIDNumber ?? 0);

        private static string MeasurementIdentity(KnowledgeMeasurementRecord value)
        {
            if (value == null) return string.Empty;
            return string.Join("\n", value.domainId, value.subjectId, value.facetId, value.claimId, value.observer?.thingIDNumber ?? 0,
                value.scope, value.contextTypeId, value.contextId, value.valueType, value.ToValue().StableKey(), value.quality,
                value.evidenceWeight, value.confidenceFactor, value.disposition, value.source, value.sourceInstanceId,
                value.methodId, value.reasonId, value.specimenId, value.summary, value.tick, value.documented, value.revealed);
        }

        private static void MergeMilestone(KnowledgeMilestoneStateRecord target, KnowledgeMilestoneStateRecord source)
        {
            target.available |= source.available;
            target.started |= source.started;
            target.interrupted |= source.interrupted;
            target.completed |= source.completed;
            target.startTick = target.startTick == 0 ? source.startTick : source.startTick == 0 ? target.startTick : Math.Min(target.startTick, source.startTick);
            target.completionTick = Math.Max(target.completionTick, source.completionTick);
            target.progress = Math.Max(target.progress, source.progress);
            target.bestHistoricalValue = Math.Max(target.bestHistoricalValue, source.bestHistoricalValue);
            if (source.revision >= target.revision)
            {
                target.interruptionReason = source.interruptionReason ?? target.interruptionReason;
                target.completingPawn = source.completingPawn ?? target.completingPawn;
                target.revision = source.revision;
            }
        }

        private static void MergeStage(KnowledgeStageStateRecord target, KnowledgeStageStateRecord source)
        {
            KnowledgeSchema schema = KnowledgeRegistry.Schema(target.domainId);
            int targetOrder = schema?.Stage(target.stageId)?.order ?? int.MinValue;
            int sourceOrder = schema?.Stage(source.stageId)?.order ?? int.MinValue;
            if (sourceOrder > targetOrder || sourceOrder == targetOrder &&
                (source.lastTick > target.lastTick || source.lastTick == target.lastTick &&
                    string.CompareOrdinal(source.stageId, target.stageId) > 0))
            {
                target.stageId = source.stageId;
            }
            target.revision = Math.Max(target.revision, source.revision);
            target.lastTick = Math.Max(target.lastTick, source.lastTick);
        }

        private static void NormalizeAccrual(KnowledgeAccrualStateRecord item)
        {
            // Newline is the delimiter of the pre-V4 key and a trailing
            // newline represents an empty/global context. Do not use Trim()
            // here or the compatibility lookup would lose that dimension.
            item.key = item.key?.Trim(' ', '\t', '\r');
            item.legacyKey = item.legacyKey?.Trim(' ', '\t', '\r');
            string originalKey = item.key;
            string[] legacyParts = item.key?.Split(new[] { '\n' }, StringSplitOptions.None);
            bool currentLayout = false;
            if (legacyParts != null && legacyParts.Length > 1)
            {
                item.domainId = item.domainId.NullOrEmpty() ? legacyParts[0] : item.domainId;
                item.subjectId = item.subjectId.NullOrEmpty() ? legacyParts[1] : item.subjectId;
                int parsedPawn = 0;
                currentLayout = legacyParts.Length > 5 && (legacyParts[4] == "P" || legacyParts[4] == "C") &&
                    int.TryParse(legacyParts[5], out parsedPawn);
                if (currentLayout)
                {
                    if (item.policyNamespace.NullOrEmpty()) item.policyNamespace = legacyParts[2];
                    if (item.facetId.NullOrEmpty()) item.facetId = legacyParts[3];
                    if (legacyParts[4] == "C") item.colony = true;
                    if (item.pawnId == 0) item.pawnId = parsedPawn;
                }
                else if (item.facetId.NullOrEmpty() && legacyParts.Length > 2)
                    item.facetId = legacyParts[2];
            }
            if (!currentLayout && !originalKey.NullOrEmpty())
            {
                item.legacyKey = item.legacyKey.NullOrEmpty() ? originalKey : item.legacyKey;
                item.keyFormatVersion = 1;
                item.outcomeHistoryComplete = false;
                item.ownershipMetadataComplete = false;
            }
            else if (currentLayout && item.keyFormatVersion == 0)
            {
                item.keyFormatVersion = 2;
                item.outcomeHistoryComplete = true;
                item.ownershipMetadataComplete = true;
            }
            else if (currentLayout && item.keyFormatVersion >= 2)
            {
                // Records written by the first namespaced format predate the
                // explicit ownership marker, but their P/C key already proves
                // that scope and pawn dimensions were retained.
                item.ownershipMetadataComplete = true;
            }
            item.domainId = item.domainId?.Trim();
            item.subjectId = item.subjectId?.Trim();
            item.observationId = item.observationId?.Trim();
            item.policyNamespace = item.policyNamespace?.Trim();
            item.facetId = item.facetId?.Trim();
            item.sourceInstanceId = item.sourceInstanceId?.Trim();
            item.specimenId = item.specimenId?.Trim();
            item.contextKey = item.contextKey?.Trim();
            string historicalDomainId = item.domainId;
            string historicalSubjectId = item.subjectId;
            item.domainId = KnowledgeRegistry.ResolveDomainId(item.domainId) ?? item.domainId;
            item.subjectId = KnowledgeRegistry.ResolveSubjectId(item.domainId, item.subjectId) ?? item.subjectId;
            if (currentLayout && !originalKey.NullOrEmpty() &&
                (historicalDomainId != item.domainId || historicalSubjectId != item.subjectId))
                item.legacyKey = item.legacyKey.NullOrEmpty() ? originalKey : item.legacyKey;
            KnowledgeObservationDef policyDefinition = ResolveAccrualDefinition(item);
            KnowledgeAccrualPolicy policy = policyDefinition?.accrualPolicy;
            if (currentLayout && policy != null)
            {
                int dimension = 6;
                if (policy.uniquePerSourceInstance || policy.uniquePerPawnAndSourceInstance)
                {
                    if (item.sourceInstanceId.NullOrEmpty() && legacyParts.Length > dimension) item.sourceInstanceId = legacyParts[dimension];
                    dimension++;
                }
                if (policy.uniquePerSubjectAndContext && item.contextKey.NullOrEmpty() && legacyParts.Length > dimension)
                    item.contextKey = legacyParts[dimension];
            }
            item.policyNamespace = item.policyNamespace.NullOrEmpty() ? (item.observationId.NullOrEmpty() ? "legacy" : item.observationId) : item.policyNamespace;
            item.observationId = item.observationId.NullOrEmpty() ? (item.policyNamespace == "legacy" ? null : item.policyNamespace) : item.observationId;
            item.facetId = item.facetId.NullOrEmpty() ? "legacy" : item.facetId;
            item.contextKey = item.contextKey.NullOrEmpty() ? "<global>" : item.contextKey;
            item.count = Math.Max(0, Math.Min(100000000, item.count));
            item.dailyCount = Math.Max(0, Math.Min(item.count, item.dailyCount));
            item.successCount = Math.Max(0, Math.Min(item.count, item.successCount));
            item.failureCount = Math.Max(0, Math.Min(item.count - item.successCount, item.failureCount));
            item.day = Math.Max(0, item.day);
            item.lastTick = Math.Max(0, item.lastTick);
            item.pawnId = Math.Max(0, item.pawnId);
            item.sourceInstanceIds = (item.sourceInstanceIds ?? new List<string>()).Concat(new[] { item.sourceInstanceId })
                .Where(value => !value.NullOrEmpty()).Distinct().Take(4096).ToList();
            item.contextKeys = (item.contextKeys ?? new List<string>()).Concat(new[] { item.contextKey })
                .Where(value => !value.NullOrEmpty()).Distinct().Take(4096).ToList();
            policyDefinition = ResolveAccrualDefinition(item);
            policy = policyDefinition?.accrualPolicy;
            if (item.facetId == "legacy" && policyDefinition?.facetIds != null && policyDefinition.facetIds.Count > 0)
                item.facetId = policyDefinition.facetIds.FirstOrDefault(value => !value.NullOrEmpty()) ?? item.facetId;
            if (policyDefinition != null)
            {
                if (item.observationId.NullOrEmpty()) item.observationId = policyDefinition.StableId;
                if (item.policyNamespace == "legacy") item.policyNamespace = policyDefinition.StableId;
            }
            if (!currentLayout && !item.ownershipMetadataComplete && item.pawnId > 0 && policy != null)
            {
                // A legacy pawn reference proves personal ownership. A zero
                // pawn reference could be either colony or personal/global in
                // the old format and must remain on the compatibility key.
                item.ownershipMetadataComplete = true;
            }
            // Old keys do not encode scope or pawn ownership. Keep them as a
            // shared compatibility state; assigning one to the current scope
            // would let the other scope repeat capped or unique rewards.
            item.key = policy != null && (currentLayout || item.ownershipMetadataComplete)
                ? KnowledgeAccrualService.BuildKey(item.domainId, item.subjectId, item.facetId, item.colony, item.pawnId,
                    item.sourceInstanceId, item.contextKey, policy, item.policyNamespace)
                : item.key.NullOrEmpty()
                    ? string.Join("\n", new[] { item.domainId ?? string.Empty, item.subjectId ?? string.Empty,
                     item.policyNamespace, item.facetId, item.colony ? "C" : "P", item.pawnId.ToString() })
                     : item.key;
            if (item.ownershipMetadataComplete && item.legacyKey == item.key) item.legacyKey = null;
        }

        private static KnowledgeObservationDef ResolveAccrualDefinition(KnowledgeAccrualStateRecord item)
        {
            KnowledgeSchema schema = KnowledgeRegistry.Schema(item.domainId);
            if (schema == null) return null;
            KnowledgeObservationDef direct = schema.Observation(item.observationId ?? item.policyNamespace);
            if (direct?.accrualPolicy != null) return direct;
            List<KnowledgeObservationDef> candidates = schema.observations.Where(value => value?.accrualPolicy != null).ToList();
            if (!item.facetId.NullOrEmpty() && item.facetId != "legacy")
            {
                List<KnowledgeObservationDef> matchingFacet = candidates.Where(value => value.facetIds == null || value.facetIds.Count == 0 ||
                    value.facetIds.Contains(item.facetId)).ToList();
                if (matchingFacet.Count == 1) return matchingFacet[0];
                candidates = matchingFacet;
            }
            return candidates.Count == 1 ? candidates[0] : null;
        }

        private static void MergeAccrual(KnowledgeAccrualStateRecord target, KnowledgeAccrualStateRecord source)
        {
            target.count = Math.Max(target.count, source.count);
            target.dailyCount = target.day == source.day ? Math.Max(target.dailyCount, source.dailyCount) :
                target.lastTick >= source.lastTick ? target.dailyCount : source.dailyCount;
            target.successCount = Math.Max(target.successCount, source.successCount);
            target.failureCount = Math.Max(target.failureCount, source.failureCount);
            target.keyFormatVersion = Math.Max(target.keyFormatVersion, source.keyFormatVersion);
            target.outcomeHistoryComplete &= source.outcomeHistoryComplete;
            target.ownershipMetadataComplete &= source.ownershipMetadataComplete;
            if (target.legacyKey.NullOrEmpty() || !source.legacyKey.NullOrEmpty() &&
                string.CompareOrdinal(source.legacyKey, target.legacyKey) < 0) target.legacyKey = source.legacyKey;
            target.sourceInstanceIds = target.sourceInstanceIds.Concat(source.sourceInstanceIds ?? new List<string>())
                .Concat(new[] { source.sourceInstanceId }).Where(value => !value.NullOrEmpty()).Distinct().Take(4096).ToList();
            target.contextKeys = target.contextKeys.Concat(source.contextKeys ?? new List<string>())
                .Concat(new[] { source.contextKey }).Where(value => !value.NullOrEmpty()).Distinct().Take(4096).ToList();
            if (source.lastTick > target.lastTick || source.lastTick == target.lastTick && string.CompareOrdinal(source.lastSource, target.lastSource) > 0)
            {
                target.lastTick = source.lastTick;
                target.lastSource = source.lastSource;
                target.sourceInstanceId = source.sourceInstanceId;
                target.specimenId = source.specimenId;
                target.contextKey = source.contextKey;
                target.day = source.day;
            }
            NormalizeAccrual(target);
        }

        private static void MergeMigration(KnowledgeMigrationStateRecord target, KnowledgeMigrationStateRecord source)
        {
            if (source.version > target.version ||
                source.version == target.version && source.committed && !target.committed ||
                source.version == target.version && source.committed == target.committed && source.tick > target.tick)
            {
                target.version = source.version;
                target.committed = source.committed;
                target.tick = source.tick;
            }
        }
    }

    internal static class KnowledgeV3PersistenceBridge
    {
        internal static void PersistSubjectOverride(string domainId, KnowledgeSubjectRegistration value) =>
            GameComponent_KnowledgeFramework.Current?.PersistSubjectOverrideV3(domainId, value);
    }
}
