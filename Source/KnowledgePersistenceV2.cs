using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace KnowledgeFramework
{
    internal sealed class KnowledgeEvidenceAggregateRecord : IExposable
    {
        public string key;
        public int count;
        public int successes;
        public int failures;
        public float qualityTotal;
        public int lastTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref successes, "successes");
            Scribe_Values.Look(ref failures, "failures");
            Scribe_Values.Look(ref qualityTotal, "qualityTotal");
            Scribe_Values.Look(ref lastTick, "lastTick");
            Normalize();
        }

        public void Normalize()
        {
            count = Mathf.Clamp(count, 0, 100000000);
            successes = Mathf.Clamp(successes, 0, count);
            failures = Mathf.Clamp(failures, 0, count);
            qualityTotal = KnowledgeMath.NonNegativeFiniteOr(qualityTotal, 0f);
            lastTick = Math.Max(0, lastTick);
        }
    }

    internal sealed class KnowledgeProvenanceRecord : IExposable
    {
        public string source;
        public string reasonId;
        public string methodId;
        public string summary;
        public int tick;
        public List<Pawn> witnesses = new List<Pawn>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref source, "source");
            Scribe_Values.Look(ref reasonId, "reasonId");
            Scribe_Values.Look(ref methodId, "methodId");
            Scribe_Values.Look(ref summary, "summary");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Collections.Look(ref witnesses, "witnesses", LookMode.Reference);
            if (witnesses == null) witnesses = new List<Pawn>();
            witnesses = witnesses.Where(item => item != null).Take(8).ToList();
            tick = Math.Max(0, tick);
        }
    }

    internal abstract class KnowledgeSubjectStateRecord : IExposable
    {
        public string domainId;
        public string subjectId;
        public string stageId;
        public float familiarity;
        public bool documented;
        public string documentationSource;
        public int revision;

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref stageId, "stageId");
            Scribe_Values.Look(ref familiarity, "familiarity");
            Scribe_Values.Look(ref documented, "documented");
            Scribe_Values.Look(ref documentationSource, "documentationSource");
            Scribe_Values.Look(ref revision, "revision");
            familiarity = KnowledgeMath.NonNegativeFiniteOr(familiarity, 0f);
            revision = Math.Max(0, revision);
        }
    }

    internal sealed class PersonalSubjectStateRecord : KnowledgeSubjectStateRecord
    {
        public Pawn pawn;

        public override void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            base.ExposeData();
        }
    }

    internal sealed class ColonySubjectStateRecord : KnowledgeSubjectStateRecord
    {
    }

    internal abstract class KnowledgeFacetStateRecord : IExposable
    {
        public string domainId;
        public string subjectId;
        public string facetId;
        public float amount;
        public float supportingEvidence;
        public float contradictoryEvidence;
        public int evidenceCount;
        public int successCount;
        public int failureCount;
        public int revision;
        public Dictionary<string, int> eventCounts = new Dictionary<string, int>();
        public List<KnowledgeEvidenceAggregateRecord> aggregates = new List<KnowledgeEvidenceAggregateRecord>();
        public List<KnowledgeProvenanceRecord> provenance = new List<KnowledgeProvenanceRecord>();

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref facetId, "facetId");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref supportingEvidence, "supportingEvidence");
            Scribe_Values.Look(ref contradictoryEvidence, "contradictoryEvidence");
            Scribe_Values.Look(ref evidenceCount, "evidenceCount");
            Scribe_Values.Look(ref successCount, "successCount");
            Scribe_Values.Look(ref failureCount, "failureCount");
            Scribe_Values.Look(ref revision, "revision");
            Scribe_Collections.Look(ref eventCounts, "eventCounts", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref aggregates, "aggregates", LookMode.Deep);
            Scribe_Collections.Look(ref provenance, "provenance", LookMode.Deep);
            Normalize();
        }

        public void Normalize()
        {
            amount = KnowledgeMath.NonNegativeFiniteOr(amount, 0f);
            supportingEvidence = Math.Min(100000000f, KnowledgeMath.NonNegativeFiniteOr(supportingEvidence, 0f));
            contradictoryEvidence = Math.Min(100000000f, KnowledgeMath.NonNegativeFiniteOr(contradictoryEvidence, 0f));
            evidenceCount = Mathf.Clamp(evidenceCount, 0, 100000000);
            successCount = Mathf.Clamp(successCount, 0, evidenceCount);
            failureCount = Mathf.Clamp(failureCount, 0, evidenceCount);
            revision = Math.Max(0, revision);
            if (facetId.NullOrEmpty()) facetId = KnowledgeSchema.DefaultFacetId;
            if (eventCounts == null) eventCounts = new Dictionary<string, int>();
            foreach (string key in eventCounts.Keys.Where(key => key.NullOrEmpty() || eventCounts[key] <= 0).ToList()) eventCounts.Remove(key);
            if (aggregates == null) aggregates = new List<KnowledgeEvidenceAggregateRecord>();
            if (provenance == null) provenance = new List<KnowledgeProvenanceRecord>();
            aggregates.RemoveAll(item => item == null || item.key.NullOrEmpty());
            provenance.RemoveAll(item => item == null);
            for (int i = 0; i < aggregates.Count; i++) aggregates[i].Normalize();
        }
    }

    internal sealed class PersonalFacetStateRecord : KnowledgeFacetStateRecord
    {
        public Pawn pawn;

        public override void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            base.ExposeData();
        }
    }

    internal sealed class ColonyFacetStateRecord : KnowledgeFacetStateRecord
    {
    }

    internal sealed class ExpertiseStateRecord : IExposable
    {
        public Pawn pawn;
        public string domainId;
        public string trackId;
        public float amount;
        public int revision;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref trackId, "trackId");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref revision, "revision");
            if (trackId.NullOrEmpty()) trackId = KnowledgeSchema.DefaultExpertiseTrackId;
            amount = KnowledgeMath.NonNegativeFiniteOr(amount, 0f);
            revision = Math.Max(0, revision);
        }
    }

    internal sealed class InsightActivationRecord : IExposable
    {
        public Pawn pawn;
        public string domainId;
        public string subjectId;
        public string insightId;
        public bool colony;
        public int tick;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref insightId, "insightId");
            Scribe_Values.Look(ref colony, "colony");
            Scribe_Values.Look(ref tick, "tick");
            tick = Math.Max(0, tick);
        }
    }

    internal readonly struct SubjectRuntimeKey : IEquatable<SubjectRuntimeKey>
    {
        public readonly string domainId;
        public readonly string subjectId;
        public readonly int pawnId;

        public SubjectRuntimeKey(string domainId, string subjectId, Pawn pawn)
        {
            this.domainId = domainId;
            this.subjectId = subjectId;
            pawnId = pawn?.thingIDNumber ?? 0;
        }

        public bool Equals(SubjectRuntimeKey other) => pawnId == other.pawnId && domainId == other.domainId && subjectId == other.subjectId;
        public override bool Equals(object obj) => obj is SubjectRuntimeKey other && Equals(other);
        public override int GetHashCode() => ((domainId?.GetHashCode() ?? 0) * 397 ^ (subjectId?.GetHashCode() ?? 0)) * 397 ^ pawnId;
    }

    internal readonly struct FacetRuntimeKey : IEquatable<FacetRuntimeKey>
    {
        public readonly SubjectRuntimeKey subject;
        public readonly string facetId;

        public FacetRuntimeKey(string domainId, string subjectId, string facetId, Pawn pawn)
        {
            subject = new SubjectRuntimeKey(domainId, subjectId, pawn);
            this.facetId = facetId;
        }

        public bool Equals(FacetRuntimeKey other) => subject.Equals(other.subject) && facetId == other.facetId;
        public override bool Equals(object obj) => obj is FacetRuntimeKey other && Equals(other);
        public override int GetHashCode() => subject.GetHashCode() * 397 ^ (facetId?.GetHashCode() ?? 0);
    }

    internal readonly struct ExpertiseRuntimeKey : IEquatable<ExpertiseRuntimeKey>
    {
        public readonly string domainId;
        public readonly string trackId;
        public readonly int pawnId;

        public ExpertiseRuntimeKey(string domainId, string trackId, Pawn pawn)
        {
            this.domainId = domainId;
            this.trackId = trackId;
            pawnId = pawn?.thingIDNumber ?? 0;
        }

        public bool Equals(ExpertiseRuntimeKey other) => pawnId == other.pawnId && domainId == other.domainId && trackId == other.trackId;
        public override bool Equals(object obj) => obj is ExpertiseRuntimeKey other && Equals(other);
        public override int GetHashCode() => ((domainId?.GetHashCode() ?? 0) * 397 ^ (trackId?.GetHashCode() ?? 0)) * 397 ^ pawnId;
    }

    public sealed partial class GameComponent_KnowledgeFramework
    {
        internal const int CurrentSchemaVersion = 3;
        private int knowledgeFrameworkSchemaVersion;
        private int globalRevision;
        private List<PersonalSubjectStateRecord> personalSubjectsV2 = new List<PersonalSubjectStateRecord>();
        private List<ColonySubjectStateRecord> colonySubjectsV2 = new List<ColonySubjectStateRecord>();
        private List<PersonalFacetStateRecord> personalFacetsV2 = new List<PersonalFacetStateRecord>();
        private List<ColonyFacetStateRecord> colonyFacetsV2 = new List<ColonyFacetStateRecord>();
        private List<ExpertiseStateRecord> expertiseV2 = new List<ExpertiseStateRecord>();
        private List<InsightActivationRecord> insightsV2 = new List<InsightActivationRecord>();
        private List<string> migratedLegacyKeys = new List<string>();

        private Dictionary<SubjectRuntimeKey, PersonalSubjectStateRecord> personalSubjectIndex;
        private Dictionary<SubjectRuntimeKey, ColonySubjectStateRecord> colonySubjectIndex;
        private Dictionary<FacetRuntimeKey, PersonalFacetStateRecord> personalFacetIndex;
        private Dictionary<FacetRuntimeKey, ColonyFacetStateRecord> colonyFacetIndex;
        private Dictionary<ExpertiseRuntimeKey, ExpertiseStateRecord> expertiseV2Index;
        private HashSet<string> insightIndex;
        private HashSet<string> migrationIndex;

        internal int GlobalRevision => globalRevision;

        private void InitializeV2()
        {
            personalSubjectIndex = new Dictionary<SubjectRuntimeKey, PersonalSubjectStateRecord>();
            colonySubjectIndex = new Dictionary<SubjectRuntimeKey, ColonySubjectStateRecord>();
            personalFacetIndex = new Dictionary<FacetRuntimeKey, PersonalFacetStateRecord>();
            colonyFacetIndex = new Dictionary<FacetRuntimeKey, ColonyFacetStateRecord>();
            expertiseV2Index = new Dictionary<ExpertiseRuntimeKey, ExpertiseStateRecord>();
            insightIndex = new HashSet<string>(StringComparer.Ordinal);
            migrationIndex = new HashSet<string>(StringComparer.Ordinal);
        }

        private void ExposeV2Data()
        {
            Scribe_Values.Look(ref knowledgeFrameworkSchemaVersion, "knowledgeFrameworkSchemaVersion");
            Scribe_Values.Look(ref globalRevision, "knowledgeFrameworkRevision");
            Scribe_Collections.Look(ref personalSubjectsV2, "knowledgeFrameworkV2PersonalSubjects", LookMode.Deep);
            Scribe_Collections.Look(ref colonySubjectsV2, "knowledgeFrameworkV2ColonySubjects", LookMode.Deep);
            Scribe_Collections.Look(ref personalFacetsV2, "knowledgeFrameworkV2PersonalFacets", LookMode.Deep);
            Scribe_Collections.Look(ref colonyFacetsV2, "knowledgeFrameworkV2ColonyFacets", LookMode.Deep);
            Scribe_Collections.Look(ref expertiseV2, "knowledgeFrameworkV2Expertise", LookMode.Deep);
            Scribe_Collections.Look(ref insightsV2, "knowledgeFrameworkV2Insights", LookMode.Deep);
            Scribe_Collections.Look(ref migratedLegacyKeys, "knowledgeFrameworkMigratedLegacyKeys", LookMode.Value);
        }

        private void RebuildV2Indexes()
        {
            InitializeV2();
            NormalizeV2Aliases();
            personalSubjectsV2 = personalSubjectsV2 ?? new List<PersonalSubjectStateRecord>();
            colonySubjectsV2 = colonySubjectsV2 ?? new List<ColonySubjectStateRecord>();
            personalFacetsV2 = personalFacetsV2 ?? new List<PersonalFacetStateRecord>();
            colonyFacetsV2 = colonyFacetsV2 ?? new List<ColonyFacetStateRecord>();
            expertiseV2 = expertiseV2 ?? new List<ExpertiseStateRecord>();
            insightsV2 = insightsV2 ?? new List<InsightActivationRecord>();
            migratedLegacyKeys = migratedLegacyKeys ?? new List<string>();

            personalSubjectsV2 = MergePersonalSubjects(personalSubjectsV2);
            colonySubjectsV2 = MergeColonySubjects(colonySubjectsV2);
            personalFacetsV2 = MergePersonalFacets(personalFacetsV2);
            colonyFacetsV2 = MergeColonyFacets(colonyFacetsV2);
            expertiseV2 = MergeExpertise(expertiseV2);
            insightsV2.RemoveAll(item => item == null || item.domainId.NullOrEmpty() || item.subjectId.NullOrEmpty() || item.insightId.NullOrEmpty());
            foreach (InsightActivationRecord item in insightsV2) insightIndex.Add(InsightKey(item.domainId, item.subjectId, item.insightId, item.pawn, item.colony));
            foreach (string key in migratedLegacyKeys.Where(key => !key.NullOrEmpty())) migrationIndex.Add(key);
            knowledgeFrameworkSchemaVersion = CurrentSchemaVersion;
            globalRevision = Math.Max(0, globalRevision) + 1;
            KnowledgeDiagnostics.UpdateRecordCounts(personalFacetsV2.Count, colonyFacetsV2.Count, expertiseV2.Count, OrphanCount());
        }

        internal void MigrateAliasesV2()
        {
            NormalizeV2Aliases();
            RebuildV2Indexes();
            KnowledgeUiCache.Reset();
        }

        private void NormalizeV2Aliases()
        {
            foreach (PersonalSubjectStateRecord item in personalSubjectsV2 ?? Enumerable.Empty<PersonalSubjectStateRecord>())
                NormalizeSubjectIdentity(item);
            foreach (ColonySubjectStateRecord item in colonySubjectsV2 ?? Enumerable.Empty<ColonySubjectStateRecord>())
                NormalizeSubjectIdentity(item);
            foreach (PersonalFacetStateRecord item in personalFacetsV2 ?? Enumerable.Empty<PersonalFacetStateRecord>())
                NormalizeFacetIdentity(item);
            foreach (ColonyFacetStateRecord item in colonyFacetsV2 ?? Enumerable.Empty<ColonyFacetStateRecord>())
                NormalizeFacetIdentity(item);
            foreach (ExpertiseStateRecord item in expertiseV2 ?? Enumerable.Empty<ExpertiseStateRecord>())
                if (item != null) item.domainId = KnowledgeRegistry.ResolveDomainId(item.domainId);
            foreach (InsightActivationRecord item in insightsV2 ?? Enumerable.Empty<InsightActivationRecord>())
            {
                if (item == null) continue;
                item.domainId = KnowledgeRegistry.ResolveDomainId(item.domainId);
                item.subjectId = KnowledgeRegistry.ResolveSubjectId(item.domainId, item.subjectId);
            }
        }

        private static void NormalizeSubjectIdentity(KnowledgeSubjectStateRecord item)
        {
            if (item == null) return;
            item.domainId = KnowledgeRegistry.ResolveDomainId(item.domainId);
            item.subjectId = KnowledgeRegistry.ResolveSubjectId(item.domainId, item.subjectId);
        }

        private static void NormalizeFacetIdentity(KnowledgeFacetStateRecord item)
        {
            if (item == null) return;
            item.domainId = KnowledgeRegistry.ResolveDomainId(item.domainId);
            item.subjectId = KnowledgeRegistry.ResolveSubjectId(item.domainId, item.subjectId);
        }

        internal PersonalSubjectStateRecord PersonalSubjectV2(string domainId, string subjectId, Pawn pawn, bool create)
        {
            SubjectRuntimeKey key = new SubjectRuntimeKey(domainId, subjectId, pawn);
            if (personalSubjectIndex.TryGetValue(key, out PersonalSubjectStateRecord value) || !create || pawn == null) return value;
            value = new PersonalSubjectStateRecord { domainId = domainId, subjectId = subjectId, pawn = pawn };
            personalSubjectsV2.Add(value);
            personalSubjectIndex.Add(key, value);
            return value;
        }

        internal ColonySubjectStateRecord ColonySubjectV2(string domainId, string subjectId, bool create)
        {
            SubjectRuntimeKey key = new SubjectRuntimeKey(domainId, subjectId, null);
            if (colonySubjectIndex.TryGetValue(key, out ColonySubjectStateRecord value) || !create) return value;
            value = new ColonySubjectStateRecord { domainId = domainId, subjectId = subjectId };
            colonySubjectsV2.Add(value);
            colonySubjectIndex.Add(key, value);
            return value;
        }

        internal PersonalFacetStateRecord PersonalFacetV2(string domainId, string subjectId, string facetId, Pawn pawn, bool create)
        {
            FacetRuntimeKey key = new FacetRuntimeKey(domainId, subjectId, facetId, pawn);
            if (personalFacetIndex.TryGetValue(key, out PersonalFacetStateRecord value) || !create || pawn == null) return value;
            value = new PersonalFacetStateRecord { domainId = domainId, subjectId = subjectId, facetId = facetId, pawn = pawn };
            personalFacetsV2.Add(value);
            personalFacetIndex.Add(key, value);
            return value;
        }

        internal ColonyFacetStateRecord ColonyFacetV2(string domainId, string subjectId, string facetId, bool create)
        {
            FacetRuntimeKey key = new FacetRuntimeKey(domainId, subjectId, facetId, null);
            if (colonyFacetIndex.TryGetValue(key, out ColonyFacetStateRecord value) || !create) return value;
            value = new ColonyFacetStateRecord { domainId = domainId, subjectId = subjectId, facetId = facetId };
            colonyFacetsV2.Add(value);
            colonyFacetIndex.Add(key, value);
            return value;
        }

        internal ExpertiseStateRecord ExpertiseV2(string domainId, string trackId, Pawn pawn, bool create)
        {
            ExpertiseRuntimeKey key = new ExpertiseRuntimeKey(domainId, trackId, pawn);
            if (expertiseV2Index.TryGetValue(key, out ExpertiseStateRecord value) || !create || pawn == null) return value;
            value = new ExpertiseStateRecord { domainId = domainId, trackId = trackId, pawn = pawn };
            expertiseV2.Add(value);
            expertiseV2Index.Add(key, value);
            return value;
        }

        internal IEnumerable<PersonalFacetStateRecord> PersonalFacetRecordsV2(string domainId, Pawn pawn = null) =>
            personalFacetsV2.Where(item => item != null && item.domainId == domainId && (pawn == null || item.pawn == pawn));
        internal IEnumerable<ColonyFacetStateRecord> ColonyFacetRecordsV2(string domainId) => colonyFacetsV2.Where(item => item != null && item.domainId == domainId);
        internal IEnumerable<PersonalSubjectStateRecord> PersonalSubjectRecordsV2(string domainId, Pawn pawn = null) =>
            personalSubjectsV2.Where(item => item != null && item.domainId == domainId && (pawn == null || item.pawn == pawn));
        internal IEnumerable<ColonySubjectStateRecord> ColonySubjectRecordsV2(string domainId) => colonySubjectsV2.Where(item => item != null && item.domainId == domainId);
        internal IEnumerable<ExpertiseStateRecord> ExpertiseRecordsV2(string domainId, Pawn pawn = null) =>
            expertiseV2.Where(item => item != null && item.domainId == domainId && (pawn == null || item.pawn == pawn));

        internal bool InsightActivated(string domainId, string subjectId, string insightId, Pawn pawn, bool colony) =>
            insightIndex.Contains(InsightKey(domainId, subjectId, insightId, pawn, colony));

        internal void ActivateInsight(string domainId, string subjectId, string insightId, Pawn pawn, bool colony)
        {
            string key = InsightKey(domainId, subjectId, insightId, pawn, colony);
            if (!insightIndex.Add(key)) return;
            insightsV2.Add(new InsightActivationRecord { domainId = domainId, subjectId = subjectId, insightId = insightId, pawn = pawn,
                colony = colony, tick = Find.TickManager?.TicksGame ?? 0 });
        }

        internal int Touch(KnowledgeSubjectStateRecord subject, KnowledgeFacetStateRecord facet, ExpertiseStateRecord expertise = null)
        {
            globalRevision++;
            if (subject != null) subject.revision = globalRevision;
            if (facet != null) facet.revision = globalRevision;
            if (expertise != null) expertise.revision = globalRevision;
            return globalRevision;
        }

        internal int OrphanCount()
        {
            int count = 0;
            count += personalFacetsV2.Count(item => item == null || item.pawn == null || KnowledgeRegistry.Schema(item.domainId) == null || KnowledgeRegistry.ResolveSubject(item.domainId, item.subjectId) == null);
            count += colonyFacetsV2.Count(item => item == null || KnowledgeRegistry.Schema(item.domainId) == null || KnowledgeRegistry.ResolveSubject(item.domainId, item.subjectId) == null);
            count += personalSubjectsV2.Count(item => item == null || item.pawn == null || KnowledgeRegistry.Schema(item.domainId) == null || KnowledgeRegistry.ResolveSubject(item.domainId, item.subjectId) == null);
            count += colonySubjectsV2.Count(item => item == null || KnowledgeRegistry.Schema(item.domainId) == null || KnowledgeRegistry.ResolveSubject(item.domainId, item.subjectId) == null);
            count += expertiseV2.Count(item => item == null || item.pawn == null || KnowledgeRegistry.Schema(item.domainId) == null);
            count += pawnKnowledge.Count(item => item == null || item.pawn == null || KnowledgeRegistry.Schema(item.domainId) == null || KnowledgeRegistry.ResolveSubject(item.domainId, item.subjectId) == null);
            count += colonyKnowledge.Count(item => item == null || KnowledgeRegistry.Schema(item.domainId) == null || KnowledgeRegistry.ResolveSubject(item.domainId, item.subjectId) == null);
            count += V3OrphanCount;
            return count;
        }

        internal long ApproximatePersistentBytes() =>
            (personalSubjectsV2.Count + colonySubjectsV2.Count) * 128L +
            (personalFacetsV2.Count + colonyFacetsV2.Count) * 256L + expertiseV2.Count * 80L + insightsV2.Count * 96L +
            personalFacetsV2.Sum(item => (long)(item.eventCounts.Count * 48 + item.aggregates.Count * 80 + item.provenance.Count * 96)) +
            colonyFacetsV2.Sum(item => (long)(item.eventCounts.Count * 48 + item.aggregates.Count * 80 + item.provenance.Count * 96)) +
            ApproximateV3PersistentBytes();

        internal void RefreshDiagnosticsV2() => KnowledgeDiagnostics.UpdateRecordCounts(
            personalFacetsV2.Count, colonyFacetsV2.Count, expertiseV2.Count, OrphanCount());

        internal void ImportMinimumV2(string domainId, string subjectId, Pawn pawn, float personalAmount,
            float colonyAmount, float expertiseAmount, IDictionary<string, int> counts)
        {
            domainId = KnowledgeRegistry.ResolveDomainId(domainId);
            subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, subjectId);
            string compatibilityFacetId = KnowledgeRegistry.Schema(domainId)?.Facet(KnowledgeSchema.DefaultFacetId)?.id ??
                KnowledgeRegistry.Schema(domainId)?.facets.FirstOrDefault()?.id ?? KnowledgeSchema.DefaultFacetId;
            if (pawn != null)
            {
                PersonalSubjectStateRecord personalSubject = PersonalSubjectV2(domainId, subjectId, pawn, true);
                PersonalFacetStateRecord personal = PersonalFacetV2(domainId, subjectId, compatibilityFacetId, pawn, true);
                personal.amount = Math.Max(personal.amount, KnowledgeMath.NonNegativeFiniteOr(personalAmount, 0f));
                personal.supportingEvidence = Math.Max(personal.supportingEvidence, personal.amount);
                MergeMinimumCounts(personal.eventCounts, counts);
                string trackId = KnowledgeRegistry.Schema(domainId)?.ExpertiseTrack(KnowledgeSchema.DefaultExpertiseTrackId)?.id ??
                    KnowledgeRegistry.Schema(domainId)?.expertiseTracks.FirstOrDefault()?.id;
                ExpertiseStateRecord expertise = trackId.NullOrEmpty() ? null : ExpertiseV2(domainId, trackId, pawn, false);
                if (!trackId.NullOrEmpty())
                {
                    expertise = ExpertiseV2(domainId, trackId, pawn, true);
                    expertise.amount = Math.Max(expertise.amount, KnowledgeMath.NonNegativeFiniteOr(expertiseAmount, 0f));
                }
                Touch(personalSubject, personal, expertise);
            }
            ColonySubjectStateRecord colonySubject = ColonySubjectV2(domainId, subjectId, true);
            ColonyFacetStateRecord colony = ColonyFacetV2(domainId, subjectId, compatibilityFacetId, true);
            colony.amount = Math.Max(colony.amount, KnowledgeMath.NonNegativeFiniteOr(colonyAmount, 0f));
            colony.supportingEvidence = Math.Max(colony.supportingEvidence, colony.amount);
            MergeMinimumCounts(colony.eventCounts, counts);
            Touch(colonySubject, colony);
            RefreshDiagnosticsV2();
        }

        internal void ResetV2(string domainId, string subjectId, Pawn pawn, bool colony, bool expertise)
        {
            if (expertise)
            {
                string compatibilityTrackId = KnowledgeRegistry.Schema(domainId)?.ExpertiseTrack(KnowledgeSchema.DefaultExpertiseTrackId)?.id ??
                    KnowledgeRegistry.Schema(domainId)?.expertiseTracks.FirstOrDefault()?.id ?? KnowledgeSchema.DefaultExpertiseTrackId;
                ExpertiseStateRecord value = ExpertiseV2(domainId, compatibilityTrackId, pawn, false);
                if (value != null) { value.amount = 0f; Touch(null, null, value); }
                RefreshDiagnosticsV2();
                return;
            }
            string compatibilityFacetId = KnowledgeRegistry.Schema(domainId)?.Facet(KnowledgeSchema.DefaultFacetId)?.id ??
                KnowledgeRegistry.Schema(domainId)?.facets.FirstOrDefault()?.id ?? KnowledgeSchema.DefaultFacetId;
            KnowledgeFacetStateRecord facet = colony
                ? (KnowledgeFacetStateRecord)ColonyFacetV2(domainId, subjectId, compatibilityFacetId, false)
                : PersonalFacetV2(domainId, subjectId, compatibilityFacetId, pawn, false);
            KnowledgeSubjectStateRecord subject = colony
                ? (KnowledgeSubjectStateRecord)ColonySubjectV2(domainId, subjectId, false)
                : PersonalSubjectV2(domainId, subjectId, pawn, false);
            if (facet != null)
            {
                facet.amount = 0f;
                facet.supportingEvidence = 0f;
                facet.contradictoryEvidence = 0f;
                facet.evidenceCount = 0;
                facet.successCount = 0;
                facet.failureCount = 0;
                facet.eventCounts.Clear();
                facet.aggregates.Clear();
                facet.provenance.Clear();
                Touch(subject, facet);
            }
            RefreshDiagnosticsV2();
        }

        internal void RemoveDomainDataV2(string domainId)
        {
            string canonicalDomain = KnowledgeRegistry.ResolveDomainId(domainId) ?? domainId;
            personalSubjectsV2.RemoveAll(item => item != null && KnowledgeRegistry.ResolveDomainId(item.domainId) == canonicalDomain);
            colonySubjectsV2.RemoveAll(item => item != null && KnowledgeRegistry.ResolveDomainId(item.domainId) == canonicalDomain);
            personalFacetsV2.RemoveAll(item => item != null && KnowledgeRegistry.ResolveDomainId(item.domainId) == canonicalDomain);
            colonyFacetsV2.RemoveAll(item => item != null && KnowledgeRegistry.ResolveDomainId(item.domainId) == canonicalDomain);
            expertiseV2.RemoveAll(item => item != null && KnowledgeRegistry.ResolveDomainId(item.domainId) == canonicalDomain);
            insightsV2.RemoveAll(item => item != null && KnowledgeRegistry.ResolveDomainId(item.domainId) == canonicalDomain);
            RebuildV2Indexes();
            RemoveDomainDataV3(domainId);
        }

        internal int ForgetPawnDataV2(Pawn pawn, string domainId)
        {
            if (pawn == null) return 0;
            int changed = 0;
            foreach (PersonalFacetStateRecord facet in personalFacetsV2.Where(item => item?.pawn == pawn &&
                (domainId.NullOrEmpty() || item.domainId == domainId)).ToList())
            {
                if (KnowledgeRegistry.Schema(facet.domainId)?.Facet(facet.facetId)?.forgettable != true) continue;
                facet.amount = 0f;
                facet.supportingEvidence = 0f;
                facet.contradictoryEvidence = 0f;
                facet.evidenceCount = 0;
                facet.successCount = 0;
                facet.failureCount = 0;
                facet.eventCounts.Clear();
                facet.aggregates.Clear();
                facet.provenance.Clear();
                PersonalSubjectStateRecord subject = PersonalSubjectV2(facet.domainId, facet.subjectId, pawn, false);
                Touch(subject, facet);
                changed++;
            }
            RefreshDiagnosticsV2();
            return changed;
        }

        private void MigrateLegacyV1()
        {
            if (personalFacetIndex == null) RebuildV2Indexes();
            foreach (IGrouping<string, PawnKnowledgeSaveRecord> group in pawnKnowledge.Where(item => item != null && item.pawn != null)
                .GroupBy(item => "P/" + item.domainId + "/" + item.subjectId + "/" + item.pawn.thingIDNumber).ToList())
            {
                PawnKnowledgeSaveRecord first = group.First();
                string key = group.Key;
                string domainId = KnowledgeRegistry.ResolveDomainId(first.domainId);
                if (migrationIndex.Contains(key) || KnowledgeRegistry.Schema(domainId) == null) continue;
                ImportLegacyFacet(domainId, first.subjectId, first.pawn, group.Max(item => KnowledgeMath.NonNegativeFiniteOr(item.experience, 0f)),
                    MergeLegacyCounts(group.Select(item => item.eventCounts)), false);
                MarkMigrated(key);
                foreach (PawnKnowledgeSaveRecord old in group) pawnKnowledge.Remove(old);
            }
            foreach (IGrouping<string, ColonyKnowledgeSaveRecord> group in colonyKnowledge.Where(item => item != null)
                .GroupBy(item => "C/" + item.domainId + "/" + item.subjectId).ToList())
            {
                ColonyKnowledgeSaveRecord first = group.First();
                string key = group.Key;
                string domainId = KnowledgeRegistry.ResolveDomainId(first.domainId);
                if (migrationIndex.Contains(key) || KnowledgeRegistry.Schema(domainId) == null) continue;
                ImportLegacyFacet(domainId, first.subjectId, null, group.Max(item => KnowledgeMath.NonNegativeFiniteOr(item.experience, 0f)),
                    MergeLegacyCounts(group.Select(item => item.eventCounts)), true);
                MarkMigrated(key);
                foreach (ColonyKnowledgeSaveRecord old in group) colonyKnowledge.Remove(old);
            }
            foreach (IGrouping<string, PawnExpertiseSaveRecord> group in pawnExpertise.Where(item => item != null && item.pawn != null)
                .GroupBy(item => "E/" + item.domainId + "/" + item.pawn.thingIDNumber).ToList())
            {
                PawnExpertiseSaveRecord first = group.First();
                string key = group.Key;
                string domainId = KnowledgeRegistry.ResolveDomainId(first.domainId);
                if (migrationIndex.Contains(key) || KnowledgeRegistry.Schema(domainId) == null) continue;
                ExpertiseStateRecord target = ExpertiseV2(domainId, KnowledgeSchema.DefaultExpertiseTrackId, first.pawn, true);
                target.amount = Math.Max(target.amount, group.Max(item => KnowledgeMath.NonNegativeFiniteOr(item.experience, 0f)));
                Touch(null, null, target);
                MarkMigrated(key);
                foreach (PawnExpertiseSaveRecord old in group) pawnExpertise.Remove(old);
            }
            knowledgeFrameworkSchemaVersion = CurrentSchemaVersion;
            RefreshDiagnosticsV2();
        }

        private void ImportLegacyFacet(string domainId, string subjectId, Pawn pawn, float amount, IDictionary<string, int> counts, bool colony)
        {
            domainId = KnowledgeRegistry.ResolveDomainId(domainId);
            subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, subjectId);
            string compatibilityFacetId = KnowledgeRegistry.Schema(domainId)?.Facet(KnowledgeSchema.DefaultFacetId)?.id ??
                KnowledgeRegistry.Schema(domainId)?.facets.FirstOrDefault()?.id ?? KnowledgeSchema.DefaultFacetId;
            KnowledgeFacetStateRecord target = colony
                ? (KnowledgeFacetStateRecord)ColonyFacetV2(domainId, subjectId, compatibilityFacetId, true)
                : PersonalFacetV2(domainId, subjectId, compatibilityFacetId, pawn, true);
            KnowledgeSubjectStateRecord subject = colony
                ? (KnowledgeSubjectStateRecord)ColonySubjectV2(domainId, subjectId, true)
                : PersonalSubjectV2(domainId, subjectId, pawn, true);
            amount = KnowledgeMath.NonNegativeFiniteOr(amount, 0f);
            target.amount = Math.Max(target.amount, amount);
            target.supportingEvidence = Math.Max(target.supportingEvidence, amount);
            if (counts != null)
                foreach (KeyValuePair<string, int> pair in counts)
                    if (!pair.Key.NullOrEmpty() && pair.Value > 0)
                        target.eventCounts[pair.Key] = Math.Max(target.eventCounts.TryGetValue(pair.Key, out int old) ? old : 0, pair.Value);
            Touch(subject, target);
        }

        private void MarkMigrated(string key)
        {
            if (!migrationIndex.Add(key)) return;
            const int limit = 8192;
            if (migratedLegacyKeys.Count >= limit)
            {
                string removed = migratedLegacyKeys[0];
                migratedLegacyKeys.RemoveAt(0);
                migrationIndex.Remove(removed);
            }
            migratedLegacyKeys.Add(key);
        }

        private List<PersonalSubjectStateRecord> MergePersonalSubjects(IEnumerable<PersonalSubjectStateRecord> source)
        {
            List<PersonalSubjectStateRecord> orphaned = source.Where(item => item != null &&
                (item.pawn == null || item.domainId.NullOrEmpty() || item.subjectId.NullOrEmpty())).ToList();
            foreach (PersonalSubjectStateRecord item in source.Where(item => item != null && item.pawn != null && !item.domainId.NullOrEmpty() && !item.subjectId.NullOrEmpty()))
            {
                SubjectRuntimeKey key = new SubjectRuntimeKey(item.domainId, item.subjectId, item.pawn);
                if (personalSubjectIndex.TryGetValue(key, out PersonalSubjectStateRecord old)) MergeSubject(old, item);
                else personalSubjectIndex.Add(key, item);
            }
            return personalSubjectIndex.Values.OrderBy(item => item.domainId).ThenBy(item => item.subjectId).ThenBy(item => item.pawn.thingIDNumber)
                .Concat(orphaned).ToList();
        }

        private List<ColonySubjectStateRecord> MergeColonySubjects(IEnumerable<ColonySubjectStateRecord> source)
        {
            List<ColonySubjectStateRecord> orphaned = source.Where(item => item != null &&
                (item.domainId.NullOrEmpty() || item.subjectId.NullOrEmpty())).ToList();
            foreach (ColonySubjectStateRecord item in source.Where(item => item != null && !item.domainId.NullOrEmpty() && !item.subjectId.NullOrEmpty()))
            {
                SubjectRuntimeKey key = new SubjectRuntimeKey(item.domainId, item.subjectId, null);
                if (colonySubjectIndex.TryGetValue(key, out ColonySubjectStateRecord old)) MergeSubject(old, item);
                else colonySubjectIndex.Add(key, item);
            }
            return colonySubjectIndex.Values.OrderBy(item => item.domainId).ThenBy(item => item.subjectId).Concat(orphaned).ToList();
        }

        private List<PersonalFacetStateRecord> MergePersonalFacets(IEnumerable<PersonalFacetStateRecord> source)
        {
            List<PersonalFacetStateRecord> orphaned = source.Where(item => item != null &&
                (item.pawn == null || item.domainId.NullOrEmpty() || item.subjectId.NullOrEmpty())).ToList();
            foreach (PersonalFacetStateRecord item in orphaned) { item.Normalize(); BoundFacet(item); }
            foreach (PersonalFacetStateRecord item in source.Where(item => item != null && item.pawn != null && !item.domainId.NullOrEmpty() && !item.subjectId.NullOrEmpty()))
            {
                item.Normalize();
                FacetRuntimeKey key = new FacetRuntimeKey(item.domainId, item.subjectId, item.facetId, item.pawn);
                if (personalFacetIndex.TryGetValue(key, out PersonalFacetStateRecord old)) MergeFacet(old, item);
                else personalFacetIndex.Add(key, item);
            }
            foreach (PersonalFacetStateRecord item in personalFacetIndex.Values) BoundFacet(item);
            return personalFacetIndex.Values.OrderBy(item => item.domainId).ThenBy(item => item.subjectId).ThenBy(item => item.facetId).ThenBy(item => item.pawn.thingIDNumber)
                .Concat(orphaned).ToList();
        }

        private List<ColonyFacetStateRecord> MergeColonyFacets(IEnumerable<ColonyFacetStateRecord> source)
        {
            List<ColonyFacetStateRecord> orphaned = source.Where(item => item != null &&
                (item.domainId.NullOrEmpty() || item.subjectId.NullOrEmpty())).ToList();
            foreach (ColonyFacetStateRecord item in orphaned) { item.Normalize(); BoundFacet(item); }
            foreach (ColonyFacetStateRecord item in source.Where(item => item != null && !item.domainId.NullOrEmpty() && !item.subjectId.NullOrEmpty()))
            {
                item.Normalize();
                FacetRuntimeKey key = new FacetRuntimeKey(item.domainId, item.subjectId, item.facetId, null);
                if (colonyFacetIndex.TryGetValue(key, out ColonyFacetStateRecord old)) MergeFacet(old, item);
                else colonyFacetIndex.Add(key, item);
            }
            foreach (ColonyFacetStateRecord item in colonyFacetIndex.Values) BoundFacet(item);
            return colonyFacetIndex.Values.OrderBy(item => item.domainId).ThenBy(item => item.subjectId).ThenBy(item => item.facetId).Concat(orphaned).ToList();
        }

        private List<ExpertiseStateRecord> MergeExpertise(IEnumerable<ExpertiseStateRecord> source)
        {
            List<ExpertiseStateRecord> orphaned = source.Where(item => item != null &&
                (item.pawn == null || item.domainId.NullOrEmpty())).ToList();
            foreach (ExpertiseStateRecord item in orphaned)
            {
                item.amount = KnowledgeMath.NonNegativeFiniteOr(item.amount, 0f);
                if (item.trackId.NullOrEmpty()) item.trackId = KnowledgeSchema.DefaultExpertiseTrackId;
            }
            foreach (ExpertiseStateRecord item in source.Where(item => item != null && item.pawn != null && !item.domainId.NullOrEmpty()))
            {
                if (item.trackId.NullOrEmpty()) item.trackId = KnowledgeSchema.DefaultExpertiseTrackId;
                item.amount = KnowledgeMath.NonNegativeFiniteOr(item.amount, 0f);
                ExpertiseRuntimeKey key = new ExpertiseRuntimeKey(item.domainId, item.trackId, item.pawn);
                if (expertiseV2Index.TryGetValue(key, out ExpertiseStateRecord old)) old.amount = Math.Max(old.amount, item.amount);
                else expertiseV2Index.Add(key, item);
            }
            return expertiseV2Index.Values.OrderBy(item => item.domainId).ThenBy(item => item.trackId).ThenBy(item => item.pawn.thingIDNumber)
                .Concat(orphaned).ToList();
        }

        private static void MergeSubject(KnowledgeSubjectStateRecord target, KnowledgeSubjectStateRecord source)
        {
            target.familiarity = Math.Max(target.familiarity, source.familiarity);
            target.documented |= source.documented;
            if (target.stageId.NullOrEmpty() || source.revision > target.revision) target.stageId = source.stageId;
            if (target.documentationSource.NullOrEmpty()) target.documentationSource = source.documentationSource;
            target.revision = Math.Max(target.revision, source.revision);
        }

        private static void MergeFacet(KnowledgeFacetStateRecord target, KnowledgeFacetStateRecord source)
        {
            target.amount = Math.Max(target.amount, source.amount);
            target.supportingEvidence = Math.Max(target.supportingEvidence, source.supportingEvidence);
            target.contradictoryEvidence = Math.Max(target.contradictoryEvidence, source.contradictoryEvidence);
            target.evidenceCount = Math.Max(target.evidenceCount, source.evidenceCount);
            target.successCount = Math.Max(target.successCount, source.successCount);
            target.failureCount = Math.Max(target.failureCount, source.failureCount);
            target.revision = Math.Max(target.revision, source.revision);
            foreach (KeyValuePair<string, int> pair in source.eventCounts)
                target.eventCounts[pair.Key] = Math.Max(target.eventCounts.TryGetValue(pair.Key, out int old) ? old : 0, pair.Value);
            foreach (KnowledgeEvidenceAggregateRecord aggregate in source.aggregates)
            {
                KnowledgeEvidenceAggregateRecord existing = target.aggregates.FirstOrDefault(item => item.key == aggregate.key);
                if (existing == null) target.aggregates.Add(aggregate);
                else
                {
                    existing.count = Math.Max(existing.count, aggregate.count);
                    existing.successes = Math.Max(existing.successes, aggregate.successes);
                    existing.failures = Math.Max(existing.failures, aggregate.failures);
                    existing.qualityTotal = Math.Max(existing.qualityTotal, aggregate.qualityTotal);
                    existing.lastTick = Math.Max(existing.lastTick, aggregate.lastTick);
                }
            }
            target.provenance.AddRange(source.provenance);
            BoundFacet(target);
        }

        private static void BoundFacet(KnowledgeFacetStateRecord facet)
        {
            if (facet == null) return;
            KnowledgeSchema schema = KnowledgeRegistry.Schema(facet.domainId);
            int aggregateLimit = schema?.evidenceAggregateLimit ?? 128;
            int provenanceLimit = schema?.provenanceLimit ?? 16;
            facet.aggregates = facet.aggregates.Where(item => item != null && !item.key.NullOrEmpty())
                .OrderByDescending(item => item.lastTick).ThenBy(item => item.key).Take(Math.Max(8, aggregateLimit)).ToList();
            facet.eventCounts = facet.eventCounts.Where(pair => !pair.Key.NullOrEmpty() && pair.Value > 0)
                .OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Take(256)
                .ToDictionary(pair => pair.Key, pair => Math.Min(100000000, pair.Value), StringComparer.Ordinal);
            List<KnowledgeProvenanceRecord> provenance = facet.provenance.Where(item => item != null).OrderBy(item => item.tick).ToList();
            int skip = Math.Max(0, provenance.Count - Math.Max(0, provenanceLimit));
            facet.provenance = provenance.Skip(skip).ToList();
        }

        private static void MergeMinimumCounts(IDictionary<string, int> target, IDictionary<string, int> source)
        {
            if (source == null) return;
            foreach (KeyValuePair<string, int> pair in source)
                if (!pair.Key.NullOrEmpty() && pair.Value > 0)
                {
                    if (!target.ContainsKey(pair.Key) && target.Count >= 256) continue;
                    target[pair.Key] = Math.Max(target.TryGetValue(pair.Key, out int old) ? old : 0, Math.Min(100000000, pair.Value));
                }
        }

        private static Dictionary<string, int> MergeLegacyCounts(IEnumerable<IDictionary<string, int>> sources)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (IDictionary<string, int> source in sources ?? Enumerable.Empty<IDictionary<string, int>>())
                MergeMinimumCounts(result, source);
            return result;
        }

        private static string InsightKey(string domainId, string subjectId, string insightId, Pawn pawn, bool colony) =>
            domainId + "\n" + subjectId + "\n" + insightId + "\n" + (colony ? "C" : (pawn?.thingIDNumber ?? 0).ToString());
    }
}
