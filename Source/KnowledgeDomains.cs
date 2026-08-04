using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace KnowledgeFramework
{
    public static class KnowledgeFrameworkApi
    {
        public const int LegacyApiVersion = 1;
        public const int V2ApiVersion = 2;
        public const int ApiVersion = 3;
        public const int SecondGenerationApiVersion = 2;
        public const int ThirdGenerationApiVersion = 3;
        public const string DomainsCapability = "domains";
        public const string ColonyKnowledgeCapability = "colony-knowledge";
        public const string PawnKnowledgeCapability = "pawn-knowledge";
        public const string ExpertiseCapability = "expertise";
        public const string EffectsCapability = "effects";
        public const string RevealCapability = "reveals";
        public const string UiCapability = "domain-ui";
        public const string EvidenceCapability = "evidence-transactions";
        public const string FacetsCapability = "facets";
        public const string ConfidenceCapability = "confidence";
        public const string DiscoveryCapability = "discovery-stages";
        public const string InsightsCapability = "insights";
        public const string RelationshipsCapability = "relationships";
        public const string TypedEffectsCapability = "typed-effects";
        public const string TransmissionCapability = "transmission";
        public const string ClaimsCapability = "claims";
        public const string TypedMeasurementsCapability = "typed-measurements";
        public const string ArchetypesCapability = "subject-archetypes";
        public const string RequirementStagesCapability = "requirement-stages";
        public const string ObservationRecipesCapability = "observation-recipes";
        public const string ContextsCapability = "contextual-knowledge";
        public const string MilestonesCapability = "milestones";
        public const string StructuralRelationsCapability = "structural-relations";
        public const string SubjectLifecycleCapability = "subject-lifecycle";
        public const string AccrualPoliciesCapability = "accrual-policies";
        public const string StalenessCapability = "claim-staleness";
        public const string FilteredTransmissionCapability = "filtered-transmission";
        public const string WitnessLearningCapability = "witness-learning";
        public const string RichEffectRequirementsCapability = "rich-effect-requirements";
        public const string CrossDomainCapability = "cross-domain-requirements";
        public const string SharedExpertiseCapability = "shared-expertise";
        public const string StructuredComparisonCapability = "structured-comparison";

        public static bool Supports(int minimumApiVersion, string capability = null)
        {
            if (minimumApiVersion > ApiVersion) return false;
            return capability.NullOrEmpty() || capability == DomainsCapability || capability == ColonyKnowledgeCapability
                || capability == PawnKnowledgeCapability || capability == ExpertiseCapability || capability == EffectsCapability
                || capability == RevealCapability || capability == UiCapability || capability == EvidenceCapability
                || capability == FacetsCapability || capability == ConfidenceCapability || capability == DiscoveryCapability
                || capability == InsightsCapability || capability == RelationshipsCapability || capability == TypedEffectsCapability
                || capability == TransmissionCapability || capability == ClaimsCapability || capability == TypedMeasurementsCapability
                || capability == ArchetypesCapability || capability == RequirementStagesCapability || capability == ObservationRecipesCapability
                || capability == ContextsCapability || capability == MilestonesCapability || capability == StructuralRelationsCapability
                || capability == SubjectLifecycleCapability || capability == AccrualPoliciesCapability || capability == StalenessCapability
                || capability == FilteredTransmissionCapability || capability == WitnessLearningCapability || capability == RichEffectRequirementsCapability
                || capability == CrossDomainCapability || capability == SharedExpertiseCapability || capability == StructuredComparisonCapability;
        }

        public static int CapabilityVersion(string capability)
        {
            if (capability.NullOrEmpty()) return ApiVersion;
            switch (capability)
            {
                case ClaimsCapability:
                case TypedMeasurementsCapability:
                case ArchetypesCapability:
                case RequirementStagesCapability:
                case ObservationRecipesCapability:
                case ContextsCapability:
                case MilestonesCapability:
                case StructuralRelationsCapability:
                case SubjectLifecycleCapability:
                case AccrualPoliciesCapability:
                case StalenessCapability:
                case FilteredTransmissionCapability:
                case WitnessLearningCapability:
                case RichEffectRequirementsCapability:
                case CrossDomainCapability:
                case SharedExpertiseCapability:
                case StructuredComparisonCapability:
                    return ThirdGenerationApiVersion;
                default:
                    return SecondGenerationApiVersion;
            }
        }
    }

    public sealed class KnowledgeRankThresholds
    {
        public static readonly KnowledgeRankThresholds Default = new KnowledgeRankThresholds(100f, 300f, 700f);
        public readonly float adept;
        public readonly float expert;
        public readonly float master;

        public KnowledgeRankThresholds(float adept, float expert, float master)
        {
            this.adept = Mathf.Max(0f, adept);
            this.expert = Mathf.Max(this.adept, expert);
            this.master = Mathf.Max(this.expert, master);
        }

        public KnowledgeRank RankFor(float experience) => KnowledgeRanks.ForExperience(experience, adept, expert, master);
        public float ProgressFor(float experience) => KnowledgeRanks.Progress(experience, adept, expert, master);
        public bool IsValid => adept > 0f && expert > adept && master > expert;
    }

    public sealed class KnowledgeSubjectDefinition
    {
        public readonly string id;
        public readonly string label;
        public readonly string description;
        public readonly Def sourceDef;
        public readonly string iconPath;
        public readonly int sortOrder;

        public KnowledgeSubjectDefinition(string id, string label, string description = null, Def sourceDef = null,
            string iconPath = null, int sortOrder = 0)
        {
            this.id = id;
            this.label = label ?? id;
            this.description = description ?? string.Empty;
            this.sourceDef = sourceDef;
            this.iconPath = iconPath;
            this.sortOrder = sortOrder;
        }
    }

    public interface IKnowledgeEffectProvider
    {
        string Id { get; }
        string DomainId { get; }
        float Apply(string effectId, KnowledgeEffectContext context, float value);
    }

    public interface IKnowledgeUiProvider
    {
        string DomainId { get; }
        KnowledgeEntry BioEntry(Pawn pawn);
        KnowledgeMenuModel Menu(Pawn pawn, bool colony);
    }

    public sealed class KnowledgeDomainDefinition
    {
        public readonly string id;
        public readonly string label;
        public readonly string description;
        public readonly bool expertiseEnabled;
        public readonly KnowledgeStageAggregationMode stageAggregationMode;
        public readonly int sortOrder;
        public readonly KnowledgeRankThresholds knowledgeRanks;
        public readonly KnowledgeRankThresholds expertiseRanks;
        public readonly Func<string, KnowledgeSubjectDefinition> subjectResolver;
        public readonly Func<IEnumerable<KnowledgeSubjectDefinition>> subjectSource;
        public readonly IReadOnlyDictionary<string, float> revealThresholds;

        public KnowledgeDomainDefinition(string id, string label, string description = null, bool expertiseEnabled = true,
            KnowledgeRankThresholds knowledgeRanks = null, KnowledgeRankThresholds expertiseRanks = null,
            Func<string, KnowledgeSubjectDefinition> subjectResolver = null,
            Func<IEnumerable<KnowledgeSubjectDefinition>> subjectSource = null,
            IDictionary<string, float> revealThresholds = null, int sortOrder = 0,
            KnowledgeStageAggregationMode stageAggregationMode = KnowledgeStageAggregationMode.LegacySumMax)
        {
            this.id = id;
            this.label = label ?? id;
            this.description = description ?? string.Empty;
            this.expertiseEnabled = expertiseEnabled;
            this.stageAggregationMode = stageAggregationMode;
            this.knowledgeRanks = knowledgeRanks ?? KnowledgeRankThresholds.Default;
            this.expertiseRanks = expertiseRanks ?? KnowledgeRankThresholds.Default;
            this.subjectResolver = subjectResolver;
            this.subjectSource = subjectSource;
            this.revealThresholds = new Dictionary<string, float>(revealThresholds ?? new Dictionary<string, float>());
            this.sortOrder = sortOrder;
        }
    }

    public readonly struct KnowledgeEffectContext
    {
        public readonly KnowledgeDomainDefinition domain;
        public readonly KnowledgeSubjectDefinition subject;
        public readonly Pawn pawn;
        public readonly float colonyExperience;
        public readonly float pawnExperience;
        public readonly float expertiseExperience;
        public readonly KnowledgeRank colonyRank;
        public readonly KnowledgeRank pawnRank;
        public readonly KnowledgeRank expertiseRank;

        internal KnowledgeEffectContext(KnowledgeDomainDefinition domain, KnowledgeSubjectDefinition subject, Pawn pawn,
            float colonyExperience, float pawnExperience, float expertiseExperience)
        {
            this.domain = domain;
            this.subject = subject;
            this.pawn = pawn;
            this.colonyExperience = colonyExperience;
            this.pawnExperience = pawnExperience;
            this.expertiseExperience = expertiseExperience;
            colonyRank = domain?.knowledgeRanks.RankFor(colonyExperience) ?? KnowledgeRank.Novice;
            pawnRank = domain?.knowledgeRanks.RankFor(pawnExperience) ?? KnowledgeRank.Novice;
            expertiseRank = domain?.expertiseEnabled == true
                ? domain.expertiseRanks.RankFor(expertiseExperience) : KnowledgeRank.Novice;
        }
    }

    public sealed class KnowledgeSnapshot
    {
        public readonly string domainId;
        public readonly string subjectId;
        public readonly Pawn pawn;
        public readonly bool colony;
        public readonly float experience;
        public readonly KnowledgeRank rank;
        public readonly float progress;
        public readonly IReadOnlyDictionary<string, int> eventCounts;

        internal KnowledgeSnapshot(string domainId, string subjectId, Pawn pawn, bool colony, float experience,
            KnowledgeRank rank, float progress, IDictionary<string, int> eventCounts)
        {
            this.domainId = domainId;
            this.subjectId = subjectId;
            this.pawn = pawn;
            this.colony = colony;
            this.experience = experience;
            this.rank = rank;
            this.progress = progress;
            this.eventCounts = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(
                new Dictionary<string, int>(eventCounts ?? new Dictionary<string, int>()));
        }

        public int EventCount(string reasonId) => reasonId != null && eventCounts.TryGetValue(reasonId, out int count) ? count : 0;
    }

    public sealed class ExpertiseSnapshot
    {
        public readonly string domainId;
        public readonly Pawn pawn;
        public readonly float experience;
        public readonly KnowledgeRank rank;
        public readonly float progress;

        internal ExpertiseSnapshot(string domainId, Pawn pawn, float experience, KnowledgeRank rank, float progress)
        {
            this.domainId = domainId;
            this.pawn = pawn;
            this.experience = experience;
            this.rank = rank;
            this.progress = progress;
        }
    }

    public sealed class KnowledgeAward
    {
        public string domainId;
        public string subjectId;
        public Pawn pawn;
        public float pawnKnowledge;
        public float colonyKnowledge;
        public float expertise;
        public string reasonId;
        public string source;
        public bool notifyRankChange = true;
    }

    public sealed class KnowledgeChangedEvent
    {
        public readonly string domainId;
        public readonly string subjectId;
        public readonly Pawn pawn;
        public readonly string reasonId;
        public readonly string source;
        public readonly KnowledgeSnapshot oldPawnKnowledge;
        public readonly KnowledgeSnapshot newPawnKnowledge;
        public readonly KnowledgeSnapshot oldColonyKnowledge;
        public readonly KnowledgeSnapshot newColonyKnowledge;
        public readonly ExpertiseSnapshot oldExpertise;
        public readonly ExpertiseSnapshot newExpertise;

        internal KnowledgeChangedEvent(KnowledgeAward award, KnowledgeSnapshot oldPawn, KnowledgeSnapshot newPawn,
            KnowledgeSnapshot oldColony, KnowledgeSnapshot newColony, ExpertiseSnapshot oldExpertise,
            ExpertiseSnapshot newExpertise)
        {
            domainId = award.domainId;
            subjectId = award.subjectId;
            pawn = award.pawn;
            reasonId = award.reasonId;
            source = award.source;
            oldPawnKnowledge = oldPawn;
            newPawnKnowledge = newPawn;
            oldColonyKnowledge = oldColony;
            newColonyKnowledge = newColony;
            this.oldExpertise = oldExpertise;
            this.newExpertise = newExpertise;
        }
    }

    public static class KnowledgeDomainRegistry
    {
        private static readonly Dictionary<string, KnowledgeDomainDefinition> Domains = new Dictionary<string, KnowledgeDomainDefinition>();
        private static readonly Dictionary<string, IKnowledgeUiProvider> UiProviders = new Dictionary<string, IKnowledgeUiProvider>();
        private static readonly Dictionary<string, List<IKnowledgeEffectProvider>> EffectProviders = new Dictionary<string, List<IKnowledgeEffectProvider>>();
        private static readonly Dictionary<string, KnowledgeSubjectDefinition> SubjectCache = new Dictionary<string, KnowledgeSubjectDefinition>();
        private static readonly List<string> Diagnostics = new List<string>();
        private static int revision;

        public static int Revision => revision;
        public static IReadOnlyList<string> RegistrationDiagnostics => new System.Collections.ObjectModel.ReadOnlyCollection<string>(Diagnostics.ToList());
        public static IEnumerable<KnowledgeDomainDefinition> AllDomains => Domains.Values.OrderBy(value => value.sortOrder).ThenBy(value => value.id);

        public static bool RegisterDomain(KnowledgeDomainDefinition domain)
        {
            if (domain == null || domain.id.NullOrEmpty())
            {
                Diagnostics.Add("Invalid domain registration: missing stable ID.");
                return false;
            }
            if (Domains.ContainsKey(domain.id)) Diagnostics.Add("Duplicate domain registration replaced: " + domain.id);
            Domains[domain.id] = domain;
            KnowledgeExpertiseTrackDef track = null;
            if (domain.expertiseEnabled)
            {
                track = new KnowledgeExpertiseTrackDef
                {
                    defName = KnowledgeSchema.DefaultExpertiseTrackId,
                    label = "Expertise",
                    adept = domain.expertiseRanks.adept,
                    expert = domain.expertiseRanks.expert,
                    master = domain.expertiseRanks.master
                };
            }
            bool v2Registered = KnowledgeRegistry.RegisterDomain(new KnowledgeDomainRegistration
            {
                id = domain.id,
                label = domain.label,
                description = domain.description,
                stageAggregationMode = domain.stageAggregationMode,
                sharingModel = KnowledgeSharingModel.Custom,
                sortOrder = domain.sortOrder,
                expertiseTracks = track == null ? Array.Empty<KnowledgeExpertiseTrackDef>() : new[] { track },
                subjectResolver = subjectId =>
                {
                    KnowledgeSubjectDefinition subject = domain.subjectResolver?.Invoke(subjectId);
                    return subject == null ? null : ToV2Subject(subject, domain.id);
                },
                subjectSource = () => (domain.subjectSource?.Invoke() ?? Enumerable.Empty<KnowledgeSubjectDefinition>())
                    .Where(subject => subject != null).Select(subject => ToV2Subject(subject, domain.id)),
                source = "v1:" + domain.id
            }, new KnowledgeRegistrationOptions
            {
                source = "v1:" + domain.id,
                conflict = KnowledgeRegistrationConflict.Replace
            });
            if (!v2Registered) Diagnostics.Add("Version-two schema registration failed: " + domain.id);
            InvalidateDomain(domain.id);
            return true;
        }

        public static bool RegisterUi(IKnowledgeUiProvider provider)
        {
            if (provider == null || provider.DomainId.NullOrEmpty()) return false;
            if (UiProviders.ContainsKey(provider.DomainId)) Diagnostics.Add("Duplicate UI provider replaced: " + provider.DomainId);
            UiProviders[provider.DomainId] = provider;
            KnowledgeDomainDefinition domain = Domain(provider.DomainId);
            KnowledgeProviderRegistry.Register(provider.DomainId, domain?.sortOrder ?? 0, provider.BioEntry);
            revision++;
            return true;
        }

        public static bool RegisterEffect(IKnowledgeEffectProvider provider)
        {
            if (provider == null || provider.DomainId.NullOrEmpty() || provider.Id.NullOrEmpty()) return false;
            if (!EffectProviders.TryGetValue(provider.DomainId, out List<IKnowledgeEffectProvider> providers))
            {
                providers = new List<IKnowledgeEffectProvider>();
                EffectProviders.Add(provider.DomainId, providers);
            }
            int existing = providers.FindIndex(value => value.Id == provider.Id);
            if (existing >= 0)
            {
                Diagnostics.Add("Duplicate effect provider replaced: " + provider.DomainId + "/" + provider.Id);
                providers[existing] = provider;
            }
            else providers.Add(provider);
            providers.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            revision++;
            return true;
        }

        public static bool UnregisterDomain(string domainId)
        {
            if (domainId.NullOrEmpty() || !Domains.Remove(domainId)) return false;
            UiProviders.Remove(domainId);
            EffectProviders.Remove(domainId);
            KnowledgeRegistry.UnregisterDomain(domainId, "v1:" + domainId);
            InvalidateDomain(domainId);
            return true;
        }

        public static KnowledgeDomainDefinition Domain(string domainId) => domainId != null && Domains.TryGetValue(domainId, out KnowledgeDomainDefinition value) ? value : null;
        public static IKnowledgeUiProvider Ui(string domainId) => domainId != null && UiProviders.TryGetValue(domainId, out IKnowledgeUiProvider value) ? value : null;
        internal static IReadOnlyList<IKnowledgeEffectProvider> Effects(string domainId) => domainId != null && EffectProviders.TryGetValue(domainId, out List<IKnowledgeEffectProvider> value) ? value : Array.Empty<IKnowledgeEffectProvider>();

        public static KnowledgeSubjectDefinition ResolveSubject(string domainId, string subjectId)
        {
            if (domainId.NullOrEmpty() || subjectId.NullOrEmpty()) return null;
            string key = domainId + "\n" + subjectId;
            if (SubjectCache.TryGetValue(key, out KnowledgeSubjectDefinition cached)) return cached;
            KnowledgeDomainDefinition domain = Domain(domainId);
            KnowledgeSubjectDefinition resolved = null;
            try { resolved = domain?.subjectResolver?.Invoke(subjectId); }
            catch (Exception exception) { KnowledgeLog.ErrorOnce("v1-subject-resolver:" + domainId, "Version-one subject resolver failed.", exception); }
            if (resolved != null) SubjectCache[key] = resolved;
            return resolved;
        }

        public static IEnumerable<KnowledgeSubjectDefinition> Subjects(string domainId)
        {
            KnowledgeDomainDefinition domain = Domain(domainId);
            try
            {
                return domain?.subjectSource?.Invoke()?.Where(subject => subject != null && !subject.id.NullOrEmpty())
                    .OrderBy(subject => subject.sortOrder).ThenBy(subject => subject.label).ToList() ?? Enumerable.Empty<KnowledgeSubjectDefinition>();
            }
            catch (Exception exception)
            {
                KnowledgeLog.ErrorOnce("v1-subject-source:" + domainId, "Version-one subject source failed.", exception);
                return Enumerable.Empty<KnowledgeSubjectDefinition>();
            }
        }

        public static void InvalidateDomain(string domainId)
        {
            if (!domainId.NullOrEmpty())
            {
                string prefix = domainId + "\n";
                foreach (string key in SubjectCache.Keys.Where(value => value.StartsWith(prefix, StringComparison.Ordinal)).ToList())
                    SubjectCache.Remove(key);
            }
            revision++;
            KnowledgeProviderRegistry.InvalidateAll();
            KnowledgeRegistry.InvalidateSubjects(domainId);
        }

        private static KnowledgeSubjectRegistration ToV2Subject(KnowledgeSubjectDefinition subject, string domainId)
        {
            return new KnowledgeSubjectRegistration
            {
                id = subject.id,
                label = subject.label,
                description = subject.description,
                sourceDef = subject.sourceDef,
                iconPath = subject.iconPath,
                sortOrder = subject.sortOrder,
                source = "v1:" + domainId
            };
        }
    }
}
