using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace KnowledgeFramework
{
    internal class ColonyKnowledgeSaveRecord : IExposable
    {
        public string domainId;
        public string subjectId;
        public float experience;
        public Dictionary<string, int> eventCounts = new Dictionary<string, int>();

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref experience, "experience");
            Scribe_Collections.Look(ref eventCounts, "eventCounts", LookMode.Value, LookMode.Value);
            Normalize();
        }

        public void Normalize()
        {
            experience = Mathf.Max(0f, experience);
            if (eventCounts == null) eventCounts = new Dictionary<string, int>();
            foreach (string key in eventCounts.Keys.Where(key => key.NullOrEmpty() || eventCounts[key] <= 0).ToList())
                eventCounts.Remove(key);
        }
    }

    internal sealed class PawnKnowledgeSaveRecord : ColonyKnowledgeSaveRecord
    {
        public Pawn pawn;

        public override void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            base.ExposeData();
        }
    }

    internal sealed class PawnExpertiseSaveRecord : IExposable
    {
        public string domainId;
        public Pawn pawn;
        public float experience;

        public void ExposeData()
        {
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref experience, "experience");
            experience = Mathf.Max(0f, experience);
        }
    }

    public sealed class GameComponent_KnowledgeFramework : GameComponent
    {
        private List<ColonyKnowledgeSaveRecord> colonyKnowledge = new List<ColonyKnowledgeSaveRecord>();
        private List<PawnKnowledgeSaveRecord> pawnKnowledge = new List<PawnKnowledgeSaveRecord>();
        private List<PawnExpertiseSaveRecord> pawnExpertise = new List<PawnExpertiseSaveRecord>();
        private Dictionary<string, ColonyKnowledgeSaveRecord> colonyIndex = new Dictionary<string, ColonyKnowledgeSaveRecord>();
        private Dictionary<string, PawnKnowledgeSaveRecord> pawnIndex = new Dictionary<string, PawnKnowledgeSaveRecord>();
        private Dictionary<string, PawnExpertiseSaveRecord> expertiseIndex = new Dictionary<string, PawnExpertiseSaveRecord>();

        public static GameComponent_KnowledgeFramework Current => Verse.Current.Game?.GetComponent<GameComponent_KnowledgeFramework>();

        public GameComponent_KnowledgeFramework() { RebuildIndexes(); }
        public GameComponent_KnowledgeFramework(Game game) { RebuildIndexes(); }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref colonyKnowledge, "knowledgeFrameworkColony", LookMode.Deep);
            Scribe_Collections.Look(ref pawnKnowledge, "knowledgeFrameworkPawns", LookMode.Deep);
            Scribe_Collections.Look(ref pawnExpertise, "knowledgeFrameworkExpertise", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (colonyKnowledge == null) colonyKnowledge = new List<ColonyKnowledgeSaveRecord>();
                if (pawnKnowledge == null) pawnKnowledge = new List<PawnKnowledgeSaveRecord>();
                if (pawnExpertise == null) pawnExpertise = new List<PawnExpertiseSaveRecord>();
                RebuildIndexes();
                KnowledgeDomainRegistry.InvalidateDomain(null);
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            RebuildIndexes();
        }

        internal ColonyKnowledgeSaveRecord Colony(string domainId, string subjectId, bool create)
        {
            string key = SubjectKey(domainId, subjectId);
            if (key == null) return null;
            if (colonyIndex.TryGetValue(key, out ColonyKnowledgeSaveRecord record) || !create) return record;
            record = new ColonyKnowledgeSaveRecord { domainId = domainId, subjectId = subjectId };
            colonyKnowledge.Add(record);
            colonyIndex.Add(key, record);
            return record;
        }

        internal PawnKnowledgeSaveRecord Personal(string domainId, string subjectId, Pawn pawn, bool create)
        {
            string key = PawnSubjectKey(domainId, subjectId, pawn);
            if (key == null) return null;
            if (pawnIndex.TryGetValue(key, out PawnKnowledgeSaveRecord record) || !create) return record;
            record = new PawnKnowledgeSaveRecord { domainId = domainId, subjectId = subjectId, pawn = pawn };
            pawnKnowledge.Add(record);
            pawnIndex.Add(key, record);
            return record;
        }

        internal PawnExpertiseSaveRecord Expertise(string domainId, Pawn pawn, bool create)
        {
            string key = PawnDomainKey(domainId, pawn);
            if (key == null) return null;
            if (expertiseIndex.TryGetValue(key, out PawnExpertiseSaveRecord record) || !create) return record;
            record = new PawnExpertiseSaveRecord { domainId = domainId, pawn = pawn };
            pawnExpertise.Add(record);
            expertiseIndex.Add(key, record);
            return record;
        }

        internal IEnumerable<ColonyKnowledgeSaveRecord> ColonyRecords(string domainId) =>
            colonyKnowledge.Where(record => record != null && record.domainId == domainId);

        internal IEnumerable<PawnKnowledgeSaveRecord> PawnRecords(string domainId, Pawn pawn = null) =>
            pawnKnowledge.Where(record => record != null && record.domainId == domainId && (pawn == null || record.pawn == pawn));

        internal IEnumerable<PawnExpertiseSaveRecord> ExpertiseRecords(string domainId) =>
            pawnExpertise.Where(record => record != null && record.domainId == domainId);

        internal void Reset(string domainId, string subjectId, Pawn pawn, bool colony, bool expertise)
        {
            if (expertise)
            {
                PawnExpertiseSaveRecord record = Expertise(domainId, pawn, false);
                if (record != null) record.experience = 0f;
            }
            else if (colony)
            {
                ColonyKnowledgeSaveRecord record = Colony(domainId, subjectId, false);
                if (record != null) { record.experience = 0f; record.eventCounts.Clear(); }
            }
            else
            {
                PawnKnowledgeSaveRecord record = Personal(domainId, subjectId, pawn, false);
                if (record != null) { record.experience = 0f; record.eventCounts.Clear(); }
            }
        }

        private void RebuildIndexes()
        {
            colonyIndex = new Dictionary<string, ColonyKnowledgeSaveRecord>();
            pawnIndex = new Dictionary<string, PawnKnowledgeSaveRecord>();
            expertiseIndex = new Dictionary<string, PawnExpertiseSaveRecord>();
            if (colonyKnowledge == null) colonyKnowledge = new List<ColonyKnowledgeSaveRecord>();
            if (pawnKnowledge == null) pawnKnowledge = new List<PawnKnowledgeSaveRecord>();
            if (pawnExpertise == null) pawnExpertise = new List<PawnExpertiseSaveRecord>();
            MergeDuplicates(colonyKnowledge, colonyIndex, record => SubjectKey(record?.domainId, record?.subjectId));
            MergeDuplicates(pawnKnowledge, pawnIndex, record => PawnSubjectKey(record?.domainId, record?.subjectId, record?.pawn));
            foreach (PawnExpertiseSaveRecord record in pawnExpertise.Where(record => record != null))
            {
                string key = PawnDomainKey(record.domainId, record.pawn);
                if (key == null) continue;
                if (expertiseIndex.TryGetValue(key, out PawnExpertiseSaveRecord existing)) existing.experience = Mathf.Max(existing.experience, record.experience);
                else expertiseIndex.Add(key, record);
            }
        }

        private static void MergeDuplicates<T>(IEnumerable<T> source, IDictionary<string, T> index, Func<T, string> keyFor)
            where T : ColonyKnowledgeSaveRecord
        {
            foreach (T record in source.Where(record => record != null))
            {
                record.Normalize();
                string key = keyFor(record);
                if (key == null) continue;
                if (index.TryGetValue(key, out T existing))
                {
                    existing.experience = Mathf.Max(existing.experience, record.experience);
                    foreach (KeyValuePair<string, int> count in record.eventCounts)
                        existing.eventCounts[count.Key] = Mathf.Max(existing.eventCounts.TryGetValue(count.Key, out int old) ? old : 0, count.Value);
                }
                else index.Add(key, record);
            }
        }

        private static string SubjectKey(string domainId, string subjectId) =>
            domainId.NullOrEmpty() || subjectId.NullOrEmpty() ? null : domainId + "\n" + subjectId;

        private static string PawnSubjectKey(string domainId, string subjectId, Pawn pawn) =>
            pawn == null ? null : SubjectKey(domainId, subjectId) + "\n" + pawn.thingIDNumber;

        private static string PawnDomainKey(string domainId, Pawn pawn) =>
            domainId.NullOrEmpty() || pawn == null ? null : domainId + "\n" + pawn.thingIDNumber;
    }

    public static class KnowledgeService
    {
        private static readonly KnowledgeSnapshot EmptyColony = new KnowledgeSnapshot(null, null, null, true, 0f,
            KnowledgeRank.Novice, 0f, null);
        private static readonly ExpertiseSnapshot EmptyExpertise = new ExpertiseSnapshot(null, null, 0f, KnowledgeRank.Novice, 0f);
        public static event Action<KnowledgeChangedEvent> KnowledgeChanged;

        public static KnowledgeSnapshot GetColonyKnowledge(string domainId, string subjectId)
        {
            KnowledgeDomainDefinition domain = KnowledgeDomainRegistry.Domain(domainId);
            ColonyKnowledgeSaveRecord record = GameComponent_KnowledgeFramework.Current?.Colony(domainId, subjectId, false);
            return record == null ? new KnowledgeSnapshot(domainId, subjectId, null, true, 0f, KnowledgeRank.Novice, 0f, null)
                : Snapshot(record, domain, null, true);
        }

        public static KnowledgeSnapshot GetPawnKnowledge(string domainId, string subjectId, Pawn pawn)
        {
            KnowledgeDomainDefinition domain = KnowledgeDomainRegistry.Domain(domainId);
            PawnKnowledgeSaveRecord record = GameComponent_KnowledgeFramework.Current?.Personal(domainId, subjectId, pawn, false);
            return record == null ? new KnowledgeSnapshot(domainId, subjectId, pawn, false, 0f, KnowledgeRank.Novice, 0f, null)
                : Snapshot(record, domain, pawn, false);
        }

        public static ExpertiseSnapshot GetPawnExpertise(string domainId, Pawn pawn)
        {
            KnowledgeDomainDefinition domain = KnowledgeDomainRegistry.Domain(domainId);
            if (domain?.expertiseEnabled != true) return new ExpertiseSnapshot(domainId, pawn, 0f, KnowledgeRank.Novice, 0f);
            PawnExpertiseSaveRecord record = GameComponent_KnowledgeFramework.Current?.Expertise(domainId, pawn, false);
            float experience = record?.experience ?? 0f;
            return new ExpertiseSnapshot(domainId, pawn, experience, domain.expertiseRanks.RankFor(experience),
                domain.expertiseRanks.ProgressFor(experience));
        }

        public static float GetColonyKnowledgeExperience(string domainId, string subjectId) =>
            GameComponent_KnowledgeFramework.Current?.Colony(domainId, subjectId, false)?.experience ?? 0f;

        public static float GetPawnKnowledgeExperience(string domainId, string subjectId, Pawn pawn) =>
            GameComponent_KnowledgeFramework.Current?.Personal(domainId, subjectId, pawn, false)?.experience ?? 0f;

        public static float GetPawnExpertiseExperience(string domainId, Pawn pawn) =>
            KnowledgeDomainRegistry.Domain(domainId)?.expertiseEnabled == true
                ? GameComponent_KnowledgeFramework.Current?.Expertise(domainId, pawn, false)?.experience ?? 0f : 0f;

        public static KnowledgeRank GetColonyKnowledgeRank(string domainId, string subjectId)
        {
            KnowledgeDomainDefinition domain = KnowledgeDomainRegistry.Domain(domainId);
            return domain?.knowledgeRanks.RankFor(GetColonyKnowledgeExperience(domainId, subjectId)) ?? KnowledgeRank.Novice;
        }

        public static KnowledgeRank GetPawnKnowledgeRank(string domainId, string subjectId, Pawn pawn)
        {
            KnowledgeDomainDefinition domain = KnowledgeDomainRegistry.Domain(domainId);
            return domain?.knowledgeRanks.RankFor(GetPawnKnowledgeExperience(domainId, subjectId, pawn)) ?? KnowledgeRank.Novice;
        }

        public static KnowledgeRank GetPawnExpertiseRank(string domainId, Pawn pawn)
        {
            KnowledgeDomainDefinition domain = KnowledgeDomainRegistry.Domain(domainId);
            return domain?.expertiseEnabled == true
                ? domain.expertiseRanks.RankFor(GetPawnExpertiseExperience(domainId, pawn)) : KnowledgeRank.Novice;
        }

        public static int GetEventCount(string domainId, string subjectId, string reasonId, Pawn pawn = null, bool colony = false)
        {
            if (reasonId.NullOrEmpty()) return 0;
            ColonyKnowledgeSaveRecord record = colony
                ? GameComponent_KnowledgeFramework.Current?.Colony(domainId, subjectId, false)
                : GameComponent_KnowledgeFramework.Current?.Personal(domainId, subjectId, pawn, false);
            return record != null && record.eventCounts.TryGetValue(reasonId, out int count) ? count : 0;
        }

        public static IReadOnlyList<KnowledgeSnapshot> ColonyKnowledge(string domainId) =>
            GameComponent_KnowledgeFramework.Current?.ColonyRecords(domainId)
                .Select(record => Snapshot(record, KnowledgeDomainRegistry.Domain(domainId), null, true)).ToList()
            ?? (IReadOnlyList<KnowledgeSnapshot>)Array.Empty<KnowledgeSnapshot>();

        public static IReadOnlyList<KnowledgeSnapshot> PawnKnowledge(string domainId, Pawn pawn) =>
            GameComponent_KnowledgeFramework.Current?.PawnRecords(domainId, pawn)
                .Select(record => Snapshot(record, KnowledgeDomainRegistry.Domain(domainId), pawn, false)).ToList()
            ?? (IReadOnlyList<KnowledgeSnapshot>)Array.Empty<KnowledgeSnapshot>();

        public static bool Award(KnowledgeAward award)
        {
            KnowledgeDomainDefinition domain = KnowledgeDomainRegistry.Domain(award?.domainId);
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (award == null || domain == null || component == null || award.subjectId.NullOrEmpty()) return false;
            if (award.pawnKnowledge > 0f && award.pawn == null) return false;

            KnowledgeSnapshot oldPawn = award.pawn == null ? null : GetPawnKnowledge(award.domainId, award.subjectId, award.pawn);
            KnowledgeSnapshot oldColony = GetColonyKnowledge(award.domainId, award.subjectId);
            ExpertiseSnapshot oldExpertise = award.pawn == null ? null : GetPawnExpertise(award.domainId, award.pawn);
            if (award.pawnKnowledge > 0f)
            {
                PawnKnowledgeSaveRecord personal = component.Personal(award.domainId, award.subjectId, award.pawn, true);
                personal.experience += award.pawnKnowledge;
                Increment(personal.eventCounts, award.reasonId);
            }
            if (award.colonyKnowledge > 0f)
            {
                ColonyKnowledgeSaveRecord colony = component.Colony(award.domainId, award.subjectId, true);
                colony.experience += award.colonyKnowledge;
                Increment(colony.eventCounts, award.reasonId);
            }
            if (award.expertise > 0f && award.pawn != null && domain.expertiseEnabled)
                component.Expertise(award.domainId, award.pawn, true).experience += award.expertise;

            KnowledgeSnapshot newPawn = award.pawn == null ? null : GetPawnKnowledge(award.domainId, award.subjectId, award.pawn);
            KnowledgeSnapshot newColony = GetColonyKnowledge(award.domainId, award.subjectId);
            ExpertiseSnapshot newExpertise = award.pawn == null ? null : GetPawnExpertise(award.domainId, award.pawn);
            KnowledgeProviderRegistry.Invalidate(award.pawn);
            KnowledgeChanged?.Invoke(new KnowledgeChangedEvent(award, oldPawn, newPawn, oldColony, newColony,
                oldExpertise, newExpertise));
            if (award.notifyRankChange) NotifyRankChanges(domain, award, oldPawn, newPawn, oldExpertise, newExpertise);
            return true;
        }

        public static bool ImportMinimum(string domainId, string subjectId, Pawn pawn, float pawnExperience,
            float colonyExperience, float expertiseExperience, IDictionary<string, int> eventCounts = null)
        {
            KnowledgeDomainDefinition domain = KnowledgeDomainRegistry.Domain(domainId);
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (domain == null || component == null || subjectId.NullOrEmpty()) return false;
            if (pawn != null)
            {
                PawnKnowledgeSaveRecord personal = component.Personal(domainId, subjectId, pawn, true);
                personal.experience = Mathf.Max(personal.experience, pawnExperience);
                MergeCounts(personal.eventCounts, eventCounts);
                if (domain.expertiseEnabled)
                {
                    PawnExpertiseSaveRecord expertise = component.Expertise(domainId, pawn, true);
                    expertise.experience = Mathf.Max(expertise.experience, expertiseExperience);
                }
            }
            ColonyKnowledgeSaveRecord colony = component.Colony(domainId, subjectId, true);
            colony.experience = Mathf.Max(colony.experience, colonyExperience);
            MergeCounts(colony.eventCounts, eventCounts);
            KnowledgeProviderRegistry.Invalidate(pawn);
            return true;
        }

        public static bool MeetsReveal(string domainId, string subjectId, string revealId, Pawn pawn = null, bool colony = false)
        {
            KnowledgeDomainDefinition domain = KnowledgeDomainRegistry.Domain(domainId);
            if (domain == null || revealId.NullOrEmpty() || !domain.revealThresholds.TryGetValue(revealId, out float threshold)) return false;
            return (colony ? GetColonyKnowledge(domainId, subjectId) : GetPawnKnowledge(domainId, subjectId, pawn)).experience >= threshold;
        }

        public static float RevealThreshold(string domainId, string revealId)
        {
            KnowledgeDomainDefinition domain = KnowledgeDomainRegistry.Domain(domainId);
            return domain != null && revealId != null && domain.revealThresholds.TryGetValue(revealId, out float threshold)
                ? threshold : float.PositiveInfinity;
        }

        public static float ApplyEffects(string domainId, string subjectId, string effectId, Pawn pawn, float value)
        {
            KnowledgeDomainDefinition domain = KnowledgeDomainRegistry.Domain(domainId);
            if (domain == null || effectId.NullOrEmpty()) return value;
            KnowledgeEffectContext context = new KnowledgeEffectContext(domain,
                KnowledgeDomainRegistry.ResolveSubject(domainId, subjectId), pawn,
                GetColonyKnowledgeExperience(domainId, subjectId), GetPawnKnowledgeExperience(domainId, subjectId, pawn),
                GetPawnExpertiseExperience(domainId, pawn));
            IReadOnlyList<IKnowledgeEffectProvider> providers = KnowledgeDomainRegistry.Effects(domainId);
            for (int i = 0; i < providers.Count; i++)
            {
                try { value = providers[i].Apply(effectId, context, value); }
                catch (Exception exception)
                {
                    Log.ErrorOnce("[Knowledge Framework] Effect provider '" + providers[i].Id + "' failed: " + exception.Message,
                        GenText.StableStringHash(domainId + "/" + providers[i].Id));
                }
            }
            return value;
        }

        public static void Reset(string domainId, string subjectId, Pawn pawn = null, bool colony = false, bool expertise = false)
        {
            GameComponent_KnowledgeFramework.Current?.Reset(domainId, subjectId, pawn, colony, expertise);
            KnowledgeProviderRegistry.Invalidate(pawn);
        }

        public static IReadOnlyList<string> Validate()
        {
            List<string> issues = KnowledgeDomainRegistry.RegistrationDiagnostics.ToList();
            foreach (KnowledgeDomainDefinition domain in KnowledgeDomainRegistry.AllDomains)
            {
                if (!domain.knowledgeRanks.IsValid) issues.Add("Invalid knowledge thresholds: " + domain.id);
                if (domain.expertiseEnabled && !domain.expertiseRanks.IsValid) issues.Add("Invalid expertise thresholds: " + domain.id);
                foreach (KeyValuePair<string, float> reveal in domain.revealThresholds)
                    if (reveal.Key.NullOrEmpty() || reveal.Value < 0f || float.IsNaN(reveal.Value))
                        issues.Add("Invalid reveal threshold: " + domain.id + "/" + reveal.Key);
                foreach (KnowledgeSubjectDefinition subject in KnowledgeDomainRegistry.Subjects(domain.id))
                    if (subject.sourceDef == null && subject.iconPath.NullOrEmpty())
                        issues.Add("Missing subject icon: " + domain.id + "/" + subject.id);
            }
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component != null)
            {
                foreach (KnowledgeDomainDefinition domain in KnowledgeDomainRegistry.AllDomains)
                {
                    foreach (KnowledgeSnapshot record in ColonyKnowledge(domain.id))
                        if (KnowledgeDomainRegistry.ResolveSubject(domain.id, record.subjectId) == null)
                            issues.Add("Orphaned colony subject: " + domain.id + "/" + record.subjectId);
                    foreach (PawnKnowledgeSaveRecord record in component.PawnRecords(domain.id))
                        if (record.pawn == null || KnowledgeDomainRegistry.ResolveSubject(domain.id, record.subjectId) == null)
                            issues.Add("Orphaned pawn subject: " + domain.id + "/" + record.subjectId);
                }
            }
            return issues.Distinct().ToList();
        }

        private static KnowledgeSnapshot Snapshot(ColonyKnowledgeSaveRecord record, KnowledgeDomainDefinition domain,
            Pawn pawn, bool colony)
        {
            KnowledgeRankThresholds thresholds = domain?.knowledgeRanks ?? KnowledgeRankThresholds.Default;
            return new KnowledgeSnapshot(record.domainId, record.subjectId, pawn, colony, record.experience,
                thresholds.RankFor(record.experience), thresholds.ProgressFor(record.experience), record.eventCounts);
        }

        private static void Increment(IDictionary<string, int> counts, string reasonId)
        {
            if (reasonId.NullOrEmpty()) return;
            counts[reasonId] = (counts.TryGetValue(reasonId, out int value) ? value : 0) + 1;
        }

        private static void MergeCounts(IDictionary<string, int> target, IDictionary<string, int> source)
        {
            if (source == null) return;
            foreach (KeyValuePair<string, int> count in source)
                if (!count.Key.NullOrEmpty() && count.Value > 0)
                    target[count.Key] = Mathf.Max(target.TryGetValue(count.Key, out int old) ? old : 0, count.Value);
        }

        private static void NotifyRankChanges(KnowledgeDomainDefinition domain, KnowledgeAward award,
            KnowledgeSnapshot oldPawn, KnowledgeSnapshot newPawn, ExpertiseSnapshot oldExpertise,
            ExpertiseSnapshot newExpertise)
        {
            if (award.pawn == null) return;
            KnowledgeSubjectDefinition subject = KnowledgeDomainRegistry.ResolveSubject(award.domainId, award.subjectId);
            if (oldPawn != null && newPawn.rank > oldPawn.rank)
                Messages.Message(award.pawn.LabelShortCap + " advanced to " + newPawn.rank + " knowledge of "
                    + (subject?.label ?? award.subjectId) + ".", award.pawn, MessageTypeDefOf.PositiveEvent, false);
            if (domain.expertiseEnabled && oldExpertise != null && newExpertise.rank > oldExpertise.rank)
                Messages.Message(award.pawn.LabelShortCap + " advanced to " + newExpertise.rank + " " + domain.label
                    + " expertise.", award.pawn, MessageTypeDefOf.PositiveEvent, false);
        }
    }

    public static class KnowledgeFrameworkDebugActions
    {
        private const string Category = "Knowledge Framework";

        [DebugAction(Category, "List domains and subjects", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static void ListDomains()
        {
            List<string> lines = new List<string>();
            foreach (KnowledgeDomainDefinition domain in KnowledgeDomainRegistry.AllDomains)
            {
                List<KnowledgeSubjectDefinition> subjects = KnowledgeDomainRegistry.Subjects(domain.id).ToList();
                lines.Add(domain.id + " (expertise=" + domain.expertiseEnabled + ", subjects=" + subjects.Count + ")");
                lines.AddRange(subjects.Select(subject => "  " + subject.id + " - " + subject.label));
            }
            Log.Message("[Knowledge Framework]\n" + (lines.Count == 0 ? "No registered domains." : string.Join("\n", lines)));
        }

        [DebugAction(Category, "Inspect selected pawn", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void InspectSelectedPawn()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null) { Messages.Message("Select a pawn first.", MessageTypeDefOf.RejectInput, false); return; }
            List<string> lines = new List<string>();
            foreach (KnowledgeDomainDefinition domain in KnowledgeDomainRegistry.AllDomains)
            {
                ExpertiseSnapshot expertise = KnowledgeService.GetPawnExpertise(domain.id, pawn);
                lines.Add(domain.id + " expertise=" + expertise.experience.ToString("0.0") + " (" + expertise.rank + ")");
                lines.AddRange(KnowledgeService.PawnKnowledge(domain.id, pawn)
                    .Select(record => "  " + record.subjectId + "=" + record.experience.ToString("0.0") + " (" + record.rank + ")"));
            }
            Log.Message("[Knowledge Framework] " + pawn.LabelShortCap + "\n" + string.Join("\n", lines));
        }

        [DebugAction(Category, "Award selected pawn 100 knowledge", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AwardSelectedPawn()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            KnowledgeDomainDefinition domain = KnowledgeDomainRegistry.AllDomains.FirstOrDefault();
            KnowledgeSubjectDefinition subject = domain == null ? null : KnowledgeDomainRegistry.Subjects(domain.id).FirstOrDefault();
            if (pawn == null || subject == null) { Messages.Message("Select a pawn and register at least one subject.", MessageTypeDefOf.RejectInput, false); return; }
            KnowledgeService.Award(new KnowledgeAward { domainId = domain.id, subjectId = subject.id, pawn = pawn,
                pawnKnowledge = 100f, colonyKnowledge = 100f, expertise = domain.expertiseEnabled ? 100f : 0f,
                reasonId = "debug", source = "KnowledgeFrameworkDebug" });
        }

        [DebugAction(Category, "Reset selected pawn knowledge", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ResetSelectedPawn()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null) { Messages.Message("Select a pawn first.", MessageTypeDefOf.RejectInput, false); return; }
            foreach (KnowledgeDomainDefinition domain in KnowledgeDomainRegistry.AllDomains)
            {
                foreach (KnowledgeSnapshot record in KnowledgeService.PawnKnowledge(domain.id, pawn))
                    KnowledgeService.Reset(domain.id, record.subjectId, pawn);
                if (domain.expertiseEnabled) KnowledgeService.Reset(domain.id, null, pawn, expertise: true);
            }
        }

        [DebugAction(Category, "Validate registrations and saves", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static void Validate()
        {
            IReadOnlyList<string> issues = KnowledgeService.Validate();
            Log.Message("[Knowledge Framework] Validation: " + (issues.Count == 0 ? "valid" : string.Join("\n", issues)));
        }
    }
}
