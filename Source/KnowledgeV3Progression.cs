using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    public static class KnowledgeMilestoneService
    {
        public static event Action<KnowledgeMilestoneChangedEvent> Changed;

        public static KnowledgeMilestoneState State(string domainId, string subjectId, string trackId, string milestoneId,
            Pawn pawn = null, KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            KnowledgeMilestoneStateRecord record = GameComponent_KnowledgeFramework.Current?.MilestoneV3(
                KnowledgeRegistry.ResolveDomainId(domainId), KnowledgeRegistry.ResolveSubjectId(domainId, subjectId), trackId, milestoneId,
                pawn, context, false);
            return ToState(record, domainId, subjectId, trackId, milestoneId, pawn, context);
        }

        public static IReadOnlyList<KnowledgeMilestoneState> States(string domainId, string subjectId, Pawn pawn = null,
            KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            if (schema == null) return Array.Empty<KnowledgeMilestoneState>();
            List<KnowledgeMilestoneState> result = new List<KnowledgeMilestoneState>();
            foreach (KnowledgeMilestoneTrackDef track in schema.milestoneTracks)
                foreach (KnowledgeMilestoneDef milestone in track.milestones ?? new List<KnowledgeMilestoneDef>())
                    result.Add(State(domainId, subjectId, track.StableId, milestone.StableId, pawn, context));
            return result;
        }

        public static bool IsCompleted(string domainId, string subjectId, string trackId, string milestoneId, Pawn pawn,
            KnowledgeContextKey context = default(KnowledgeContextKey)) => State(domainId, subjectId, trackId, milestoneId, pawn, context).completed;

        public static bool ReportCondition(KnowledgeMilestoneConditionSample sample)
        {
            if (sample == null || sample.domainId.NullOrEmpty() || sample.subjectId.NullOrEmpty() || sample.trackId.NullOrEmpty() || sample.milestoneId.NullOrEmpty()) return false;
            KnowledgeSchema schema = KnowledgeRegistry.Schema(sample.domainId);
            KnowledgeMilestoneTrackDef track = schema?.milestoneTracks.FirstOrDefault(item => item.StableId == sample.trackId);
            KnowledgeMilestoneDef milestone = track?.milestones?.FirstOrDefault(item => item?.StableId == sample.milestoneId);
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (track == null || milestone == null || component == null) return false;
            KnowledgeMilestoneStateRecord record = component.MilestoneV3(sample.domainId, sample.subjectId, track.StableId, milestone.StableId,
                sample.pawn, sample.context, true);
            bool requirementsMet = milestone.requirements == null || KnowledgeRequirementService.Evaluate(milestone.requirements, sample.domainId,
                sample.subjectId, sample.pawn, KnowledgeScope.Personal, sample.context, out _);
            bool met = sample.conditionMet && requirementsMet && PreviousMilestonesComplete(track, milestone, sample, component);
            int tick = Find.TickManager?.TicksGame ?? 0;
            KnowledgeMilestoneState before = ToState(record, sample.domainId, sample.subjectId, track.StableId, milestone.StableId, sample.pawn, sample.context);
            if (record.completed && (milestone.permanent || !(track.repeatable || milestone.repeatable))) return true;
            if (!met)
            {
                if (!record.started) return false;
                if (milestone.pauseBehavior == KnowledgeMilestonePauseBehavior.Pause) return false;
                record.interrupted = true;
                record.available = false;
                record.interruptionReason = sample.interruptionReason ?? "condition failed";
                if (milestone.pauseBehavior == KnowledgeMilestonePauseBehavior.Interrupt || milestone.pauseBehavior == KnowledgeMilestonePauseBehavior.Reset ||
                    milestone.resetBehavior == KnowledgeMilestoneResetBehavior.OnInterruption || milestone.resetBehavior == KnowledgeMilestoneResetBehavior.OnFailure)
                {
                    record.started = false;
                    record.progress = 0f;
                    record.startTick = 0;
                }
                component.TouchV3();
                Emit(KnowledgeMilestoneEventKind.Interrupted, ToState(record, sample.domainId, sample.subjectId, track.StableId, milestone.StableId, sample.pawn, sample.context), record.interruptionReason);
                return false;
            }
            if (!record.available)
            {
                record.available = true;
                Emit(KnowledgeMilestoneEventKind.Available, ToState(record, sample.domainId, sample.subjectId, track.StableId, milestone.StableId, sample.pawn, sample.context), null);
            }
            if (!record.started)
            {
                record.started = true;
                record.interrupted = false;
                record.startTick = tick;
                record.interruptionReason = null;
                Emit(KnowledgeMilestoneEventKind.Started, ToState(record, sample.domainId, sample.subjectId, track.StableId, milestone.StableId, sample.pawn, sample.context), null);
            }
            int elapsed = sample.elapsedTicks > 0 ? sample.elapsedTicks : Math.Max(0, tick - record.startTick);
            float target = milestone.sustainedTicks <= 0 ? 1f : Math.Min(1f, (float)elapsed / Math.Max(1, milestone.sustainedTicks));
            record.progress = Math.Max(record.progress, target);
            record.bestHistoricalValue = Math.Max(record.bestHistoricalValue, KnowledgeMath.NonNegativeFiniteOr(sample.value, target));
            if (record.progress >= 1f)
            {
                record.completed = true;
                record.completionTick = tick;
                record.completingPawn = sample.completingPawn ?? sample.pawn;
                record.interrupted = false;
                Emit(KnowledgeMilestoneEventKind.Completed, ToState(record, sample.domainId, sample.subjectId, track.StableId, milestone.StableId, sample.pawn, sample.context), null);
            }
            component.TouchV3();
            return true;
        }

        public static bool Confirm(string domainId, string subjectId, string trackId, string milestoneId, Pawn pawn = null,
            KnowledgeContextKey context = default(KnowledgeContextKey)) => ReportCondition(new KnowledgeMilestoneConditionSample
            {
                domainId = domainId,
                subjectId = subjectId,
                trackId = trackId,
                milestoneId = milestoneId,
                pawn = pawn,
                context = context,
                conditionMet = true,
                elapsedTicks = int.MaxValue
            });

        public static bool Interrupt(string domainId, string subjectId, string trackId, string milestoneId, string reason,
            Pawn pawn = null, KnowledgeContextKey context = default(KnowledgeContextKey)) => ReportCondition(new KnowledgeMilestoneConditionSample
            {
                domainId = domainId,
                subjectId = subjectId,
                trackId = trackId,
                milestoneId = milestoneId,
                pawn = pawn,
                context = context,
                conditionMet = false,
                interruptionReason = reason
            });

        public static bool Reset(string domainId, string subjectId, string trackId, string milestoneId, Pawn pawn = null,
            KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            KnowledgeMilestoneStateRecord record = component?.MilestoneV3(domainId, subjectId, trackId, milestoneId, pawn, context, false);
            if (record == null) return false;
            KnowledgeMilestoneTrackDef track = KnowledgeRegistry.Schema(domainId)?.milestoneTracks.FirstOrDefault(item => item.StableId == trackId);
            KnowledgeMilestoneDef milestone = track?.milestones?.FirstOrDefault(item => item?.StableId == milestoneId);
            if (milestone?.permanent == true && record.completed) return false;
            record.available = false;
            record.started = false;
            record.interrupted = false;
            record.completed = false;
            record.startTick = 0;
            record.completionTick = 0;
            record.progress = 0f;
            record.interruptionReason = null;
            record.completingPawn = null;
            component.TouchV3();
            Emit(KnowledgeMilestoneEventKind.Reset, ToState(record, domainId, subjectId, trackId, milestoneId, pawn, context), null);
            return true;
        }

        internal static void EvaluateTouched(IEnumerable<KnowledgeChange> changes)
        {
            foreach (KnowledgeChange change in changes ?? Enumerable.Empty<KnowledgeChange>())
            {
                KnowledgeSchema schema = KnowledgeRegistry.Schema(change.domainId);
                foreach (KnowledgeMilestoneTrackDef track in schema?.milestoneTracks ?? Array.Empty<KnowledgeMilestoneTrackDef>())
                    foreach (KnowledgeMilestoneDef milestone in track.milestones ?? new List<KnowledgeMilestoneDef>())
                        ReportCondition(new KnowledgeMilestoneConditionSample
                        {
                            domainId = change.domainId,
                            subjectId = change.subjectId,
                            trackId = track.StableId,
                            milestoneId = milestone.StableId,
                            pawn = change.pawn,
                            conditionMet = true,
                            value = change.newAmount
                        });
            }
        }

        private static bool PreviousMilestonesComplete(KnowledgeMilestoneTrackDef track, KnowledgeMilestoneDef milestone,
            KnowledgeMilestoneConditionSample sample, GameComponent_KnowledgeFramework component)
        {
            if (!track.ordered) return true;
            foreach (KnowledgeMilestoneDef previous in (track.milestones ?? new List<KnowledgeMilestoneDef>()).Where(item => item != null && item.order < milestone.order))
                if (!component.MilestoneV3(sample.domainId, sample.subjectId, track.StableId, previous.StableId, sample.pawn, sample.context, false)?.completed == true)
                    return false;
            return true;
        }

        private static KnowledgeMilestoneState ToState(KnowledgeMilestoneStateRecord record, string domainId, string subjectId,
            string trackId, string milestoneId, Pawn pawn, KnowledgeContextKey context) => new KnowledgeMilestoneState(domainId, subjectId, trackId,
                milestoneId, pawn, context, record?.available ?? false, record?.started ?? false, record?.interrupted ?? false,
                record?.completed ?? false, record?.startTick ?? 0, record?.completionTick ?? 0, record?.progress ?? 0f,
                record?.bestHistoricalValue ?? 0f, record?.interruptionReason, record?.completingPawn);

        private static void Emit(KnowledgeMilestoneEventKind kind, KnowledgeMilestoneState state, string reason)
        {
            Action<KnowledgeMilestoneChangedEvent> handlers = Changed;
            if (handlers == null) return;
            KnowledgeMilestoneChangedEvent value = new KnowledgeMilestoneChangedEvent(kind, state, reason);
            foreach (Action<KnowledgeMilestoneChangedEvent> handler in handlers.GetInvocationList())
                try { handler(value); }
                catch (Exception exception) { KnowledgeLog.ErrorOnce("milestone-event:" + handler.Method.Name, "A milestone subscriber failed.", exception); }
        }
    }

    public static class KnowledgeRelationService
    {
        private static readonly Dictionary<string, KnowledgeSubjectRelationTypeDef> Types = new Dictionary<string, KnowledgeSubjectRelationTypeDef>(StringComparer.Ordinal);

        public static bool RegisterType(KnowledgeSubjectRelationTypeDef definition, bool replace = false)
        {
            if (definition == null || definition.StableId.NullOrEmpty() || Types.ContainsKey(definition.StableId) && !replace) return false;
            Types[definition.StableId] = definition;
            return true;
        }

        public static KnowledgeSubjectRelationTypeDef Type(string id)
        {
            if (id.NullOrEmpty()) return null;
            if (Types.TryGetValue(id, out KnowledgeSubjectRelationTypeDef value)) return value;
            value = DefDatabase<KnowledgeSubjectRelationTypeDef>.AllDefsListForReading.FirstOrDefault(item => item.StableId == id);
            if (value != null) Types[id] = value;
            return value;
        }

        public static bool Add(KnowledgeSubjectRelation relation, bool addInverse = true)
        {
            if (!Validate(relation, out string error))
            {
                KnowledgeLog.ErrorOnce("relation-add:" + relation?.relationTypeId, "Structural relation rejected: " + error, new InvalidOperationException(error));
                return false;
            }
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            KnowledgeSubjectRelationTypeDef type = Type(relation.relationTypeId);
            if (type.parentage && WouldCycle(relation)) return false;
            string inverseTypeId = type.inverseTypeId.NullOrEmpty() ? type.StableId : type.inverseTypeId;
            if (addInverse && (type.symmetric || !type.inverseTypeId.NullOrEmpty()) && Type(inverseTypeId) == null) return false;
            component.AddRelationV3(relation);
            if (addInverse && (type.symmetric || !type.inverseTypeId.NullOrEmpty()))
            {
                KnowledgeSubjectRelation inverse = new KnowledgeSubjectRelation
                {
                    domainId = relation.toDomainId,
                    fromSubjectId = relation.toSubjectId,
                    toDomainId = relation.domainId,
                    toSubjectId = relation.fromSubjectId,
                    relationTypeId = inverseTypeId,
                    role = relation.role,
                    order = relation.order,
                    revealed = relation.revealed,
                    confidence = relation.confidence,
                    context = relation.context,
                    source = relation.source,
                    tick = relation.tick,
                    metadata = relation.metadata == null ? null : new Dictionary<string, string>(relation.metadata)
                };
                component.AddRelationV3(inverse);
            }
            return true;
        }

        public static bool Remove(KnowledgeSubjectRelation relation) => relation != null && GameComponent_KnowledgeFramework.Current?.RemoveRelationV3(
            relation.domainId, relation.fromSubjectId, relation.toDomainId, relation.toSubjectId, relation.relationTypeId, relation.context) == true;

        public static IReadOnlyList<KnowledgeSubjectRelation> Query(string domainId, string subjectId, bool outgoing = true, bool incoming = true,
            KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component == null) return Array.Empty<KnowledgeSubjectRelation>();
            return component.RelationRecordsV3(domainId, subjectId).Where(item =>
                (outgoing && item.fromSubjectId == subjectId || incoming && item.toSubjectId == subjectId) &&
                (context.IsEmpty || item.contextTypeId == context.typeId && item.contextId == context.stableId)).Select(item => item.ToRelation()).ToList();
        }

        public static IReadOnlyList<string> Validate()
        {
            List<string> issues = new List<string>();
            foreach (KnowledgeSubjectRelationTypeDef type in Types.Values)
            {
                if (type.parentage && type.inverseTypeId == type.StableId) issues.Add("relation inverse self-cycle: " + type.StableId);
                if (!type.inverseTypeId.NullOrEmpty() && Type(type.inverseTypeId) == null) issues.Add("relation inverse type missing: " + type.StableId);
            }
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component != null && KnowledgeGraphValidation.HasCycle(component.RelationRecordsV3(null).Where(item => Type(item.relationTypeId)?.parentage == true)
                .Select(item => new KeyValuePair<string, string>(item.domainId + ":" + item.fromSubjectId, item.toDomainId + ":" + item.toSubjectId))))
                issues.Add("structural relation cycle");
            return issues;
        }

        private static bool Validate(KnowledgeSubjectRelation relation, out string error)
        {
            error = null;
            if (relation == null || relation.domainId.NullOrEmpty() || relation.fromSubjectId.NullOrEmpty() || relation.toDomainId.NullOrEmpty() ||
                relation.toSubjectId.NullOrEmpty() || relation.relationTypeId.NullOrEmpty()) { error = "Relation IDs are required."; return false; }
            if (Type(relation.relationTypeId) == null) { error = "Unknown relation type."; return false; }
            if (!KnowledgeMath.IsFinite(relation.confidence) || relation.confidence < 0f || relation.confidence > 1f) { error = "Relation confidence is invalid."; return false; }
            if (relation.metadata != null && relation.metadata.Count > Math.Max(1, Type(relation.relationTypeId).metadataLimit)) { error = "Relation metadata exceeds the configured bound."; return false; }
            return true;
        }

        private static bool WouldCycle(KnowledgeSubjectRelation relation)
        {
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component == null) return true;
            List<KeyValuePair<string, string>> edges = component.RelationRecordsV3(null).Where(item => Type(item.relationTypeId)?.parentage == true)
                .Select(item => new KeyValuePair<string, string>(item.domainId + ":" + item.fromSubjectId, item.toDomainId + ":" + item.toSubjectId)).ToList();
            edges.Add(new KeyValuePair<string, string>(relation.domainId + ":" + relation.fromSubjectId, relation.toDomainId + ":" + relation.toSubjectId));
            return KnowledgeGraphValidation.HasCycle(edges);
        }
    }

    public static class KnowledgeSharedExpertiseService
    {
        private static readonly Dictionary<string, KnowledgeExpertiseNamespaceDef> Namespaces = new Dictionary<string, KnowledgeExpertiseNamespaceDef>(StringComparer.Ordinal);

        public static bool RegisterNamespace(KnowledgeExpertiseNamespaceDef definition, bool replace = false)
        {
            if (definition == null || definition.StableId.NullOrEmpty() || Namespaces.ContainsKey(definition.StableId) && !replace) return false;
            if (!KnowledgeMath.IsFinite(definition.adept) || !KnowledgeMath.IsFinite(definition.expert) || !KnowledgeMath.IsFinite(definition.master) ||
                !(new KnowledgeRankThresholds(definition.adept, definition.expert, definition.master)).IsValid) return false;
            Namespaces[definition.StableId] = definition;
            return true;
        }

        public static KnowledgeExpertiseNamespaceDef Namespace(string id)
        {
            if (id.NullOrEmpty()) return null;
            if (Namespaces.TryGetValue(id, out KnowledgeExpertiseNamespaceDef value)) return value;
            value = DefDatabase<KnowledgeExpertiseNamespaceDef>.AllDefsListForReading.FirstOrDefault(item => item.StableId == id);
            if (value != null) Namespaces[id] = value;
            return value;
        }

        internal static void Apply(string namespaceId, string domainId, string trackId, Pawn pawn, float amount, float weight)
        {
            if (pawn == null || amount <= 0f || !KnowledgeMath.IsFinite(weight) || weight <= 0f || Namespace(namespaceId) == null) return;
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            KnowledgeExpertiseNamespaceDef definition = Namespace(namespaceId);
            KnowledgeSharedExpertiseStateRecord record = component?.SharedExpertiseV3(namespaceId, domainId, trackId, pawn, false);
            if (record == null && component != null &&
                component.SharedExpertiseRecordsV3(namespaceId, pawn).Count() >= Math.Max(1, Math.Min(4096, definition.contributionLimit))) return;
            record = record ?? component?.SharedExpertiseV3(namespaceId, domainId, trackId, pawn, true);
            if (record == null) return;
            record.amount = Math.Min(100000000f, record.amount + amount * Math.Min(10f, weight));
            record.revision = component.TouchV3();
        }

        public static KnowledgeSharedExpertiseSnapshot Snapshot(string namespaceId, Pawn pawn)
        {
            KnowledgeExpertiseNamespaceDef definition = Namespace(namespaceId);
            if (definition == null) return new KnowledgeSharedExpertiseSnapshot(namespaceId, pawn, 0f, KnowledgeRank.Novice, 0f, null);
            List<KnowledgeSharedExpertiseStateRecord> records = GameComponent_KnowledgeFramework.Current?.SharedExpertiseRecordsV3(namespaceId, pawn).ToList() ?? new List<KnowledgeSharedExpertiseStateRecord>();
            float total = records.Sum(item => item.amount);
            KnowledgeRankThresholds thresholds = new KnowledgeRankThresholds(definition.adept, definition.expert, definition.master);
            return new KnowledgeSharedExpertiseSnapshot(namespaceId, pawn, total, thresholds.RankFor(total), thresholds.ProgressFor(total),
                records.Select(item => new KnowledgeSharedExpertiseContribution(item.domainId, item.trackId, item.amount)));
        }
    }
}
