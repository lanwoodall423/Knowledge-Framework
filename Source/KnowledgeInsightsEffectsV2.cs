using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    public sealed class KnowledgeInsightContext
    {
        public readonly KnowledgeInsightDef insight;
        public readonly string domainId;
        public readonly string subjectId;
        public readonly Pawn pawn;
        public readonly KnowledgeScope scope;
        public readonly KnowledgeContextKey context;

        internal KnowledgeInsightContext(KnowledgeInsightDef insight, string domainId, string subjectId, Pawn pawn, KnowledgeScope scope)
            : this(insight, domainId, subjectId, pawn, scope, KnowledgeContextKey.Empty)
        {
        }

        internal KnowledgeInsightContext(KnowledgeInsightDef insight, string domainId, string subjectId, Pawn pawn, KnowledgeScope scope,
            KnowledgeContextKey context)
        {
            this.insight = insight;
            this.domainId = domainId;
            this.subjectId = subjectId;
            this.pawn = pawn;
            this.scope = scope;
            this.context = context;
        }
    }

    public sealed class KnowledgeInsightProgress
    {
        public readonly string insightId;
        public readonly bool activated;
        public readonly bool requirementsMet;
        public readonly IReadOnlyList<string> unmetRequirements;

        internal KnowledgeInsightProgress(string insightId, bool activated, bool met, IEnumerable<string> unmet)
        {
            this.insightId = insightId;
            this.activated = activated;
            requirementsMet = met;
            unmetRequirements = new ReadOnlyCollection<string>((unmet ?? Enumerable.Empty<string>()).ToList());
        }
    }

    public static class KnowledgeInsightService
    {
        private static readonly Dictionary<string, Func<KnowledgeInsightContext, KnowledgeInsightRequirement, bool>> CustomRequirements =
            new Dictionary<string, Func<KnowledgeInsightContext, KnowledgeInsightRequirement, bool>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Action<KnowledgeInsightContext, KnowledgeInsightOutcome>> CustomOutcomes =
            new Dictionary<string, Action<KnowledgeInsightContext, KnowledgeInsightOutcome>>(StringComparer.Ordinal);

        public static event Action<KnowledgeInsightContext> InsightAvailable;
        public static event Action<KnowledgeInsightContext> InsightConfirmed;

        public static bool RegisterRequirementEvaluator(string id,
            Func<KnowledgeInsightContext, KnowledgeInsightRequirement, bool> evaluator, bool replace = false)
        {
            if (id.NullOrEmpty() || evaluator == null || CustomRequirements.ContainsKey(id) && !replace) return false;
            CustomRequirements[id] = evaluator;
            return true;
        }

        public static bool RegisterOutcome(string id, Action<KnowledgeInsightContext, KnowledgeInsightOutcome> outcome, bool replace = false)
        {
            if (id.NullOrEmpty() || outcome == null || CustomOutcomes.ContainsKey(id) && !replace) return false;
            CustomOutcomes[id] = outcome;
            return true;
        }

        public static KnowledgeInsightProgress Progress(string insightId, string domainId, string subjectId, Pawn pawn = null,
            KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            KnowledgeInsightDef insight = schema?.insights.FirstOrDefault(item => item.defName == insightId);
            if (insight == null) return new KnowledgeInsightProgress(insightId, false, false, new[] { "Unknown insight." });
            bool colony = scope == KnowledgeScope.Colony;
            bool activated = GameComponent_KnowledgeFramework.Current?.InsightActivated(domainId, subjectId, insightId, pawn, colony) == true;
            List<string> unmet = Unmet(new KnowledgeInsightContext(insight, domainId, subjectId, pawn, scope, context));
            return new KnowledgeInsightProgress(insightId, activated, unmet.Count == 0, unmet);
        }

        public static bool Confirm(string insightId, string domainId, string subjectId, Pawn pawn = null,
            KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            KnowledgeInsightDef insight = schema?.insights.FirstOrDefault(item => item.defName == insightId);
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (insight == null || component == null || Unmet(new KnowledgeInsightContext(insight, domainId, subjectId, pawn, scope, context)).Count > 0)
                return false;
            bool colony = scope == KnowledgeScope.Colony;
            if (insight.preventRepeat && component.InsightActivated(domainId, subjectId, insightId, pawn, colony)) return false;
            KnowledgeInsightContext contextValue = new KnowledgeInsightContext(insight, domainId, subjectId, pawn, scope, context);
            component.ActivateInsight(domainId, subjectId, insightId, pawn, colony);
            List<KnowledgeChange> changes = new List<KnowledgeChange>();
            ApplyOutcomes(component, contextValue, changes);
            InvokeSafely(InsightConfirmed, contextValue, "confirmed");
            KnowledgeUiCache.Invalidate(changes);
            return true;
        }

        internal static List<string> EvaluateTouched(GameComponent_KnowledgeFramework component, IEnumerable<string> dependencies,
            List<KnowledgeChange> changes)
        {
            List<string> activated = new List<string>();
            HashSet<string> evaluated = new HashSet<string>(StringComparer.Ordinal);
            foreach (string dependency in dependencies)
            {
                string[] parts = dependency.Split('\n');
                if (parts.Length < 4) continue;
                string domainId = parts[0];
                string subjectId = parts[1];
                string facetId = parts[2];
                bool colony = parts[3] == "C";
                Pawn pawn = colony ? null : changes.FirstOrDefault(item => item.domainId == domainId && item.subjectId == subjectId && item.facetId == facetId && item.scope == KnowledgeScope.Personal)?.pawn;
                foreach (KnowledgeInsightDef insight in KnowledgeRegistry.InsightsFor(domainId, facetId))
                {
                    if (insight == null || !ScopeMatches(insight.scope, colony)) continue;
                    string ownerDomainId = insight.domainId.NullOrEmpty() ? domainId : insight.domainId;
                    string evaluationKey = insight.defName + "\n" + subjectId + "\n" + (colony ? "C" : (pawn?.thingIDNumber ?? 0).ToString());
                    bool alreadyActivated = component.InsightActivated(ownerDomainId, subjectId, insight.defName, pawn, colony);
                    if (!evaluated.Add(evaluationKey) || alreadyActivated && insight.preventRepeat) continue;
                    KnowledgeInsightContext context = new KnowledgeInsightContext(insight, ownerDomainId, subjectId, pawn,
                        colony ? KnowledgeScope.Colony : KnowledgeScope.Personal);
                    if (Unmet(context).Count > 0) continue;
                    InvokeSafely(InsightAvailable, context, "available");
                    if (!insight.activateAutomatically) continue;
                    component.ActivateInsight(ownerDomainId, subjectId, insight.defName, pawn, colony);
                    ApplyOutcomes(component, context, changes);
                    activated.Add(insight.defName);
                    InvokeSafely(InsightConfirmed, context, "confirmed");
                }
            }
            return activated;
        }

        private static List<string> Unmet(KnowledgeInsightContext context)
        {
            List<string> result = new List<string>();
            if (context.insight.requirementGroup != null && !KnowledgeRequirementService.Evaluate(context.insight.requirementGroup,
                context.domainId, context.subjectId, context.pawn, context.scope, context.context, out List<string> groupedUnmet)) result.AddRange(groupedUnmet);
            if (context.insight.requirementGroup != null) return result;
            if (context.insight.requirements == null) return result;
            for (int i = 0; i < context.insight.requirements.Count; i++)
            {
                KnowledgeInsightRequirement requirement = context.insight.requirements[i];
                if (requirement == null) continue;
                if (!RequirementMet(context, requirement)) result.Add(RequirementLabel(requirement));
            }
            return result;
        }

        private static bool RequirementMet(KnowledgeInsightContext context, KnowledgeInsightRequirement requirement)
        {
            if (requirement?.group != null && !KnowledgeRequirementService.Evaluate(requirement.group, requirement.domainId.NullOrEmpty() ? context.domainId : requirement.domainId,
                requirement.subjectId.NullOrEmpty() ? context.subjectId : requirement.subjectId, context.pawn, context.scope, context.context, out _)) return false;
            if (!requirement.domainId.NullOrEmpty() || !requirement.claimId.NullOrEmpty() || requirement.value != null || !requirement.contextTypeId.NullOrEmpty())
            {
                KnowledgeRequirement generalized = new KnowledgeRequirement
                {
                    domainId = requirement.domainId,
                    kind = requirement.claimId.NullOrEmpty() ? requirement.kind : KnowledgeRequirementKind.ClaimValue,
                    subjectId = requirement.subjectId,
                    facetId = requirement.facetId,
                    claimId = requirement.claimId,
                    trackId = requirement.trackId,
                    eventId = requirement.eventId,
                    insightId = requirement.insightId,
                    stageId = requirement.stageId,
                    customId = requirement.customId,
                    minimum = requirement.minimum,
                    colony = requirement.colony,
                    contextTypeId = requirement.contextTypeId,
                    contextId = requirement.contextId,
                    value = requirement.value,
                    comparison = requirement.comparison
                };
                return KnowledgeRequirementService.Evaluate(generalized, context.domainId, context.subjectId, context.pawn, context.scope, context.context);
            }
            string subjectId = requirement.subjectId.NullOrEmpty() ? context.subjectId : requirement.subjectId;
            KnowledgeScope scope = requirement.colony ? KnowledgeScope.Colony : context.scope;
            KnowledgeFacetSnapshotV2 facet = KnowledgeQuery.Facet(context.domainId, subjectId, requirement.facetId,
                context.pawn, scope);
            switch (requirement.kind)
            {
                case KnowledgeRequirementKind.Knowledge: return facet.amount >= requirement.minimum;
                case KnowledgeRequirementKind.Confidence: return facet.confidence >= requirement.minimum;
                case KnowledgeRequirementKind.EvidenceCount: return requirement.eventId.NullOrEmpty()
                    ? facet.evidenceCount >= requirement.minimum : facet.EventCount(requirement.eventId) >= requirement.minimum;
                case KnowledgeRequirementKind.SuccessCount: return requirement.eventId.NullOrEmpty()
                    ? facet.successCount >= requirement.minimum : facet.EventCount(requirement.eventId) >= requirement.minimum;
                case KnowledgeRequirementKind.FailureCount: return requirement.eventId.NullOrEmpty()
                    ? facet.failureCount >= requirement.minimum : facet.EventCount(requirement.eventId) >= requirement.minimum;
                case KnowledgeRequirementKind.Familiarity:
                    return KnowledgeQuery.Subject(context.domainId, subjectId, context.pawn, scope).familiarity >= requirement.minimum;
                case KnowledgeRequirementKind.DiscoveryStage:
                    KnowledgeSchema schema = KnowledgeRegistry.Schema(context.domainId);
                    KnowledgeStageSchema actual = schema?.Stage(KnowledgeDiscovery.CurrentStage(context.domainId, subjectId,
                        context.pawn, scope, context.context, KnowledgeContextFallbackMode.ParentThenGlobal));
                    KnowledgeStageSchema needed = schema?.Stage(requirement.stageId);
                    return actual != null && needed != null && actual.order >= needed.order;
                case KnowledgeRequirementKind.Expertise:
                    return KnowledgeQuery.Expertise(context.domainId, context.pawn, requirement.trackId).amount >= requirement.minimum;
                case KnowledgeRequirementKind.RelatedKnowledge: return facet.derivedAmount >= requirement.minimum;
                case KnowledgeRequirementKind.Insight:
                    return GameComponent_KnowledgeFramework.Current?.InsightActivated(context.domainId, subjectId,
                        requirement.insightId, context.pawn, scope == KnowledgeScope.Colony) == true;
                case KnowledgeRequirementKind.Custom:
                    if (!requirement.customId.NullOrEmpty() && CustomRequirements.TryGetValue(requirement.customId, out Func<KnowledgeInsightContext, KnowledgeInsightRequirement, bool> evaluator))
                    {
                        try { return evaluator(context, requirement); }
                        catch (Exception exception) { KnowledgeLog.ErrorOnce("insight-requirement:" + requirement.customId,
                            "Custom insight requirement '" + requirement.customId + "' failed.", exception); }
                    }
                    return false;
                default: return false;
            }
        }

        private static void ApplyOutcomes(GameComponent_KnowledgeFramework component, KnowledgeInsightContext context,
            List<KnowledgeChange> changes)
        {
            if (context.insight.outcomes == null) return;
            foreach (KnowledgeInsightOutcome outcome in context.insight.outcomes.Where(item => item != null))
            {
                if (outcome.knowledge > 0f || outcome.familiarity > 0f || outcome.expertise > 0f ||
                    outcome.claimMeasurements != null && outcome.claimMeasurements.Any(item => item != null))
                {
                    KnowledgeObservation observation = new KnowledgeObservation
                    {
                        observer = context.pawn,
                        domainId = context.domainId,
                        subjectId = context.subjectId,
                        facetId = outcome.facetId,
                        targetColony = context.scope == KnowledgeScope.Colony,
                        directKnowledge = KnowledgeMath.NonNegativeFiniteOr(outcome.knowledge, 0f),
                        directFamiliarity = KnowledgeMath.NonNegativeFiniteOr(outcome.familiarity, 0f),
                        directExpertise = KnowledgeMath.NonNegativeFiniteOr(outcome.expertise, 0f),
                        expertiseTrackId = outcome.expertiseTrackId,
                        suppressConfiguredKnowledge = true,
                        source = "insight:" + context.insight.defName,
                        reasonId = context.insight.defName,
                        documented = outcome.document,
                        claimMeasurements = outcome.claimMeasurements,
                        sharedExpertiseNamespaceId = outcome.sharedExpertiseNamespaceId,
                        sharedExpertiseWeight = outcome.sharedExpertiseWeight,
                        context = context.context
                    };
                    KnowledgeEngine.ApplyInsightOutcome(component, observation, changes);
                }
                if (!outcome.stageId.NullOrEmpty())
                {
                    KnowledgeSubjectStateRecord subject = context.scope == KnowledgeScope.Colony
                        ? (KnowledgeSubjectStateRecord)component.ColonySubjectV2(context.domainId, context.subjectId, true)
                        : component.PersonalSubjectV2(context.domainId, context.subjectId, context.pawn, true);
                    subject.stageId = outcome.stageId;
                    component.Touch(subject, null);
                }
                if (!outcome.customId.NullOrEmpty() && CustomOutcomes.TryGetValue(outcome.customId, out Action<KnowledgeInsightContext, KnowledgeInsightOutcome> custom))
                {
                    try { custom(context, outcome); }
                    catch (Exception exception) { KnowledgeLog.ErrorOnce("insight-outcome:" + outcome.customId,
                        "Custom insight outcome '" + outcome.customId + "' failed.", exception); }
                }
            }
        }

        private static bool ScopeMatches(KnowledgeInsightScope scope, bool colony) => scope == KnowledgeInsightScope.Both ||
            colony && scope == KnowledgeInsightScope.Colony || !colony && scope == KnowledgeInsightScope.Personal;
        private static string RequirementLabel(KnowledgeInsightRequirement value) => value.kind + " " + value.minimum.ToString("0.##");

        private static void InvokeSafely(Action<KnowledgeInsightContext> handlers, KnowledgeInsightContext value, string eventName)
        {
            if (handlers == null) return;
            foreach (Action<KnowledgeInsightContext> handler in handlers.GetInvocationList())
                try { handler(value); }
                catch (Exception exception) { KnowledgeLog.ErrorOnce("insight-event:" + eventName + ":" + handler.Method.Name,
                    "Insight " + eventName + " subscriber failed.", exception); }
        }
    }

    public sealed class KnowledgeEffectQuery
    {
        public string domainId;
        public string subjectId;
        public string facetId;
        public string channelId;
        public Pawn pawn;
        public KnowledgeScope scope;
        public float baseValue;
        public bool basePermission = true;
        public string contextId;
        public string contextTypeId;
        public KnowledgeContextKey context;
        public bool includeExplanation;
    }

    public sealed class KnowledgeEffectResult
    {
        public readonly float numericValue;
        public readonly bool permitted;
        public readonly bool revealed;
        public readonly string resultId;
        public readonly float predictionAccuracy;
        public readonly IReadOnlyList<string> actionIds;
        public readonly IReadOnlyList<string> explanation;

        internal KnowledgeEffectResult(float numeric, bool permitted, bool revealed, string resultId,
            float prediction, IEnumerable<string> actions, IEnumerable<string> explanation = null)
        {
            numericValue = numeric;
            this.permitted = permitted;
            this.revealed = revealed;
            this.resultId = resultId;
            predictionAccuracy = prediction;
            actionIds = new ReadOnlyCollection<string>((actions ?? Enumerable.Empty<string>()).Distinct().ToList());
            this.explanation = new ReadOnlyCollection<string>((explanation ?? Enumerable.Empty<string>()).Distinct().ToList());
        }
    }

    public interface IKnowledgeTypedEffectProvider
    {
        string Id { get; }
        string DomainId { get; }
        int Priority { get; }
        void Apply(KnowledgeEffectQuery query, KnowledgeEffectAccumulator accumulator);
    }

    public sealed class KnowledgeEffectAccumulator
    {
        private float numeric;
        private bool permitted;
        private bool revealed;
        private string resultId;
        private int overridePriority = int.MinValue;
        private float prediction;
        private readonly List<string> actions = new List<string>();
        private readonly List<string> explanation = new List<string>();

        internal KnowledgeEffectAccumulator(float numeric, bool permitted)
        {
            this.numeric = numeric;
            this.permitted = permitted;
        }

        public void Compose(KnowledgeEffectComposition composition, float value, int priority = 0, string id = null)
        {
            if (!KnowledgeMath.IsFinite(value)) return;
            switch (composition)
            {
                case KnowledgeEffectComposition.Add: numeric += value; break;
                case KnowledgeEffectComposition.Multiply: numeric *= value; break;
                case KnowledgeEffectComposition.Minimum: numeric = Math.Min(numeric, value); break;
                case KnowledgeEffectComposition.Maximum: numeric = Math.Max(numeric, value); break;
                case KnowledgeEffectComposition.Override:
                    if (priority >= overridePriority) { numeric = value; overridePriority = priority; resultId = id; }
                    break;
                case KnowledgeEffectComposition.Allow: permitted = true; break;
                case KnowledgeEffectComposition.Deny: permitted = false; break;
                case KnowledgeEffectComposition.Reveal: revealed = true; resultId = id ?? resultId; break;
                case KnowledgeEffectComposition.Action: if (!id.NullOrEmpty()) actions.Add(id); break;
                case KnowledgeEffectComposition.Prediction: prediction = Math.Max(prediction, KnowledgeMath.Clamp01Finite(value)); break;
            }
            explanation.Add((id.NullOrEmpty() ? composition.ToString() : id) + ": " + composition);
            if (!KnowledgeMath.IsFinite(numeric)) numeric = 0f;
        }

        internal KnowledgeEffectResult Result(bool includeExplanation = true) => new KnowledgeEffectResult(numeric, permitted, revealed, resultId, prediction, actions,
            includeExplanation ? explanation : null);
    }

    public static class KnowledgeEffects
    {
        private static readonly Dictionary<string, List<IKnowledgeTypedEffectProvider>> Providers =
            new Dictionary<string, List<IKnowledgeTypedEffectProvider>>(StringComparer.Ordinal);

        public static bool Register(IKnowledgeTypedEffectProvider provider, bool replace = false)
        {
            string domainId = provider == null ? null : KnowledgeRegistry.ResolveDomainId(provider.DomainId);
            if (provider == null || provider.Id.NullOrEmpty() || KnowledgeRegistry.Schema(domainId) == null) return false;
            if (!Providers.TryGetValue(domainId, out List<IKnowledgeTypedEffectProvider> list))
                Providers.Add(domainId, list = new List<IKnowledgeTypedEffectProvider>());
            int existing = list.FindIndex(item => item.Id == provider.Id);
            if (existing >= 0 && !replace) return false;
            if (existing >= 0) list[existing] = provider;
            else list.Add(provider);
            list.Sort((left, right) => left.Priority != right.Priority ? left.Priority.CompareTo(right.Priority) : string.CompareOrdinal(left.Id, right.Id));
            return true;
        }

        public static bool Unregister(string domainId, string providerId = null)
        {
            domainId = KnowledgeRegistry.ResolveDomainId(domainId);
            if (domainId.NullOrEmpty() || !Providers.TryGetValue(domainId, out List<IKnowledgeTypedEffectProvider> list)) return false;
            if (providerId.NullOrEmpty())
            {
                Providers.Remove(domainId);
                return true;
            }
            bool removed = list.RemoveAll(item => item.Id == providerId) > 0;
            if (list.Count == 0) Providers.Remove(domainId);
            return removed;
        }

        public static KnowledgeEffectResult Query(KnowledgeEffectQuery query)
        {
            if (query == null || query.channelId.NullOrEmpty()) return new KnowledgeEffectAccumulator(query?.baseValue ?? 0f, query?.basePermission ?? false).Result(query?.includeExplanation == true);
            KnowledgeSchema schema = KnowledgeRegistry.Schema(query.domainId);
            KnowledgeEffectAccumulator accumulator = new KnowledgeEffectAccumulator(
                KnowledgeMath.IsFinite(query.baseValue) ? query.baseValue : 0f, query.basePermission);
            if (schema == null) return accumulator.Result();
            KnowledgeContextKey context = query.context.IsEmpty && !query.contextTypeId.NullOrEmpty() && !query.contextId.NullOrEmpty()
                ? new KnowledgeContextKey(query.contextTypeId, query.contextId) : query.context;
            KnowledgeScope effectScope = query.scope;
            foreach (KnowledgeEffectDef effect in schema.effects.Where(item => item.channelId == query.channelId))
            {
                KnowledgeFacetSnapshotV2 effectFacet = effect.useColony
                    ? KnowledgeQuery.Facet(query.domainId, query.subjectId, effect.facetId ?? query.facetId, null, KnowledgeScope.Colony, true, false)
                    : KnowledgeQuery.Facet(query.domainId, query.subjectId, effect.facetId ?? query.facetId, query.pawn, effectScope, true, false);
                string effectStage = KnowledgeDiscovery.CurrentStage(query.domainId, query.subjectId,
                    effect.useColony ? null : query.pawn, effect.useColony ? KnowledgeScope.Colony : effectScope,
                    context, KnowledgeContextFallbackMode.ParentThenGlobal);
                if (effectFacet.amount < effect.minimumKnowledge || effectFacet.confidence < effect.minimumConfidence ||
                    !StageMet(schema, effectStage, effect.minimumStageId) ||
                    effect.requirements != null && !KnowledgeRequirementService.Evaluate(effect.requirements, schema.id, query.subjectId, query.pawn,
                        effect.useColony ? KnowledgeScope.Colony : effectScope, context, out _)) continue;
                accumulator.Compose(effect.composition, effect.value, effect.priority, effect.resultId);
            }
            if (Providers.TryGetValue(schema.id, out List<IKnowledgeTypedEffectProvider> providers))
            {
                for (int i = 0; i < providers.Count; i++)
                {
                    try { providers[i].Apply(query, accumulator); }
                    catch (Exception exception) { KnowledgeLog.ErrorOnce("typed-effect:" + providers[i].Id,
                        "Typed effect provider '" + providers[i].Id + "' failed.", exception); }
                }
            }
            return accumulator.Result(query.includeExplanation);
        }

        private static bool StageMet(KnowledgeSchema schema, string actualId, string requiredId)
        {
            if (requiredId.NullOrEmpty()) return true;
            KnowledgeStageSchema actual = schema.Stage(actualId);
            KnowledgeStageSchema required = schema.Stage(requiredId);
            return actual != null && required != null && actual.order >= required.order;
        }
    }
}
