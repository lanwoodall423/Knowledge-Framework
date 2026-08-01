using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    /// <summary>One observed event. Use KnowledgeTransaction for atomic multi-facet events.</summary>
    public sealed class KnowledgeObservation
    {
        public Pawn observer;
        public string domainId;
        public string subjectId;
        public string facetId;
        public string observationId;
        public string methodId;
        public float quality = 1f;
        public float novelty = 1f;
        public float repetition = 1f;
        public bool success = true;
        public float environmentalDifficulty = 1f;
        public float sourceReliability = 1f;
        public KnowledgeEvidenceDisposition disposition = KnowledgeEvidenceDisposition.Supporting;
        public IReadOnlyList<Pawn> witnesses;
        public bool shareable = true;
        public bool documented;
        public bool targetColony;
        public string reasonId;
        public string source;
        public string sourceInstanceId;
        public string contextId;
        public string contextTypeId;
        public KnowledgeContextKey context;
        public string specimenId;
        public string summary;
        public IReadOnlyDictionary<string, string> metadata;
        public IReadOnlyList<KnowledgeMeasurement> claimMeasurements;
        public KnowledgeWitnessDistribution witnessDistribution;
        public string sharedExpertiseNamespaceId;
        public float sharedExpertiseWeight = 1f;
        public float directKnowledge;
        public float directExpertise;
        public float directFamiliarity;
        public string expertiseTrackId;
        public bool suppressConfiguredKnowledge;
        public bool notify = true;
    }

    public sealed class KnowledgeTransaction
    {
        private readonly List<KnowledgeObservation> observations = new List<KnowledgeObservation>();
        public string source;
        public string transactionId;
        public bool notify = true;
        public IReadOnlyList<KnowledgeObservation> Observations => observations;

        public KnowledgeTransaction Add(KnowledgeObservation observation)
        {
            if (observation != null) observations.Add(observation);
            return this;
        }

        public KnowledgeTransaction AddRange(IEnumerable<KnowledgeObservation> values)
        {
            if (values != null) observations.AddRange(values.Where(value => value != null));
            return this;
        }
    }

    public sealed class KnowledgeFacetSnapshotV2
    {
        private static readonly IReadOnlyDictionary<string, int> EmptyEventCounts =
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal));
        public readonly string domainId;
        public readonly string subjectId;
        public readonly string facetId;
        public readonly Pawn pawn;
        public readonly KnowledgeScope scope;
        public readonly float amount;
        public readonly float completeness;
        public readonly float confidence;
        public readonly float directAmount;
        public readonly float derivedAmount;
        public readonly bool provisional;
        public readonly int evidenceCount;
        public readonly int successCount;
        public readonly int failureCount;
        public readonly int revision;
        public readonly IReadOnlyList<KnowledgeEvidenceAggregateSnapshot> aggregates;
        public readonly IReadOnlyList<KnowledgeProvenanceSnapshot> provenance;
        private readonly IReadOnlyDictionary<string, int> eventCounts;

        internal KnowledgeFacetSnapshotV2(string domainId, string subjectId, string facetId, Pawn pawn, KnowledgeScope scope,
            float directAmount, float derivedAmount, float completeness, float confidence, bool provisional,
            int evidenceCount, int successCount, int failureCount, int revision, IDictionary<string, int> counts)
        {
            this.domainId = domainId;
            this.subjectId = subjectId;
            this.facetId = facetId;
            this.pawn = pawn;
            this.scope = scope;
            this.directAmount = directAmount;
            this.derivedAmount = derivedAmount;
            amount = Math.Max(directAmount, derivedAmount);
            this.completeness = completeness;
            this.confidence = confidence;
            this.provisional = provisional;
            this.evidenceCount = evidenceCount;
            this.successCount = successCount;
            this.failureCount = failureCount;
            this.revision = revision;
            eventCounts = counts == null || counts.Count == 0
                ? EmptyEventCounts
                : new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(counts, StringComparer.Ordinal));
            aggregates = Array.Empty<KnowledgeEvidenceAggregateSnapshot>();
            provenance = Array.Empty<KnowledgeProvenanceSnapshot>();
        }

        internal KnowledgeFacetSnapshotV2(string domainId, string subjectId, string facetId, Pawn pawn, KnowledgeScope scope,
            float directAmount, float derivedAmount, float completeness, float confidence, bool provisional,
            int evidenceCount, int successCount, int failureCount, int revision, IDictionary<string, int> counts,
            IEnumerable<KnowledgeEvidenceAggregateSnapshot> aggregates, IEnumerable<KnowledgeProvenanceSnapshot> provenance)
            : this(domainId, subjectId, facetId, pawn, scope, directAmount, derivedAmount, completeness, confidence, provisional,
                evidenceCount, successCount, failureCount, revision, counts)
        {
            this.aggregates = new ReadOnlyCollection<KnowledgeEvidenceAggregateSnapshot>((aggregates ?? Enumerable.Empty<KnowledgeEvidenceAggregateSnapshot>()).ToList());
            this.provenance = new ReadOnlyCollection<KnowledgeProvenanceSnapshot>((provenance ?? Enumerable.Empty<KnowledgeProvenanceSnapshot>()).ToList());
        }

        public int EventCount(string eventId) => !eventId.NullOrEmpty() && eventCounts.TryGetValue(eventId, out int value) ? value : 0;
        public IReadOnlyDictionary<string, int> EventCounts => eventCounts;
    }

    public sealed class KnowledgeEvidenceAggregateSnapshot
    {
        public readonly string key;
        public readonly int count;
        public readonly int successes;
        public readonly int failures;
        public readonly float qualityTotal;
        public readonly int lastTick;

        internal KnowledgeEvidenceAggregateSnapshot(KnowledgeEvidenceAggregateRecord value)
        {
            key = value.key;
            count = value.count;
            successes = value.successes;
            failures = value.failures;
            qualityTotal = value.qualityTotal;
            lastTick = value.lastTick;
        }
    }

    public sealed class KnowledgeProvenanceSnapshot
    {
        public readonly string source;
        public readonly string reasonId;
        public readonly string methodId;
        public readonly string summary;
        public readonly int tick;
        public readonly IReadOnlyList<Pawn> witnesses;

        internal KnowledgeProvenanceSnapshot(KnowledgeProvenanceRecord value)
        {
            source = value.source;
            reasonId = value.reasonId;
            methodId = value.methodId;
            summary = value.summary;
            tick = value.tick;
            witnesses = new ReadOnlyCollection<Pawn>((value.witnesses ?? new List<Pawn>()).Where(item => item != null).Take(8).ToList());
        }
    }

    public sealed class KnowledgeSubjectSnapshotV2
    {
        public readonly string domainId;
        public readonly string subjectId;
        public readonly Pawn pawn;
        public readonly KnowledgeScope scope;
        public readonly string stageId;
        public readonly float familiarity;
        public readonly bool documented;
        public readonly string documentationSource;
        public readonly int revision;

        internal KnowledgeSubjectSnapshotV2(string domainId, string subjectId, Pawn pawn, KnowledgeScope scope,
            KnowledgeSubjectStateRecord record)
        {
            this.domainId = domainId;
            this.subjectId = subjectId;
            this.pawn = pawn;
            this.scope = scope;
            stageId = record?.stageId;
            familiarity = record?.familiarity ?? 0f;
            documented = record?.documented ?? false;
            documentationSource = record?.documentationSource;
            revision = record?.revision ?? 0;
        }
    }

    public sealed class KnowledgeExpertiseSnapshotV2
    {
        public readonly string domainId;
        public readonly string trackId;
        public readonly Pawn pawn;
        public readonly float amount;
        public readonly KnowledgeRank rank;
        public readonly float progress;
        public readonly int revision;

        internal KnowledgeExpertiseSnapshotV2(string domainId, string trackId, Pawn pawn, float amount,
            KnowledgeRank rank, float progress, int revision)
        {
            this.domainId = domainId;
            this.trackId = trackId;
            this.pawn = pawn;
            this.amount = amount;
            this.rank = rank;
            this.progress = progress;
            this.revision = revision;
        }
    }

    public sealed class KnowledgeRelationshipSnapshot
    {
        public readonly string domainId;
        public readonly string fromDomainId;
        public readonly string toDomainId;
        public readonly string fromSubjectId;
        public readonly string toSubjectId;
        public readonly string facetId;
        public readonly float coefficient;
        public readonly float confidenceCoefficient;
        public readonly float derivedAmount;
        public readonly float derivedConfidence;
        public readonly bool provisional;

        internal KnowledgeRelationshipSnapshot(KnowledgeRelationshipDef definition, KnowledgeFacetSnapshotV2 source)
        {
            domainId = definition.domainId;
            fromDomainId = definition.fromDomainId.NullOrEmpty() ? definition.domainId : definition.fromDomainId;
            toDomainId = definition.toDomainId.NullOrEmpty() ? definition.domainId : definition.toDomainId;
            fromSubjectId = definition.fromSubjectId;
            toSubjectId = definition.toSubjectId;
            facetId = definition.facetId.NullOrEmpty() ? KnowledgeSchema.DefaultFacetId : definition.facetId;
            coefficient = definition.coefficient;
            confidenceCoefficient = definition.confidenceCoefficient;
            derivedAmount = source.directAmount * coefficient;
            derivedConfidence = source.confidence * confidenceCoefficient;
            provisional = definition.provisional;
        }
    }

    public sealed class KnowledgeFacetComparison
    {
        public readonly string facetId;
        public readonly float firstAmount;
        public readonly float secondAmount;
        public readonly float amountDifference;
        public readonly float firstConfidence;
        public readonly float secondConfidence;
        public readonly float confidenceDifference;

        internal KnowledgeFacetComparison(string facetId, KnowledgeFacetSnapshotV2 first, KnowledgeFacetSnapshotV2 second)
        {
            this.facetId = facetId;
            firstAmount = first.amount;
            secondAmount = second.amount;
            amountDifference = first.amount - second.amount;
            firstConfidence = first.confidence;
            secondConfidence = second.confidence;
            confidenceDifference = first.confidence - second.confidence;
        }
    }

    public sealed class KnowledgeComparisonSnapshot
    {
        public readonly string domainId;
        public readonly string firstSubjectId;
        public readonly string secondSubjectId;
        public readonly IReadOnlyList<KnowledgeFacetComparison> facets;
        public readonly IReadOnlyList<KnowledgeComparisonRow> rows;

        internal KnowledgeComparisonSnapshot(string domainId, string firstSubjectId, string secondSubjectId,
            IEnumerable<KnowledgeFacetComparison> facets, IEnumerable<KnowledgeComparisonRow> rows = null)
        {
            this.domainId = domainId;
            this.firstSubjectId = firstSubjectId;
            this.secondSubjectId = secondSubjectId;
            this.facets = new ReadOnlyCollection<KnowledgeFacetComparison>((facets ?? Enumerable.Empty<KnowledgeFacetComparison>()).ToList());
            this.rows = new ReadOnlyCollection<KnowledgeComparisonRow>((rows ?? Enumerable.Empty<KnowledgeComparisonRow>()).ToList());
        }
    }

    public sealed class KnowledgeChange
    {
        public readonly string domainId;
        public readonly string subjectId;
        public readonly string facetId;
        public readonly Pawn pawn;
        public readonly KnowledgeScope scope;
        public readonly float oldAmount;
        public readonly float newAmount;
        public readonly float oldConfidence;
        public readonly float newConfidence;
        public readonly string oldStageId;
        public readonly string newStageId;

        internal KnowledgeChange(string domainId, string subjectId, string facetId, Pawn pawn, KnowledgeScope scope,
            float oldAmount, float newAmount, float oldConfidence, float newConfidence, string oldStageId, string newStageId)
        {
            this.domainId = domainId;
            this.subjectId = subjectId;
            this.facetId = facetId;
            this.pawn = pawn;
            this.scope = scope;
            this.oldAmount = oldAmount;
            this.newAmount = newAmount;
            this.oldConfidence = oldConfidence;
            this.newConfidence = newConfidence;
            this.oldStageId = oldStageId;
            this.newStageId = newStageId;
        }
    }

    public sealed class KnowledgeTransactionResult
    {
        public readonly bool success;
        public readonly string error;
        public readonly int revision;
        public readonly IReadOnlyList<KnowledgeChange> changes;
        public readonly IReadOnlyList<string> activatedInsightIds;

        internal KnowledgeTransactionResult(bool success, string error, int revision,
            IEnumerable<KnowledgeChange> changes = null, IEnumerable<string> insights = null)
        {
            this.success = success;
            this.error = error;
            this.revision = revision;
            this.changes = new ReadOnlyCollection<KnowledgeChange>((changes ?? Enumerable.Empty<KnowledgeChange>()).ToList());
            activatedInsightIds = new ReadOnlyCollection<string>((insights ?? Enumerable.Empty<string>()).ToList());
        }
    }

    public sealed class KnowledgeBatchChangedEvent
    {
        public readonly string source;
        public readonly string transactionId;
        public readonly int revision;
        public readonly IReadOnlyList<KnowledgeChange> changes;

        internal KnowledgeBatchChangedEvent(KnowledgeTransaction transaction, KnowledgeTransactionResult result)
        {
            source = transaction.source;
            transactionId = transaction.transactionId;
            revision = result.revision;
            changes = result.changes;
        }
    }

    internal sealed class ValidatedObservation
    {
        public KnowledgeObservation input;
        public KnowledgeSchema schema;
        public KnowledgeFacetSchema facet;
        public KnowledgeObservationDef observationDef;
        public string domainId;
        public string subjectId;
        public string facetId;
        public string trackId;
        public float knowledge;
        public float evidenceWeight;
        public float expertise;
        public float familiarity;
    }

    public static class KnowledgeEngine
    {
        public static event Action<KnowledgeBatchChangedEvent> Changed;

        public static KnowledgeTransactionResult Submit(KnowledgeObservation observation) =>
            Submit(new KnowledgeTransaction { source = observation?.source }.Add(observation));

        public static KnowledgeTransactionResult Submit(KnowledgeTransaction transaction)
        {
            Stopwatch stopwatch = KnowledgeDiagnostics.Enabled ? Stopwatch.StartNew() : null;
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component == null) return Failed("No active game knowledge component.");
            if (transaction == null || transaction.Observations.Count == 0) return Failed("The transaction contains no observations.");
            if (transaction.Observations.Count > 256) return Failed("A transaction is limited to 256 observations.");

            if (!KnowledgeV3Runtime.PrepareTransaction(transaction, out KnowledgeTransaction prepared, out string preparationError))
                return Failed(preparationError);
            transaction = prepared;
            if (transaction.Observations.Count > KnowledgeV3Runtime.ExpandedTransactionLimit)
                return Failed("The expanded transaction exceeds the safe observation limit.");

            List<ValidatedObservation> validated = new List<ValidatedObservation>(transaction.Observations.Count);
            for (int i = 0; i < transaction.Observations.Count; i++)
            {
                if (!TryValidate(transaction.Observations[i], out ValidatedObservation item, out string error))
                    return Failed("Observation " + i + ": " + error);
                validated.Add(item);
            }

            List<KnowledgeChange> changes = new List<KnowledgeChange>();
            HashSet<string> changedDependencies = new HashSet<string>(StringComparer.Ordinal);
            HashSet<Pawn> changedPawns = new HashSet<Pawn>();
            for (int i = 0; i < validated.Count; i++) Apply(component, validated[i], changes, changedDependencies, changedPawns);
            List<string> insights = KnowledgeInsightService.EvaluateTouched(component, changedDependencies, changes);
            KnowledgeMilestoneService.EvaluateTouched(changes);
            component.RefreshDiagnosticsV2();

            foreach (Pawn pawn in changedPawns) KnowledgeProviderRegistry.Invalidate(pawn);
            if (changes.Any(change => change.scope == KnowledgeScope.Colony)) KnowledgeProviderRegistry.InvalidateAll();
            KnowledgeUiCache.Invalidate(changes);
            KnowledgeTransactionResult result = new KnowledgeTransactionResult(true, null, component.GlobalRevision, changes, insights);
            InvokeSafely(Changed, new KnowledgeBatchChangedEvent(transaction, result), "batch change");
            KnowledgeDiagnostics.RecordTransaction(stopwatch?.ElapsedTicks ?? 0L, validated.Count, insights.Count,
                changedDependencies.Sum(key => KnowledgeRegistry.RelationshipsTo(ParseDependency(key, 0), ParseDependency(key, 1), ParseDependency(key, 2)).Count));
            return result;
        }

        internal static bool ApplyInsightOutcome(GameComponent_KnowledgeFramework component, KnowledgeObservation observation,
            List<KnowledgeChange> changes)
        {
            if (!TryValidate(observation, out ValidatedObservation validated, out string error)) return false;
            HashSet<string> ignoredDependencies = new HashSet<string>(StringComparer.Ordinal);
            ApplyScope(component, validated, observation.targetColony, changes, ignoredDependencies);
            return true;
        }

        internal static void NotifyExternalChange(string source)
        {
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component == null) return;
            KnowledgeUiCache.Invalidate(Array.Empty<KnowledgeChange>());
            KnowledgeBatchChangedEvent value = new KnowledgeBatchChangedEvent(
                new KnowledgeTransaction { source = source },
                new KnowledgeTransactionResult(true, null, component.GlobalRevision));
            InvokeSafely(Changed, value, "external change");
        }

        private static bool TryValidate(KnowledgeObservation input, out ValidatedObservation output, out string error)
        {
            output = null;
            error = null;
            if (input == null) { error = "Observation is null."; return false; }
            string domainId = KnowledgeRegistry.ResolveDomainId(input.domainId);
            string subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, input.subjectId);
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            if (schema == null) { error = "Unknown domain '" + input.domainId + "'."; return false; }
            if (subjectId.NullOrEmpty() || KnowledgeRegistry.ResolveSubject(domainId, subjectId) == null)
            {
                error = "Unknown subject '" + input.subjectId + "'.";
                return false;
            }
            string facetId = input.facetId.NullOrEmpty() ? KnowledgeSchema.DefaultFacetId : input.facetId;
            KnowledgeFacetSchema facet = schema.Facet(facetId);
            if (facet == null) { error = "Unknown facet '" + facetId + "'."; return false; }
            if (!input.targetColony && input.observer == null) { error = "Personal observations require an observer pawn."; return false; }
            if (!input.targetColony && !facet.personallyKnowable) { error = "The facet cannot be personally known."; return false; }
            float[] values = { input.quality, input.novelty, input.repetition, input.environmentalDifficulty,
                input.sourceReliability, input.directKnowledge, input.directExpertise, input.directFamiliarity };
            if (values.Any(value => !KnowledgeMath.IsFinite(value) || value < 0f))
            {
                error = "Numeric values must be finite and non-negative.";
                return false;
            }
            if (values.Skip(5).Any(value => value > 100000000f))
            {
                error = "Direct knowledge, expertise, and familiarity values exceed the transaction limit.";
                return false;
            }
            if (input.quality > 100f || input.novelty > 10f || input.repetition > 10f || input.environmentalDifficulty > 100f || input.sourceReliability > 10f)
            {
                error = "Observation multipliers exceed safe limits.";
                return false;
            }
            KnowledgeObservationDef observationDef = input.observationId.NullOrEmpty() ? null : schema.Observation(input.observationId);
            if (!input.observationId.NullOrEmpty() && observationDef == null) { error = "Unknown observation type '" + input.observationId + "'."; return false; }
            if (observationDef?.facetIds != null && observationDef.facetIds.Count > 0 && !observationDef.facetIds.Contains(facetId))
            {
                error = "Observation type does not support facet '" + facetId + "'.";
                return false;
            }
            string trackId = input.expertiseTrackId.NullOrEmpty() ? KnowledgeSchema.DefaultExpertiseTrackId : input.expertiseTrackId;
            if ((input.directExpertise > 0f || observationDef?.baseExpertise > 0f) && schema.ExpertiseTrack(trackId) == null)
            {
                error = "Unknown expertise track '" + trackId + "'.";
                return false;
            }
            float difficulty = Math.Max(0.1f, input.environmentalDifficulty);
            float multiplier = input.quality * input.novelty * input.repetition * input.sourceReliability / difficulty;
            float configuredKnowledge = input.suppressConfiguredKnowledge ? 0f : observationDef?.baseKnowledge ?? (input.directKnowledge > 0f ? 0f : 1f);
            float configuredExpertise = observationDef?.baseExpertise ?? 0f;
            float configuredFamiliarity = observationDef?.baseFamiliarity ?? 0f;
            if (!input.success)
            {
                configuredKnowledge *= observationDef?.failureKnowledgeFactor ?? 0.5f;
                configuredExpertise *= observationDef?.failureExpertiseFactor ?? 1f;
            }
            if (!KnowledgeMath.IsFinite(multiplier) || !KnowledgeMath.IsFinite(configuredKnowledge * multiplier) ||
                !KnowledgeMath.IsFinite(configuredExpertise * multiplier) || !KnowledgeMath.IsFinite(configuredFamiliarity * multiplier))
            {
                error = "The observation calculation produced a non-finite result.";
                return false;
            }
            output = new ValidatedObservation
            {
                input = input,
                schema = schema,
                facet = facet,
                observationDef = observationDef,
                domainId = domainId,
                subjectId = subjectId,
                facetId = facetId,
                trackId = trackId,
                knowledge = input.directKnowledge + configuredKnowledge * multiplier,
                evidenceWeight = Math.Max(0f, input.quality * input.novelty * input.sourceReliability / difficulty),
                expertise = input.directExpertise + configuredExpertise * multiplier,
                familiarity = input.directFamiliarity + configuredFamiliarity * multiplier
            };
            return true;
        }

        private static void Apply(GameComponent_KnowledgeFramework component, ValidatedObservation value,
            List<KnowledgeChange> changes, HashSet<string> dependencies, HashSet<Pawn> changedPawns)
        {
            bool colony = value.input.targetColony;
            ApplyScope(component, value, colony, changes, dependencies);
            if (!colony && value.schema.sharingModel == KnowledgeSharingModel.Immediate && value.input.shareable &&
                (value.observationDef?.shareable ?? true) && value.facet.shareable)
                ApplyScope(component, value, true, changes, dependencies);
            if (value.input.observer != null) changedPawns.Add(value.input.observer);
        }

        private static void ApplyScope(GameComponent_KnowledgeFramework component, ValidatedObservation value, bool colony,
            List<KnowledgeChange> changes, HashSet<string> dependencies)
        {
            Pawn pawn = colony ? null : value.input.observer;
            KnowledgeFacetStateRecord facet = colony
                ? (KnowledgeFacetStateRecord)component.ColonyFacetV2(value.domainId, value.subjectId, value.facetId, true)
                : component.PersonalFacetV2(value.domainId, value.subjectId, value.facetId, pawn, true);
            KnowledgeSubjectStateRecord subject = colony
                ? (KnowledgeSubjectStateRecord)component.ColonySubjectV2(value.domainId, value.subjectId, true)
                : component.PersonalSubjectV2(value.domainId, value.subjectId, pawn, true);
            ExpertiseStateRecord expertise = !colony && value.expertise > 0f
                ? component.ExpertiseV2(value.domainId, value.trackId, pawn, true) : null;
            float oldAmount = facet.amount;
            float oldConfidence = KnowledgeMath.Confidence(facet.supportingEvidence, facet.contradictoryEvidence, value.schema.uncertaintyEnabled);
            string oldStage = subject.stageId;

            facet.amount = Math.Min(100000000f, facet.amount + value.knowledge);
            if (value.input.disposition == KnowledgeEvidenceDisposition.Supporting)
                facet.supportingEvidence = Math.Min(100000000f, facet.supportingEvidence + value.evidenceWeight);
            else if (value.input.disposition == KnowledgeEvidenceDisposition.Contradictory)
                facet.contradictoryEvidence = Math.Min(100000000f, facet.contradictoryEvidence + value.evidenceWeight);
            bool countEvidence = value.input.success || value.observationDef?.countFailureAsEvidence != false;
            if (countEvidence) facet.evidenceCount = Math.Min(100000000, facet.evidenceCount + 1);
            if (value.input.success) facet.successCount = Math.Min(100000000, facet.successCount + 1);
            else facet.failureCount = Math.Min(100000000, facet.failureCount + 1);
            IncrementBounded(facet.eventCounts, value.input.reasonId);
            AggregateEvidence(facet, value);
            RetainProvenance(facet, value);
            subject.familiarity = Math.Min(100000000f, subject.familiarity + value.familiarity);
            if (value.input.documented && value.facet.documentable)
            {
                subject.documented = true;
                subject.documentationSource = value.input.source;
            }
            if (expertise != null) expertise.amount = Math.Min(100000000f, expertise.amount + value.expertise);
            UpdateStage(component, value.schema, value.domainId, value.subjectId, pawn, colony, subject, KnowledgeV3Runtime.ContextFor(value.input));
            component.Touch(subject, facet, expertise);
            KnowledgeV3Runtime.ApplyObservation(component, value, colony);

            float newConfidence = KnowledgeMath.Confidence(facet.supportingEvidence, facet.contradictoryEvidence, value.schema.uncertaintyEnabled);
            changes.Add(new KnowledgeChange(value.domainId, value.subjectId, value.facetId, pawn,
                colony ? KnowledgeScope.Colony : KnowledgeScope.Personal, oldAmount, facet.amount,
                oldConfidence, newConfidence, oldStage, subject.stageId));
            dependencies.Add(value.domainId + "\n" + value.subjectId + "\n" + value.facetId + "\n" +
                (colony ? "C" : pawn.thingIDNumber.ToString()));
        }

        internal static void UpdateStage(GameComponent_KnowledgeFramework component, KnowledgeSchema schema, string domainId,
            string subjectId, Pawn pawn, bool colony, KnowledgeSubjectStateRecord subject,
            KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            if (schema.stages.Count == 0) return;
            float amount = 0f;
            float confidence = 0f;
            IReadOnlyList<KnowledgeFacetSchema> applicable = KnowledgeRegistry.ApplicableFacets(domainId, subjectId);
            for (int i = 0; i < applicable.Count; i++)
            {
                KnowledgeFacetStateRecord facet = colony
                    ? (KnowledgeFacetStateRecord)component.ColonyFacetV2(domainId, subjectId, applicable[i].id, false)
                    : component.PersonalFacetV2(domainId, subjectId, applicable[i].id, pawn, false);
                if (facet == null) continue;
                amount += facet.amount;
                confidence = Math.Max(confidence, KnowledgeMath.Confidence(facet.supportingEvidence, facet.contradictoryEvidence, schema.uncertaintyEnabled));
            }
            KnowledgeStageSchema current = schema.Stage(subject.stageId);
            KnowledgeStageSchema stage = schema.stages.LastOrDefault(item =>
            {
                bool legacy = amount >= item.minimumKnowledge && confidence >= item.minimumConfidence && (!item.documented || subject.documented);
                bool requirements = item.requirementGroup == null || KnowledgeRequirementService.Evaluate(item.requirementGroup, domainId, subjectId,
                    pawn, colony ? KnowledgeScope.Colony : KnowledgeScope.Personal, context, out _);
                return legacy && requirements;
            });
            if (current != null && stage != null && !stage.allowRegression && stage.order < current.order) stage = current;
            subject.stageId = stage?.id;
        }

        private static void AggregateEvidence(KnowledgeFacetStateRecord facet, ValidatedObservation value)
        {
            string key = (value.input.observationId ?? value.input.methodId ?? "observation") + "/" +
                (value.input.reasonId ?? string.Empty) + "/" + (value.input.contextId ?? string.Empty) + "/" +
                (value.input.sourceInstanceId ?? value.input.source ?? string.Empty);
            KnowledgeEvidenceAggregateRecord aggregate = facet.aggregates.FirstOrDefault(item => item.key == key);
            if (aggregate == null)
            {
                if (facet.aggregates.Count >= value.schema.evidenceAggregateLimit)
                {
                    aggregate = facet.aggregates.FirstOrDefault(item => item.key == "__overflow");
                    if (aggregate == null)
                    {
                        aggregate = new KnowledgeEvidenceAggregateRecord { key = "__overflow" };
                        facet.aggregates[0] = aggregate;
                    }
                }
                else
                {
                    aggregate = new KnowledgeEvidenceAggregateRecord { key = key };
                    facet.aggregates.Add(aggregate);
                }
            }
            aggregate.count = Math.Min(100000000, aggregate.count + 1);
            if (value.input.success) aggregate.successes = Math.Min(aggregate.count, aggregate.successes + 1);
            else aggregate.failures = Math.Min(aggregate.count, aggregate.failures + 1);
            aggregate.qualityTotal = Math.Min(100000000f, aggregate.qualityTotal + value.input.quality);
            aggregate.lastTick = Find.TickManager?.TicksGame ?? 0;
        }

        private static void RetainProvenance(KnowledgeFacetStateRecord facet, ValidatedObservation value)
        {
            if (value.schema.provenanceLimit <= 0 || value.observationDef?.retainProvenance != true && value.input.summary.NullOrEmpty()) return;
            facet.provenance.Add(new KnowledgeProvenanceRecord
            {
                source = value.input.source,
                reasonId = value.input.reasonId,
                methodId = value.input.methodId ?? value.input.observationId,
                summary = value.input.summary,
                tick = Find.TickManager?.TicksGame ?? 0,
                witnesses = (value.input.witnesses ?? Array.Empty<Pawn>()).Where(item => item != null).Take(8).ToList()
            });
            while (facet.provenance.Count > value.schema.provenanceLimit) facet.provenance.RemoveAt(0);
        }

        private static void IncrementBounded(IDictionary<string, int> counts, string key)
        {
            if (key.NullOrEmpty()) return;
            if (!counts.ContainsKey(key) && counts.Count >= 256) return;
            int old = counts.TryGetValue(key, out int value) ? value : 0;
            counts[key] = Math.Min(100000000, old + 1);
        }

        private static void InvokeSafely(Action<KnowledgeBatchChangedEvent> handlers, KnowledgeBatchChangedEvent value, string name)
        {
            if (handlers == null) return;
            foreach (Action<KnowledgeBatchChangedEvent> handler in handlers.GetInvocationList())
            {
                try { handler(value); }
                catch (Exception exception) { KnowledgeLog.ErrorOnce("event:" + handler.Method.DeclaringType?.FullName + "." + handler.Method.Name,
                    "A " + name + " subscriber failed.", exception); }
            }
        }

        private static KnowledgeTransactionResult Failed(string error) => new KnowledgeTransactionResult(false, error, GameComponent_KnowledgeFramework.Current?.GlobalRevision ?? 0);
        private static string ParseDependency(string key, int index)
        {
            string[] parts = key?.Split('\n');
            return parts != null && index >= 0 && index < parts.Length ? parts[index] : null;
        }
    }

    public static partial class KnowledgeQuery
    {
        public static KnowledgeFacetSnapshotV2 Facet(string domainId, string subjectId, string facetId = null,
            Pawn pawn = null, KnowledgeScope scope = KnowledgeScope.Personal, bool includeDerived = true,
            bool includeEvidenceDetails = true)
        {
            domainId = KnowledgeRegistry.ResolveDomainId(domainId);
            subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, subjectId);
            facetId = facetId.NullOrEmpty() ? KnowledgeSchema.DefaultFacetId : facetId;
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            if (schema != null && facetId == KnowledgeSchema.DefaultFacetId && schema.Facet(facetId) == null && schema.facets.Count > 0)
                facetId = schema.facets[0].id;
            KnowledgeFacetSchema facetSchema = schema?.Facet(facetId);
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (schema == null || facetSchema == null || component == null)
                return new KnowledgeFacetSnapshotV2(domainId, subjectId, facetId, pawn, scope, 0f, 0f, 0f, 0f, false, 0, 0, 0, 0, null);
            if (!KnowledgeRegistry.ApplicableFacets(domainId, subjectId).Any(item => item.id == facetId))
                return new KnowledgeFacetSnapshotV2(domainId, subjectId, facetId, pawn, scope, 0f, 0f, 0f, 0f, false, 0, 0, 0, 0, null);
            KnowledgeFacetStateRecord record = scope == KnowledgeScope.Colony
                ? (KnowledgeFacetStateRecord)component.ColonyFacetV2(domainId, subjectId, facetId, false)
                : component.PersonalFacetV2(domainId, subjectId, facetId, pawn, false);
            float direct = record?.amount ?? 0f;
            float directConfidence = record == null ? 0f : KnowledgeMath.Confidence(record.supportingEvidence, record.contradictoryEvidence, schema.uncertaintyEnabled);
            float derived = 0f;
            float derivedConfidence = 0f;
            if (includeDerived)
            {
                IReadOnlyList<KnowledgeRelationshipDef> relationships = KnowledgeRegistry.RelationshipsTo(domainId, subjectId, facetId);
                for (int i = 0; i < relationships.Count; i++)
                {
                    KnowledgeRelationshipDef relation = relationships[i];
                    string sourceDomainId = relation.fromDomainId.NullOrEmpty() ? domainId : relation.fromDomainId;
                    string sourceFacetId = relation.facetId.NullOrEmpty() ? facetId : relation.facetId;
                    KnowledgeFacetSnapshotV2 source = Facet(sourceDomainId, relation.fromSubjectId, sourceFacetId, pawn, scope, false, false);
                    float candidate = source.directAmount * relation.coefficient;
                    if (candidate > derived)
                    {
                        derived = candidate;
                        derivedConfidence = source.confidence * relation.confidenceCoefficient;
                    }
                }
                KnowledgeSubjectSnapshot subject = KnowledgeRegistry.ResolveSubject(domainId, subjectId);
                if (subject != null && !subject.templateSubjectId.NullOrEmpty() && subject.templateKnowledgeCoefficient > 0f)
                {
                    KnowledgeFacetSnapshotV2 template = Facet(domainId, subject.templateSubjectId, facetId, pawn, scope, false, false);
                    if (template.directAmount * subject.templateKnowledgeCoefficient > derived)
                    {
                        derived = template.directAmount * subject.templateKnowledgeCoefficient;
                        derivedConfidence = template.confidence * subject.templateConfidenceCoefficient;
                    }
                }
            }
            float amount = Math.Max(direct, derived);
            float confidence = direct >= derived ? directConfidence : derivedConfidence;
            return new KnowledgeFacetSnapshotV2(domainId, subjectId, facetId, pawn, scope, direct, derived,
                Math.Min(1f, amount / facetSchema.completenessAmount), confidence, derived > direct,
                record?.evidenceCount ?? 0, record?.successCount ?? 0, record?.failureCount ?? 0,
                record?.revision ?? 0, includeEvidenceDetails ? record?.eventCounts : null,
                includeEvidenceDetails ? record?.aggregates?.Select(item => new KnowledgeEvidenceAggregateSnapshot(item)) : null,
                includeEvidenceDetails ? record?.provenance?.Select(item => new KnowledgeProvenanceSnapshot(item)) : null);
        }

        public static KnowledgeSubjectSnapshotV2 Subject(string domainId, string subjectId, Pawn pawn = null,
            KnowledgeScope scope = KnowledgeScope.Personal)
        {
            domainId = KnowledgeRegistry.ResolveDomainId(domainId);
            subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, subjectId);
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            KnowledgeSubjectStateRecord record = scope == KnowledgeScope.Colony
                ? (KnowledgeSubjectStateRecord)component?.ColonySubjectV2(domainId, subjectId, false)
                : component?.PersonalSubjectV2(domainId, subjectId, pawn, false);
            return new KnowledgeSubjectSnapshotV2(domainId, subjectId, pawn, scope, record);
        }

        public static KnowledgeExpertiseSnapshotV2 Expertise(string domainId, Pawn pawn, string trackId = null)
        {
            domainId = KnowledgeRegistry.ResolveDomainId(domainId);
            trackId = trackId.NullOrEmpty() ? KnowledgeSchema.DefaultExpertiseTrackId : trackId;
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            KnowledgeExpertiseTrackSchema track = schema?.ExpertiseTrack(trackId);
            if (track == null && trackId == KnowledgeSchema.DefaultExpertiseTrackId) track = schema?.expertiseTracks.FirstOrDefault();
            if (track != null && trackId == KnowledgeSchema.DefaultExpertiseTrackId) trackId = track.id;
            ExpertiseStateRecord record = GameComponent_KnowledgeFramework.Current?.ExpertiseV2(domainId, trackId, pawn, false);
            float amount = record?.amount ?? 0f;
            return new KnowledgeExpertiseSnapshotV2(domainId, trackId, pawn, amount,
                track?.ranks.RankFor(amount) ?? KnowledgeRank.Novice, track?.ranks.ProgressFor(amount) ?? 0f, record?.revision ?? 0);
        }

        public static IReadOnlyList<KnowledgeFacetSnapshotV2> PersonalFacets(string domainId, Pawn pawn)
        {
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component == null) return Array.Empty<KnowledgeFacetSnapshotV2>();
            return component.PersonalFacetRecordsV2(KnowledgeRegistry.ResolveDomainId(domainId), pawn)
                .Where(item => KnowledgeRegistry.ApplicableFacets(item.domainId, item.subjectId).Any(facet => facet.id == item.facetId))
                .Select(item => Facet(item.domainId, item.subjectId, item.facetId, pawn)).ToList();
        }

        public static IReadOnlyList<KnowledgeFacetSnapshotV2> ColonyFacets(string domainId)
        {
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component == null) return Array.Empty<KnowledgeFacetSnapshotV2>();
            return component.ColonyFacetRecordsV2(KnowledgeRegistry.ResolveDomainId(domainId))
                .Where(item => KnowledgeRegistry.ApplicableFacets(item.domainId, item.subjectId).Any(facet => facet.id == item.facetId))
                .Select(item => Facet(item.domainId, item.subjectId, item.facetId, null, KnowledgeScope.Colony)).ToList();
        }

        public static IReadOnlyList<KnowledgeRelationshipSnapshot> Relationships(string domainId, string subjectId,
            string facetId = null, Pawn pawn = null, KnowledgeScope scope = KnowledgeScope.Personal)
        {
            return KnowledgeRegistry.RelationshipsTo(domainId, subjectId, facetId).Select(definition =>
                new KnowledgeRelationshipSnapshot(definition, Facet(domainId, definition.fromSubjectId,
                    definition.facetId, pawn, scope, false, false))).ToList();
        }

        public static KnowledgeComparisonSnapshot Compare(string domainId, string firstSubjectId, string secondSubjectId,
            Pawn pawn = null, KnowledgeScope scope = KnowledgeScope.Personal)
        {
            return KnowledgeComparisonService.Compare(domainId, firstSubjectId, secondSubjectId, pawn, scope);
        }
    }
}
