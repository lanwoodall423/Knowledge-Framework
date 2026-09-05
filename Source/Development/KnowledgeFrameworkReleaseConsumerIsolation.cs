using System;
using System.Linq;
using LudeonTK;
using Verse;

namespace KnowledgeFramework.Development
{
    // Development-only fixture for the release recipe. It uses two independent
    // consumer owners and leaves no registration or state behind.
    internal static class KnowledgeFrameworkReleaseConsumerIsolation
    {
        private const string AlphaDomain = "knowledgeframework.release.consumer.alpha";
        private const string BetaDomain = "knowledgeframework.release.consumer.beta";
        private const string AlphaSource = "knowledgeframework.release.consumer.alpha";
        private const string BetaSource = "knowledgeframework.release.consumer.beta";
        private const string FacetId = "knowledge";
        private const string AlphaSubject = "alpha-subject";
        private const string BetaSubject = "beta-subject";

        [DebugAction("Knowledge Framework", "Run two-consumer isolation validation", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RunFromDebugMenu()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn ?? Find.CurrentMap?.mapPawns.FreeColonists.FirstOrDefault();
            Exception failure = null;
            try
            {
                if (pawn == null)
                    throw new InvalidOperationException("a live pawn is required");

                CleanupDomain(AlphaDomain, AlphaSource);
                CleanupDomain(BetaDomain, BetaSource);
                RegisterDomain(AlphaDomain, AlphaSource, AlphaSubject, "Alpha consumer");
                RegisterDomain(BetaDomain, BetaSource, BetaSubject, "Beta consumer");

                KnowledgeDomainRegistration alpha = Registration(AlphaDomain, AlphaSource, AlphaSubject, "Alpha consumer");
                KnowledgeDomainRegistration beta = Registration(BetaDomain, BetaSource, BetaSubject, "Beta consumer");
                bool alphaOwnership = KnowledgeConsumerApi.InspectDomainRegistration(alpha,
                    new KnowledgeRegistrationOptions { source = AlphaSource }).state == KnowledgeDomainRegistrationState.RegisteredBySameOwner;
                bool betaOwnership = KnowledgeConsumerApi.InspectDomainRegistration(beta,
                    new KnowledgeRegistrationOptions { source = BetaSource }).state == KnowledgeDomainRegistrationState.RegisteredBySameOwner;
                bool foreignRejected = !KnowledgeConsumerApi.RegisterDomain(alpha,
                    new KnowledgeRegistrationOptions { source = BetaSource }).Success;
                Require(alphaOwnership && betaOwnership && foreignRejected,
                    "consumer ownership and foreign registration isolation");

                KnowledgeTransactionResult alphaObservation = Observe(AlphaDomain, AlphaSubject, pawn, AlphaSource);
                KnowledgeTransactionResult betaObservation = Observe(BetaDomain, BetaSubject, pawn, BetaSource);
                Require(alphaObservation != null && alphaObservation.success && betaObservation != null && betaObservation.success,
                    "independent consumer observations");
                Require(KnowledgeQuery.Facet(AlphaDomain, AlphaSubject, FacetId, pawn).amount > 0f &&
                    KnowledgeQuery.Facet(BetaDomain, BetaSubject, FacetId, pawn).amount > 0f,
                    "independent consumer records");

                GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
                if (component == null)
                    throw new InvalidOperationException("framework game component is unavailable");
                component.RemoveDomainDataV2(AlphaDomain);
                Require(KnowledgeQuery.Facet(AlphaDomain, AlphaSubject, FacetId, pawn).amount == 0f &&
                    KnowledgeQuery.Facet(BetaDomain, BetaSubject, FacetId, pawn).amount > 0f,
                    "removing one consumer preserves the other consumer");
                KnowledgeRegistry.UnregisterDomain(AlphaDomain, AlphaSource);
                Require(KnowledgeRegistry.Schema(AlphaDomain) == null && KnowledgeRegistry.Schema(BetaDomain) != null,
                    "consumer registration cleanup isolation");
                Log.Message("[Knowledge Framework] Two-consumer isolation validation PASS");
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                try
                {
                    CleanupDomain(AlphaDomain, AlphaSource);
                    CleanupDomain(BetaDomain, BetaSource);
                }
                catch (Exception cleanupException)
                {
                    failure = new InvalidOperationException("fixture cleanup failed", cleanupException);
                }
            }

            if (failure != null)
                throw new InvalidOperationException("Knowledge Framework two-consumer isolation validation FAILED: " + failure.Message, failure);
        }

        private static void RegisterDomain(string domainId, string source, string subjectId, string label)
        {
            KnowledgeConsumerRegistrationResult result = KnowledgeConsumerApi.RegisterDomain(
                Registration(domainId, source, subjectId, label), new KnowledgeRegistrationOptions
                {
                    source = source,
                    priority = 100
                });
            if (!result.Success)
                throw new InvalidOperationException("registration failed for " + domainId + ": " + result.code);
            bool subjectRegistered = KnowledgeRegistry.RegisterSubject(domainId, new KnowledgeSubjectRegistration
            {
                id = subjectId,
                label = subjectId,
                applicableFacetIds = new[] { FacetId },
                source = source,
                state = KnowledgeSubjectState.Active
            }, new KnowledgeRegistrationOptions { source = source, priority = 100 });
            if (!subjectRegistered && KnowledgeRegistry.ResolveSubject(domainId, subjectId) == null)
                throw new InvalidOperationException("subject registration failed for " + domainId);
        }

        private static KnowledgeDomainRegistration Registration(string domainId, string source, string subjectId, string label)
        {
            return new KnowledgeDomainRegistration
            {
                id = domainId,
                label = label,
                source = source,
                facets = new[] { new KnowledgeFacetDef { defName = FacetId, stableId = FacetId, label = FacetId } },
                subjectSource = () => new[] { new KnowledgeSubjectRegistration
                {
                    id = subjectId,
                    label = subjectId,
                    applicableFacetIds = new[] { FacetId },
                    source = source,
                    state = KnowledgeSubjectState.Active
                } }
            };
        }

        private static KnowledgeTransactionResult Observe(string domainId, string subjectId, Pawn pawn, string source)
        {
            return KnowledgeEngine.Submit(new KnowledgeObservation
            {
                observer = pawn,
                domainId = domainId,
                subjectId = subjectId,
                facetId = FacetId,
                directKnowledge = 1f,
                suppressConfiguredKnowledge = true,
                source = source,
                sourceInstanceId = source
            });
        }

        private static void CleanupDomain(string domainId, string source)
        {
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component != null)
                component.RemoveDomainDataV2(domainId);
            KnowledgeRegistry.UnregisterDomain(domainId, source);
            KnowledgeRegistry.ClearDiagnostics(domainId);
        }

        private static void Require(bool condition, string check)
        {
            if (!condition)
                throw new InvalidOperationException(check);
        }
    }
}
