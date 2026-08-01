using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    public sealed class KnowledgeConsumerMigration
    {
        public string consumerId;
        public int version;
        public string domainId;
        public string subjectId;
        public Pawn pawn;
        public float personalKnowledge;
        public float colonyKnowledge;
        public float expertise;
        public IDictionary<string, int> eventCounts;
        public IReadOnlyList<KnowledgeMeasurement> claims;
        public IReadOnlyList<KnowledgeMilestoneConditionSample> milestones;
        public IReadOnlyList<KnowledgeSubjectRegistration> subjects;
        public IReadOnlyList<KnowledgeSubjectRelation> relations;
    }

    public static class KnowledgeMigrationService
    {
        public static bool IsCommitted(string consumerId, int version) =>
            GameComponent_KnowledgeFramework.Current?.HasConsumerMigrationV3(consumerId, version) == true;

        public static bool Import(KnowledgeConsumerMigration migration)
        {
            System.Diagnostics.Stopwatch stopwatch = KnowledgeDiagnostics.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;
            if (migration == null || migration.consumerId.NullOrEmpty() || migration.version < 1) return false;
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component == null || IsCommitted(migration.consumerId, migration.version)) return true;
            if (!Validate(migration, out string error))
            {
                Log.Warning("[Knowledge Framework] Consumer migration rejected: " + error);
                KnowledgeDiagnostics.RecordMigration(stopwatch?.ElapsedTicks ?? 0L);
                return false;
            }
            foreach (KnowledgeSubjectRegistration subject in migration.subjects ?? Array.Empty<KnowledgeSubjectRegistration>())
                if (KnowledgeRegistry.ResolveSubject(migration.domainId, subject.id) == null)
                    KnowledgeRegistry.RegisterSubject(migration.domainId, subject, new KnowledgeRegistrationOptions
                    {
                        source = migration.consumerId,
                        priority = int.MaxValue,
                        conflict = KnowledgeRegistrationConflict.Replace
                    });
            if (!migration.domainId.NullOrEmpty() && !migration.subjectId.NullOrEmpty())
                KnowledgeService.ImportMinimum(migration.domainId, migration.subjectId, migration.pawn, migration.personalKnowledge,
                    migration.colonyKnowledge, migration.expertise, migration.eventCounts);
            foreach (KnowledgeMeasurement claim in migration.claims ?? Array.Empty<KnowledgeMeasurement>())
                KnowledgeClaimService.Apply(component, claim, new KnowledgeObservation
                {
                    domainId = claim.domainId,
                    subjectId = claim.subjectId,
                    facetId = claim.facetId,
                    observer = claim.observer,
                    targetColony = claim.scope == KnowledgeScope.Colony
                });
            foreach (KnowledgeMilestoneConditionSample sample in migration.milestones ?? Array.Empty<KnowledgeMilestoneConditionSample>())
                KnowledgeMilestoneService.ReportCondition(sample);
            foreach (KnowledgeSubjectRelation relation in migration.relations ?? Array.Empty<KnowledgeSubjectRelation>())
                KnowledgeRelationService.Add(relation);
            component.CommitConsumerMigrationV3(migration.consumerId, migration.version);
            KnowledgeEngine.NotifyExternalChange("migration:" + migration.consumerId);
            KnowledgeDiagnostics.RecordMigration(stopwatch?.ElapsedTicks ?? 0L);
            return true;
        }

        public static bool ImportMinimum(string consumerId, int version, string domainId, string subjectId, Pawn pawn,
            float personalKnowledge, float colonyKnowledge, float expertise, IDictionary<string, int> eventCounts = null) => Import(new KnowledgeConsumerMigration
            {
                consumerId = consumerId,
                version = version,
                domainId = domainId,
                subjectId = subjectId,
                pawn = pawn,
                personalKnowledge = personalKnowledge,
                colonyKnowledge = colonyKnowledge,
                expertise = expertise,
                eventCounts = eventCounts
            });

        private static bool Validate(KnowledgeConsumerMigration migration, out string error)
        {
            error = null;
            if (!KnowledgeMath.IsFinite(migration.personalKnowledge) || !KnowledgeMath.IsFinite(migration.colonyKnowledge) || !KnowledgeMath.IsFinite(migration.expertise) ||
                migration.personalKnowledge < 0f || migration.colonyKnowledge < 0f || migration.expertise < 0f)
            { error = "Migration scalar values must be finite and non-negative."; return false; }
            if (!migration.domainId.NullOrEmpty() && KnowledgeRegistry.Schema(migration.domainId) == null)
            { error = "Migration references an unknown domain."; return false; }
            foreach (KnowledgeMeasurement claim in migration.claims ?? Array.Empty<KnowledgeMeasurement>())
            {
                if (!KnowledgeClaimService.ValidateMeasurement(claim, new KnowledgeObservation
                {
                    domainId = claim?.domainId,
                    subjectId = claim?.subjectId,
                    facetId = claim?.facetId,
                    observer = claim?.observer,
                    targetColony = claim?.scope == KnowledgeScope.Colony
                }, out error)) return false;
            }
            foreach (KnowledgeSubjectRelation relation in migration.relations ?? Array.Empty<KnowledgeSubjectRelation>())
                if (relation == null || relation.domainId.NullOrEmpty() || relation.fromSubjectId.NullOrEmpty() || relation.toDomainId.NullOrEmpty() || relation.toSubjectId.NullOrEmpty())
                { error = "Migration relation IDs are invalid."; return false; }
            return true;
        }
    }
}
