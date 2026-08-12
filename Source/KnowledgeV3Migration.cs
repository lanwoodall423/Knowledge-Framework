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
        private const int MaxDiagnosticPartLength = 96;
        private const int MaxRelationMetadata = 32;

        private sealed class MigrationPlan
        {
            internal readonly KnowledgeConsumerMigration migration;
            internal readonly IDictionary<string, int> eventCounts;
            internal readonly IReadOnlyList<KnowledgeSubjectRegistration> subjects;
            internal readonly IReadOnlyList<KnowledgeMeasurement> claims;
            internal readonly IReadOnlyList<KnowledgeMilestoneConditionSample> milestones;
            internal readonly IReadOnlyList<KnowledgeSubjectRelation> relations;

            internal MigrationPlan(KnowledgeConsumerMigration migration)
            {
                this.migration = migration;
                eventCounts = migration.eventCounts == null ? null :
                    new Dictionary<string, int>(migration.eventCounts, StringComparer.Ordinal);
                subjects = (migration.subjects ?? Array.Empty<KnowledgeSubjectRegistration>()).ToArray();
                claims = (migration.claims ?? Array.Empty<KnowledgeMeasurement>()).ToArray();
                milestones = (migration.milestones ?? Array.Empty<KnowledgeMilestoneConditionSample>()).ToArray();
                relations = (migration.relations ?? Array.Empty<KnowledgeSubjectRelation>()).ToArray();
            }
        }

        private sealed class SubjectValidationContext
        {
            internal readonly Dictionary<string, KnowledgeSubjectSnapshot> virtualSubjects =
                new Dictionary<string, KnowledgeSubjectSnapshot>(StringComparer.Ordinal);

            internal KnowledgeSubjectSnapshot Resolve(string domainId, string subjectId)
            {
                string canonicalDomain = KnowledgeRegistry.ResolveDomainId(domainId) ?? domainId;
                string canonicalSubject = KnowledgeRegistry.ResolveSubjectId(canonicalDomain, subjectId) ?? subjectId;
                if (virtualSubjects.TryGetValue(SubjectKey(canonicalDomain, canonicalSubject), out KnowledgeSubjectSnapshot replacement))
                    return replacement;
                return KnowledgeRegistry.ResolveSubject(domainId, subjectId);
            }

            internal void AddVirtual(string domainId, KnowledgeSubjectRegistration registration)
            {
                string canonicalDomain = KnowledgeRegistry.ResolveDomainId(domainId) ?? domainId;
                string canonicalSubject = KnowledgeRegistry.ResolveSubjectId(canonicalDomain, registration.id) ?? registration.id;
                virtualSubjects[SubjectKey(canonicalDomain, canonicalSubject)] =
                    new KnowledgeSubjectSnapshot(canonicalDomain, registration);
            }
        }

        private sealed class MigrationFailure
        {
            internal readonly string operation;
            internal readonly string domainId;
            internal readonly string subjectId;
            internal readonly string reason;

            internal MigrationFailure(string operation, string domainId, string subjectId, string reason)
            {
                this.operation = operation;
                this.domainId = domainId;
                this.subjectId = subjectId;
                this.reason = reason;
            }
        }

        public static bool IsCommitted(string consumerId, int version) =>
            GameComponent_KnowledgeFramework.Current?.HasConsumerMigrationV3(consumerId, version) == true;

        public static bool Import(KnowledgeConsumerMigration migration)
        {
            System.Diagnostics.Stopwatch stopwatch = KnowledgeDiagnostics.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;
            try
            {
                if (migration == null)
                    return Reject(null, Failure("validate migration", null, null, "Migration is null."));
                if (!ValidId(migration.consumerId))
                    return Reject(migration, Failure("validate consumer ID", migration.domainId, migration.subjectId,
                        "Consumer ID is missing or invalid."));
                if (migration.version < 1)
                    return Reject(migration, Failure("validate migration version", migration.domainId, migration.subjectId,
                        "Migration version must be positive."));

                GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
                if (component == null)
                    return Reject(migration, Failure("resolve component", migration.domainId, migration.subjectId,
                        "Knowledge component is unavailable."));
                if (component.HasConsumerMigrationV3(migration.consumerId, migration.version)) return true;

                if (!TryCreatePlan(migration, out MigrationPlan plan, out MigrationFailure failure))
                    return Reject(migration, failure);
                if (!Validate(plan, component, out failure))
                    return Reject(migration, failure);

                if (!ApplySubjects(plan, out failure)) return Reject(migration, failure);
                if (!ApplyMinimum(plan, out failure)) return Reject(migration, failure);
                if (!ApplyClaims(plan, component, out failure)) return Reject(migration, failure);
                if (!ApplyMilestones(plan, out failure)) return Reject(migration, failure);
                if (!ApplyRelations(plan, out failure)) return Reject(migration, failure);

                try
                {
                    component.CommitConsumerMigrationV3(migration.consumerId, migration.version);
                }
                catch (Exception exception)
                {
                    return Reject(migration, Failure("commit migration", migration.domainId, migration.subjectId,
                        "Commit threw " + exception.GetType().Name + "."));
                }
                if (!component.HasConsumerMigrationV3(migration.consumerId, migration.version))
                    return Reject(migration, Failure("verify migration commit", migration.domainId, migration.subjectId,
                        "Commit did not make the migration durable."));

                try
                {
                    KnowledgeEngine.NotifyExternalChange("migration:" + migration.consumerId);
                }
                catch (Exception exception)
                {
                    // The migration is already committed. Do not turn a notification
                    // failure into a retry that would duplicate monotonic work.
                    Log.Warning("[Knowledge Framework] Consumer migration notification failed: consumer=" +
                        Bounded(migration.consumerId) + " operation=external-change domain=" +
                        Bounded(migration.domainId) + " subject=" + Bounded(migration.subjectId) +
                        " reason=" + Bounded(exception.GetType().Name));
                }
                return true;
            }
            catch (Exception exception)
            {
                return Reject(migration, Failure("unexpected migration failure", migration?.domainId,
                    migration?.subjectId, "Unexpected exception " + exception.GetType().Name + "."));
            }
            finally
            {
                KnowledgeDiagnostics.RecordMigration(stopwatch?.ElapsedTicks ?? 0L);
            }
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

        private static bool TryCreatePlan(KnowledgeConsumerMigration migration, out MigrationPlan plan, out MigrationFailure failure)
        {
            plan = null;
            failure = null;
            try
            {
                plan = new MigrationPlan(migration);
                return true;
            }
            catch (Exception exception)
            {
                failure = Failure("snapshot migration inputs", migration.domainId, migration.subjectId,
                    "Input collection could not be read: " + exception.GetType().Name + ".");
                return false;
            }
        }

        private static bool Validate(MigrationPlan plan, GameComponent_KnowledgeFramework component,
            out MigrationFailure failure)
        {
            failure = null;
            KnowledgeConsumerMigration migration = plan.migration;
            if (!KnowledgeMath.IsFinite(migration.personalKnowledge) || !KnowledgeMath.IsFinite(migration.colonyKnowledge) ||
                !KnowledgeMath.IsFinite(migration.expertise) || migration.personalKnowledge < 0f ||
                migration.colonyKnowledge < 0f || migration.expertise < 0f)
            {
                failure = Failure("validate migration scalars", migration.domainId, migration.subjectId,
                    "Migration scalar values must be finite and non-negative.");
                return false;
            }

            if (plan.eventCounts != null)
            {
                if (plan.eventCounts.Count > 256)
                {
                    failure = Failure("validate event counts", migration.domainId, migration.subjectId,
                        "Event count entries exceed the bounded limit.");
                    return false;
                }
                foreach (KeyValuePair<string, int> pair in plan.eventCounts)
                    if (!ValidId(pair.Key) || pair.Value < 0 || pair.Value > 100000000)
                    {
                        failure = Failure("validate event counts", migration.domainId, migration.subjectId,
                            "Event count keys and values must be valid and bounded.");
                        return false;
                    }
            }

            bool hasDomain = !migration.domainId.NullOrEmpty();
            bool hasSubject = !migration.subjectId.NullOrEmpty();
            if (hasDomain != hasSubject)
            {
                failure = Failure("validate minimum target", migration.domainId, migration.subjectId,
                    "Minimum imports require both a domain and a subject.");
                return false;
            }
            if (hasDomain && !ValidId(migration.domainId) || hasSubject && !ValidId(migration.subjectId))
            {
                failure = Failure("validate minimum target", migration.domainId, migration.subjectId,
                    "Domain and subject IDs must be valid stable IDs.");
                return false;
            }
            KnowledgeSchema migrationSchema = hasDomain ? KnowledgeRegistry.Schema(migration.domainId) : null;
            if (hasDomain && migrationSchema == null)
            {
                failure = Failure("validate domain", migration.domainId, migration.subjectId,
                    "Migration references an unknown domain.");
                return false;
            }
            if (!hasDomain && (plan.eventCounts?.Count > 0 || migration.personalKnowledge > 0f ||
                migration.colonyKnowledge > 0f || migration.expertise > 0f))
            {
                failure = Failure("validate minimum target", migration.domainId, migration.subjectId,
                    "Minimum values were supplied without a target domain and subject.");
                return false;
            }

            SubjectValidationContext subjects = new SubjectValidationContext();
            HashSet<string> requestedSubjectKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (KnowledgeSubjectRegistration subject in plan.subjects)
            {
                if (!hasDomain || migrationSchema == null)
                {
                    failure = Failure("validate subject registration", migration.domainId, subject?.id,
                        "Subject registrations require a known migration domain.");
                    return false;
                }
                if (!ValidateSubjectRegistration(migration.domainId, migrationSchema, subject, out string subjectError))
                {
                    failure = Failure("validate subject registration", migration.domainId, subject?.id, subjectError);
                    return false;
                }
                string key = SubjectKey(KnowledgeRegistry.ResolveDomainId(migration.domainId) ?? migration.domainId, subject.id);
                if (!requestedSubjectKeys.Add(key))
                {
                    failure = Failure("validate subject registration", migration.domainId, subject.id,
                        "Duplicate subject registration.");
                    return false;
                }
                subjects.AddVirtual(migration.domainId, subject);
            }
            foreach (KnowledgeSubjectRegistration subject in plan.subjects)
                if (!subject.templateSubjectId.NullOrEmpty() && subjects.Resolve(migration.domainId, subject.templateSubjectId) == null)
                {
                    failure = Failure("validate subject template", migration.domainId, subject.id,
                        "Subject registration references an unknown template subject.");
                    return false;
                }

            if (hasDomain && subjects.Resolve(migration.domainId, migration.subjectId) == null)
            {
                failure = Failure("validate minimum subject", migration.domainId, migration.subjectId,
                    "Migration references an unknown subject.");
                return false;
            }

            foreach (KnowledgeMeasurement claim in plan.claims)
                if (!ValidateClaim(migration, subjects, claim, out failure)) return false;
            foreach (KnowledgeMilestoneConditionSample sample in plan.milestones)
                if (!ValidateMilestone(migration, subjects, sample, out failure)) return false;

            Dictionary<string, List<KeyValuePair<string, string>>> relationEdges = component.RelationRecordsV3(null)
                .Where(item => item != null && KnowledgeRelationService.Type(item.relationTypeId)?.parentage == true)
                .GroupBy(item => item.contextTypeId + "\n" + item.contextId)
                .ToDictionary(group => group.Key, group => group.Select(RelationEdge).ToList(), StringComparer.Ordinal);
            foreach (KnowledgeSubjectRelation relation in plan.relations)
            {
                if (!ValidateRelation(migration, subjects, relation, out failure)) return false;
                string contextKey = relation.context.typeId + "\n" + relation.context.stableId;
                if (!relationEdges.TryGetValue(contextKey, out List<KeyValuePair<string, string>> edges))
                    relationEdges[contextKey] = edges = new List<KeyValuePair<string, string>>();
                AddRelationEdges(edges, relation);
            }
            if (relationEdges.Values.Any(KnowledgeGraphValidation.HasCycle))
            {
                KnowledgeSubjectRelation relation = plan.relations.LastOrDefault();
                failure = Failure("validate relation cycle", relation?.domainId ?? migration.domainId,
                    relation?.fromSubjectId ?? migration.subjectId, "Structural relation graph contains a cycle.");
                return false;
            }
            return true;
        }

        private static bool ValidateSubjectRegistration(string domainId, KnowledgeSchema schema,
            KnowledgeSubjectRegistration subject, out string error)
        {
            error = null;
            if (subject == null || !ValidId(subject.id))
            {
                error = "Subject registration ID is invalid.";
                return false;
            }
            if (!Enum.IsDefined(typeof(KnowledgeSubjectState), subject.state))
            {
                error = "Subject registration state is invalid.";
                return false;
            }
            if (!KnowledgeMath.IsFinite(subject.templateKnowledgeCoefficient) ||
                !KnowledgeMath.IsFinite(subject.templateConfidenceCoefficient) ||
                subject.templateKnowledgeCoefficient < 0f || subject.templateKnowledgeCoefficient > 1f ||
                subject.templateConfidenceCoefficient < 0f || subject.templateConfidenceCoefficient > 1f)
            {
                error = "Subject registration coefficients must be finite and between zero and one.";
                return false;
            }
            if (!ValidOptionalId(subject.templateSubjectId) || !ValidOptionalId(subject.archetypeId))
            {
                error = "Subject registration references an invalid template or archetype ID.";
                return false;
            }
            if (!subject.archetypeId.NullOrEmpty() && schema.Archetype(subject.archetypeId) == null)
            {
                error = "Subject registration references an unknown archetype.";
                return false;
            }
            if (!ValidateIdList(subject.categoryIds) || !ValidateIdList(subject.applicableFacetIds) ||
                !ValidateIdList(subject.applicableClaimIds))
            {
                error = "Subject registration contains an invalid ID list.";
                return false;
            }
            if ((subject.applicableFacetIds ?? Array.Empty<string>()).Any(item => schema.Facet(item) == null) ||
                (subject.applicableClaimIds ?? Array.Empty<string>()).Any(item => schema.Claim(item) == null))
            {
                error = "Subject registration references an unknown facet or claim.";
                return false;
            }
            return true;
        }

        private static bool ValidateClaim(KnowledgeConsumerMigration migration, SubjectValidationContext subjects,
            KnowledgeMeasurement measurement, out MigrationFailure failure)
        {
            failure = null;
            KnowledgeObservation observation = ObservationFor(migration, measurement);
            if (measurement == null)
            {
                failure = Failure("validate claim", observation.domainId, observation.subjectId,
                    "Claim measurement is null.");
                return false;
            }
            string domainId = KnowledgeRegistry.ResolveDomainId(measurement.domainId ?? observation.domainId);
            string subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, measurement.subjectId ?? observation.subjectId);
            if (!ValidId(domainId) || !ValidId(subjectId))
            {
                failure = Failure("validate claim", measurement.domainId ?? observation.domainId,
                    measurement.subjectId ?? observation.subjectId, "Claim domain and subject IDs are invalid.");
                return false;
            }
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            KnowledgeSubjectSnapshot subject = subjects.Resolve(domainId, subjectId);
            KnowledgeClaimDef claim = schema?.Claim(measurement.claimId);
            if (schema == null || subject == null)
            {
                failure = Failure("validate claim", domainId, subjectId,
                    "Claim references an unknown domain or subject.");
                return false;
            }
            if (claim == null)
            {
                failure = Failure("validate claim", domainId, subjectId, "Claim references an unknown claim.");
                return false;
            }
            string facetId = measurement.facetId.NullOrEmpty() ? claim.facetId ?? KnowledgeSchema.DefaultFacetId : measurement.facetId;
            if (!ValidId(facetId) || schema.Facet(facetId) == null ||
                !ApplicableFacets(schema, subject).Any(item => item.id == facetId) ||
                !ApplicableClaims(schema, subject, facetId).Any(item => item.StableId == claim.StableId))
            {
                failure = Failure("validate claim", domainId, subjectId, "Claim facet or applicability is invalid.");
                return false;
            }
            if (measurement.value == null)
            {
                failure = Failure("validate typed claim", domainId, subjectId, "Claim value is missing.");
                return false;
            }
            if (!measurement.value.TryValidate(claim.valueType, out string valueError))
            {
                failure = Failure("validate typed claim", domainId, subjectId, valueError ?? "Claim value is invalid.");
                return false;
            }
            float[] weights = { measurement.quality, measurement.evidenceWeight, measurement.confidenceFactor };
            if (weights.Any(value => !KnowledgeMath.IsFinite(value) || value < 0f) || measurement.confidenceFactor > 1f)
            {
                failure = Failure("validate claim weights", domainId, subjectId,
                    "Claim weights must be finite and non-negative, with confidence at most one.");
                return false;
            }
            if (!Enum.IsDefined(typeof(KnowledgeScope), measurement.scope) ||
                !Enum.IsDefined(typeof(KnowledgeEvidenceDisposition), measurement.disposition))
            {
                failure = Failure("validate claim", domainId, subjectId, "Claim scope or disposition is invalid.");
                return false;
            }
            if (measurement.scope == KnowledgeScope.Personal && measurement.observer == null)
            {
                failure = Failure("validate claim observer", domainId, subjectId,
                    "Personal claims require an observer.");
                return false;
            }
            if (InvalidContext(measurement.context))
            {
                failure = Failure("validate claim context", domainId, subjectId,
                    "Claim context is partial or contains an invalid ID.");
                return false;
            }
            return true;
        }

        private static bool ValidateMilestone(KnowledgeConsumerMigration migration, SubjectValidationContext subjects,
            KnowledgeMilestoneConditionSample sample, out MigrationFailure failure)
        {
            failure = null;
            if (sample == null)
            {
                failure = Failure("validate milestone", migration.domainId, migration.subjectId, "Milestone sample is null.");
                return false;
            }
            string domainId = sample.domainId ?? migration.domainId;
            string subjectId = sample.subjectId ?? migration.subjectId;
            if (!ValidId(domainId) || !ValidId(subjectId) || !ValidId(sample.trackId) || !ValidId(sample.milestoneId))
            {
                failure = Failure("validate milestone", domainId, subjectId, "Milestone IDs are invalid.");
                return false;
            }
            domainId = KnowledgeRegistry.ResolveDomainId(domainId);
            subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, subjectId);
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            KnowledgeMilestoneTrackDef track = schema?.milestoneTracks?.FirstOrDefault(item => item?.StableId == sample.trackId);
            KnowledgeMilestoneDef milestone = track?.milestones?.FirstOrDefault(item => item?.StableId == sample.milestoneId);
            if (schema == null || subjects.Resolve(domainId, subjectId) == null || track == null || milestone == null)
            {
                failure = Failure("validate milestone", domainId, subjectId,
                    "Milestone references an unknown domain, subject, track, or milestone.");
                return false;
            }
            if (!KnowledgeMath.IsFinite(sample.value) || sample.value < 0f || sample.elapsedTicks < 0)
            {
                failure = Failure("validate milestone values", domainId, subjectId,
                    "Milestone value and elapsed ticks must be finite and non-negative.");
                return false;
            }
            if (InvalidContext(sample.context))
            {
                failure = Failure("validate milestone context", domainId, subjectId,
                    "Milestone context is partial or contains an invalid ID.");
                return false;
            }
            return true;
        }

        private static bool ValidateRelation(KnowledgeConsumerMigration migration, SubjectValidationContext subjects,
            KnowledgeSubjectRelation relation, out MigrationFailure failure)
        {
            failure = null;
            if (relation == null || !ValidId(relation.domainId) || !ValidId(relation.fromSubjectId) ||
                !ValidId(relation.toDomainId) || !ValidId(relation.toSubjectId) || !ValidId(relation.relationTypeId))
            {
                failure = Failure("validate relation", relation?.domainId ?? migration.domainId,
                    relation?.fromSubjectId ?? migration.subjectId, "Relation IDs are invalid.");
                return false;
            }
            string domainId = KnowledgeRegistry.ResolveDomainId(relation.domainId);
            string fromSubjectId = KnowledgeRegistry.ResolveSubjectId(domainId, relation.fromSubjectId);
            string toDomainId = KnowledgeRegistry.ResolveDomainId(relation.toDomainId);
            string toSubjectId = KnowledgeRegistry.ResolveSubjectId(toDomainId, relation.toSubjectId);
            KnowledgeSubjectRelationTypeDef type = KnowledgeRelationService.Type(relation.relationTypeId);
            if (KnowledgeRegistry.Schema(domainId) == null || KnowledgeRegistry.Schema(toDomainId) == null ||
                subjects.Resolve(domainId, fromSubjectId) == null || subjects.Resolve(toDomainId, toSubjectId) == null)
            {
                failure = Failure("validate relation subjects", domainId, fromSubjectId,
                    "Relation references an unknown domain or subject.");
                return false;
            }
            if (type == null)
            {
                failure = Failure("validate relation type", domainId, fromSubjectId, "Relation type is unknown.");
                return false;
            }
            if (!KnowledgeMath.IsFinite(relation.confidence) || relation.confidence < 0f || relation.confidence > 1f)
            {
                failure = Failure("validate relation confidence", domainId, fromSubjectId,
                    "Relation confidence must be finite and between zero and one.");
                return false;
            }
            if (relation.tick < 0 || InvalidContext(relation.context))
            {
                failure = Failure("validate relation metadata", domainId, fromSubjectId,
                    "Relation tick or context is invalid.");
                return false;
            }
            if (!ValidateRelationMetadata(relation.metadata, type, out string metadataError))
            {
                failure = Failure("validate relation metadata", domainId, fromSubjectId, metadataError);
                return false;
            }
            string inverseTypeId = type.inverseTypeId.NullOrEmpty() ? type.StableId : type.inverseTypeId;
            if (type.symmetric || !type.inverseTypeId.NullOrEmpty())
            {
                KnowledgeSubjectRelationTypeDef inverse = KnowledgeRelationService.Type(inverseTypeId);
                if (!ValidId(inverseTypeId) || inverse == null)
                {
                    failure = Failure("validate inverse relation type", domainId, fromSubjectId,
                        "Relation inverse type is unknown.");
                    return false;
                }
                if (!ValidateRelationMetadata(relation.metadata, inverse, out metadataError))
                {
                    failure = Failure("validate inverse relation metadata", domainId, fromSubjectId, metadataError);
                    return false;
                }
            }
            return true;
        }

        private static bool ValidateRelationMetadata(Dictionary<string, string> metadata,
            KnowledgeSubjectRelationTypeDef type, out string error)
        {
            error = null;
            if (metadata == null) return true;
            int limit = Math.Min(MaxRelationMetadata, Math.Max(1, type.metadataLimit));
            if (metadata.Count > limit)
            {
                error = "Relation metadata exceeds the configured bound.";
                return false;
            }
            if (metadata.Any(pair => !ValidId(pair.Key) || pair.Value == null))
            {
                error = "Relation metadata keys and values are invalid.";
                return false;
            }
            return true;
        }

        private static bool ApplySubjects(MigrationPlan plan, out MigrationFailure failure)
        {
            failure = null;
            foreach (KnowledgeSubjectRegistration subject in plan.subjects)
            {
                bool registered = KnowledgeRegistry.RegisterSubject(plan.migration.domainId, subject, new KnowledgeRegistrationOptions
                {
                    source = plan.migration.consumerId,
                    priority = int.MaxValue,
                    conflict = KnowledgeRegistrationConflict.Replace
                });
                if (!registered)
                {
                    failure = Failure("register subject", plan.migration.domainId, subject.id,
                        "Subject registration returned false.");
                    return false;
                }
                if (KnowledgeRegistry.ResolveSubject(plan.migration.domainId, subject.id) == null)
                {
                    failure = Failure("verify subject registration", plan.migration.domainId, subject.id,
                        "Subject registration did not produce a resolvable subject.");
                    return false;
                }
            }
            return true;
        }

        private static bool ApplyMinimum(MigrationPlan plan, out MigrationFailure failure)
        {
            failure = null;
            if (plan.migration.domainId.NullOrEmpty() && plan.migration.subjectId.NullOrEmpty()) return true;
            bool imported = KnowledgeService.ImportMinimum(plan.migration.domainId, plan.migration.subjectId, plan.migration.pawn,
                plan.migration.personalKnowledge, plan.migration.colonyKnowledge, plan.migration.expertise, plan.eventCounts);
            if (imported) return true;
            failure = Failure("import minimum", plan.migration.domainId, plan.migration.subjectId,
                "KnowledgeService.ImportMinimum returned false.");
            return false;
        }

        private static bool ApplyClaims(MigrationPlan plan, GameComponent_KnowledgeFramework component,
            out MigrationFailure failure)
        {
            failure = null;
            foreach (KnowledgeMeasurement claim in plan.claims)
            {
                KnowledgeObservation observation = ObservationFor(plan.migration, claim);
                try
                {
                    if (!KnowledgeClaimService.ValidateMeasurement(claim, observation, out string validationError))
                    {
                        failure = Failure("validate claim before apply", observation.domainId, observation.subjectId, validationError);
                        return false;
                    }
                    if (ClaimAlreadyApplied(component, claim, observation)) continue;
                    if (!KnowledgeClaimService.Apply(component, claim, observation))
                    {
                        failure = Failure("apply claim", observation.domainId, observation.subjectId,
                            "Claim application returned false.");
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    failure = Failure("apply claim", observation.domainId, observation.subjectId,
                        "Claim application threw " + exception.GetType().Name + ".");
                    return false;
                }
            }
            return true;
        }

        private static bool ApplyMilestones(MigrationPlan plan, out MigrationFailure failure)
        {
            failure = null;
            foreach (KnowledgeMilestoneConditionSample sample in plan.milestones)
            {
                KnowledgeMilestoneConditionSample prepared = PrepareMilestone(plan.migration, sample);
                try
                {
                    if (!prepared.conditionMet)
                    {
                        failure = Failure("report milestone", prepared.domainId, prepared.subjectId,
                            "Migration milestone condition is not met.");
                        return false;
                    }
                    if (!KnowledgeMilestoneService.ReportCondition(prepared))
                    {
                        failure = Failure("report milestone", prepared.domainId, prepared.subjectId,
                            "Milestone reporting returned false.");
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    failure = Failure("report milestone", prepared.domainId, prepared.subjectId,
                        "Milestone reporting threw " + exception.GetType().Name + ".");
                    return false;
                }
            }
            return true;
        }

        private static bool ApplyRelations(MigrationPlan plan, out MigrationFailure failure)
        {
            failure = null;
            foreach (KnowledgeSubjectRelation relation in plan.relations)
            {
                try
                {
                    if (!KnowledgeRelationService.Add(relation))
                    {
                        failure = Failure("insert relation", relation.domainId, relation.fromSubjectId,
                            "Relation insertion returned false.");
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    failure = Failure("insert relation", relation.domainId, relation.fromSubjectId,
                        "Relation insertion threw " + exception.GetType().Name + ".");
                    return false;
                }
            }
            return true;
        }

        private static bool ClaimAlreadyApplied(GameComponent_KnowledgeFramework component,
            KnowledgeMeasurement measurement, KnowledgeObservation observation)
        {
            string domainId = KnowledgeRegistry.ResolveDomainId(measurement.domainId ?? observation.domainId);
            string subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, measurement.subjectId ?? observation.subjectId);
            KnowledgeClaimDef claim = KnowledgeRegistry.Schema(domainId)?.Claim(measurement.claimId);
            if (domainId.NullOrEmpty() || subjectId.NullOrEmpty() || claim == null) return false;
            string facetId = measurement.facetId.NullOrEmpty() ? claim.facetId ?? KnowledgeSchema.DefaultFacetId : measurement.facetId;
            bool colony = measurement.scope == KnowledgeScope.Colony || observation.targetColony;
            Pawn observer = colony ? null : measurement.observer ?? observation.observer;
            KnowledgeContextKey context = KnowledgeV3Runtime.ContextFor(observation);
            if (KnowledgeV3Runtime.ContextPropagates(observation) && !measurement.context.IsEmpty) context = measurement.context;
            KnowledgeMeasurement expectedInput = measurement.Clone();
            expectedInput.domainId = domainId;
            expectedInput.subjectId = subjectId;
            expectedInput.facetId = facetId;
            expectedInput.claimId = claim.StableId;
            // Apply uses the observation context when the measurement leaves it
            // empty. Mirror that effective identity so retries do not append a
            // duplicate record for contextual claims.
            expectedInput.context = context;
            KnowledgeMeasurementRecord expected = KnowledgeMeasurementRecord.FromMeasurement(expectedInput, domainId, subjectId,
                facetId, observer, colony ? KnowledgeScope.Colony : KnowledgeScope.Personal);
            if (expected == null) return false;
            foreach (KnowledgeClaimStateRecord record in component.ClaimRecordsV3(domainId, subjectId, observer, colony)
                .Where(item => item != null && item.facetId == facetId && item.claimId == claim.StableId &&
                    item.contextTypeId == context.typeId && item.contextId == context.stableId))
                if ((record.measurements ?? new List<KnowledgeMeasurementRecord>()).Any(item => SameMeasurement(item, expected, measurement.tick)))
                    return true;
            return false;
        }

        private static bool SameMeasurement(KnowledgeMeasurementRecord left, KnowledgeMeasurementRecord right, int requestedTick)
        {
            if (left == null || right == null) return false;
            KnowledgeClaimValue leftValue = left.ToValue();
            KnowledgeClaimValue rightValue = right.ToValue();
            if (leftValue == null || rightValue == null) return false;
            return left.domainId == right.domainId && left.subjectId == right.subjectId && left.facetId == right.facetId &&
                left.claimId == right.claimId && left.observer == right.observer && left.scope == right.scope &&
                left.contextTypeId == right.contextTypeId && left.contextId == right.contextId &&
                left.valueType == right.valueType && leftValue.StableKey() == rightValue.StableKey() &&
                left.quality == right.quality && left.evidenceWeight == right.evidenceWeight &&
                left.confidenceFactor == right.confidenceFactor && left.disposition == right.disposition &&
                left.source == right.source && left.sourceInstanceId == right.sourceInstanceId &&
                left.methodId == right.methodId && left.reasonId == right.reasonId && left.specimenId == right.specimenId &&
                left.summary == right.summary && left.documented == right.documented && left.revealed == right.revealed &&
                (requestedTick < 0 || left.tick == right.tick);
        }

        private static KnowledgeObservation ObservationFor(KnowledgeConsumerMigration migration, KnowledgeMeasurement claim) =>
            new KnowledgeObservation
            {
                domainId = claim?.domainId ?? migration?.domainId,
                subjectId = claim?.subjectId ?? migration?.subjectId,
                facetId = claim?.facetId,
                observer = claim?.observer,
                targetColony = claim?.scope == KnowledgeScope.Colony
            };

        private static KnowledgeMilestoneConditionSample PrepareMilestone(KnowledgeConsumerMigration migration,
            KnowledgeMilestoneConditionSample sample) => new KnowledgeMilestoneConditionSample
            {
                domainId = sample?.domainId ?? migration?.domainId,
                subjectId = sample?.subjectId ?? migration?.subjectId,
                trackId = sample?.trackId,
                milestoneId = sample?.milestoneId,
                pawn = sample?.pawn,
                context = sample?.context ?? default(KnowledgeContextKey),
                conditionMet = sample?.conditionMet ?? false,
                value = sample?.value ?? 0f,
                elapsedTicks = sample?.elapsedTicks ?? 0,
                completingPawn = sample?.completingPawn,
                interruptionReason = sample?.interruptionReason
            };

        private static void AddRelationEdges(List<KeyValuePair<string, string>> edges, KnowledgeSubjectRelation relation)
        {
            KnowledgeSubjectRelationTypeDef type = KnowledgeRelationService.Type(relation.relationTypeId);
            if (type == null) return;
            string domainId = KnowledgeRegistry.ResolveDomainId(relation.domainId) ?? relation.domainId;
            string fromSubjectId = KnowledgeRegistry.ResolveSubjectId(domainId, relation.fromSubjectId) ?? relation.fromSubjectId;
            string toDomainId = KnowledgeRegistry.ResolveDomainId(relation.toDomainId) ?? relation.toDomainId;
            string toSubjectId = KnowledgeRegistry.ResolveSubjectId(toDomainId, relation.toSubjectId) ?? relation.toSubjectId;
            if (type.parentage) edges.Add(new KeyValuePair<string, string>(NodeKey(domainId, fromSubjectId),
                NodeKey(toDomainId, toSubjectId)));
            if (type.symmetric || !type.inverseTypeId.NullOrEmpty())
            {
                KnowledgeSubjectRelationTypeDef inverse = KnowledgeRelationService.Type(type.inverseTypeId.NullOrEmpty() ? type.StableId : type.inverseTypeId);
                if (inverse?.parentage == true) edges.Add(new KeyValuePair<string, string>(NodeKey(toDomainId, toSubjectId),
                    NodeKey(domainId, fromSubjectId)));
            }
        }

        private static KeyValuePair<string, string> RelationEdge(KnowledgeSubjectRelationStateRecord relation)
        {
            string domainId = KnowledgeRegistry.ResolveDomainId(relation.domainId) ?? relation.domainId;
            string fromSubjectId = KnowledgeRegistry.ResolveSubjectId(domainId, relation.fromSubjectId) ?? relation.fromSubjectId;
            string toDomainId = KnowledgeRegistry.ResolveDomainId(relation.toDomainId) ?? relation.toDomainId;
            string toSubjectId = KnowledgeRegistry.ResolveSubjectId(toDomainId, relation.toSubjectId) ?? relation.toSubjectId;
            return new KeyValuePair<string, string>(NodeKey(domainId, fromSubjectId), NodeKey(toDomainId, toSubjectId));
        }

        private static IEnumerable<KnowledgeFacetSchema> ApplicableFacets(KnowledgeSchema schema, KnowledgeSubjectSnapshot subject)
        {
            if (subject.applicableFacetIds != null && subject.applicableFacetIds.Count > 0)
                return schema.facets.Where(item => subject.applicableFacetIds.Contains(item.id));
            KnowledgeSubjectArchetypeDef archetype = schema.Archetype(subject.archetypeId);
            if (archetype?.applicableFacetIds != null && archetype.applicableFacetIds.Count > 0)
                return schema.facets.Where(item => archetype.applicableFacetIds.Contains(item.id));
            return schema.facets;
        }

        private static IEnumerable<KnowledgeClaimDef> ApplicableClaims(KnowledgeSchema schema, KnowledgeSubjectSnapshot subject,
            string facetId)
        {
            KnowledgeFacetSchema facet = schema.Facet(facetId);
            KnowledgeSubjectArchetypeDef archetype = schema.Archetype(subject.archetypeId);
            IEnumerable<string> ids = subject.applicableClaimIds?.Count > 0 ? subject.applicableClaimIds : archetype?.applicableClaimIds;
            IEnumerable<KnowledgeClaimDef> claims = schema.claims.Where(claim => claim.facetId.NullOrEmpty() || claim.facetId == facet.id);
            if (ids != null && ids.Any()) claims = claims.Where(claim => ids.Contains(claim.StableId));
            if (facet.claimIds != null && facet.claimIds.Count > 0) claims = claims.Where(claim => facet.claimIds.Contains(claim.StableId));
            return claims;
        }

        private static bool ValidateIdList(IReadOnlyList<string> values) =>
            values == null || values.All(ValidId);

        private static bool InvalidContext(KnowledgeContextKey context) =>
            context.IsPartial || !ValidOptionalId(context.typeId) || !ValidOptionalId(context.stableId);

        private static bool ValidOptionalId(string value) => value.NullOrEmpty() || ValidId(value);

        private static bool ValidId(string value) => !value.NullOrEmpty() && value.Trim() == value && value.IndexOf('\n') < 0 &&
            value.IndexOf('\r') < 0;

        private static string SubjectKey(string domainId, string subjectId) => domainId + "\n" + subjectId;

        private static string NodeKey(string domainId, string subjectId) => domainId + "\n" + subjectId;

        private static MigrationFailure Failure(string operation, string domainId, string subjectId, string reason) =>
            new MigrationFailure(operation, domainId, subjectId, reason);

        private static bool Reject(KnowledgeConsumerMigration migration, MigrationFailure failure)
        {
            Log.Warning("[Knowledge Framework] Consumer migration rejected: consumer=" +
                Bounded(migration?.consumerId) + " operation=" + Bounded(failure?.operation) +
                " domain=" + Bounded(failure?.domainId ?? migration?.domainId) +
                " subject=" + Bounded(failure?.subjectId ?? migration?.subjectId) +
                " reason=" + Bounded(failure?.reason));
            return false;
        }

        private static string Bounded(string value)
        {
            if (value.NullOrEmpty()) return "<none>";
            value = value.Replace('\r', ' ').Replace('\n', ' ');
            return value.Length <= MaxDiagnosticPartLength ? value : value.Substring(0, MaxDiagnosticPartLength);
        }
    }
}
