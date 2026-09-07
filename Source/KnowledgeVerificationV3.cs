using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    internal static class KnowledgeFrameworkVerificationV3
    {
        private const string DomainId = "KnowledgeFramework.Verification.V3";
        private const string AggregationDomainId = "KnowledgeFramework.Verification.V3.Aggregation";

        internal static KnowledgeVerificationResult RunPureTests()
        {
            KnowledgeVerificationTrace.Begin();
            int passed = 0;
            List<string> failures = new List<string>();
            KnowledgeClaimDef highest = Claim("highest", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.Highest);
            KnowledgeClaimDef lowest = Claim("lowest", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.Lowest);
            KnowledgeClaimDef mean = Claim("mean", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.Mean);
            KnowledgeClaimDef weighted = Claim("weighted", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.WeightedMean);
            KnowledgeClaimDef range = Claim("range", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.ObservedRange);
            KnowledgeClaimDef union = Claim("union", KnowledgeClaimValueType.SetOfIds, KnowledgeClaimAggregation.Union);
            KnowledgeClaimDef intersection = Claim("intersection", KnowledgeClaimValueType.SetOfIds, KnowledgeClaimAggregation.Intersection);
            List<KnowledgeMeasurement> numeric = new List<KnowledgeMeasurement>
            {
                Measurement(KnowledgeClaimValue.Float(1f), 1f, 1),
                Measurement(KnowledgeClaimValue.Float(3f), 3f, 2)
            };
            Check("claim highest", KnowledgeClaimService.AggregateForVerification(highest, numeric).numericValue == 3f, ref passed, failures);
            Check("claim lowest", KnowledgeClaimService.AggregateForVerification(lowest, numeric).numericValue == 1f, ref passed, failures);
            Check("claim mean", Math.Abs(KnowledgeClaimService.AggregateForVerification(mean, numeric).numericValue - 2f) < 0.001f, ref passed, failures);
            Check("claim weighted mean", Math.Abs(KnowledgeClaimService.AggregateForVerification(weighted, numeric).numericValue - 2.5f) < 0.001f, ref passed, failures);
            KnowledgeClaimValue rangeValue = KnowledgeClaimService.AggregateForVerification(range, numeric);
            Check("claim observed range", rangeValue.type == KnowledgeClaimValueType.NumericRange && rangeValue.rangeValue.minimum == 1f && rangeValue.rangeValue.maximum == 3f, ref passed, failures);
            List<KnowledgeMeasurement> sets = new List<KnowledgeMeasurement>
            {
                Measurement(KnowledgeClaimValue.Set(new[] { "a", "b" }), 1f, 1),
                Measurement(KnowledgeClaimValue.Set(new[] { "b", "c" }), 1f, 2)
            };
            Check("claim union", KnowledgeClaimService.AggregateForVerification(union, sets).setValues.SequenceEqual(new[] { "a", "b", "c" }), ref passed, failures);
            Check("claim intersection", KnowledgeClaimService.AggregateForVerification(intersection, sets).setValues.SequenceEqual(new[] { "b" }), ref passed, failures);
            Check("typed value rejection", !KnowledgeClaimValue.Percentage(101f).TryValidate(KnowledgeClaimValueType.Percentage, out _), ref passed, failures);
            Check("requirement comparison", KnowledgeRequirementService.CompareClaimValue(KnowledgeClaimValue.Set(new[] { "a", "b" }),
                KnowledgeClaimValue.Set(new[] { "a" }), KnowledgeRequirementComparison.Contains), ref passed, failures);
            Check("context cycle graph", KnowledgeGraphValidation.HasCycle(new[]
            {
                new KeyValuePair<string, string>("region:a", "region:b"),
                new KeyValuePair<string, string>("region:b", "region:a")
            }), ref passed, failures);
            Check("parent relation acyclic graph", !KnowledgeGraphValidation.HasCycle(new[]
            {
                new KeyValuePair<string, string>("a", "b"),
                new KeyValuePair<string, string>("b", "c")
            }), ref passed, failures);
            KnowledgeRequirementGroup group = new KnowledgeRequirementGroup
            {
                mode = KnowledgeRequirementGroupMode.Any,
                requirements = new List<KnowledgeRequirement>
                {
                    new KnowledgeRequirement { kind = KnowledgeRequirementKind.Knowledge, minimum = 1f },
                    new KnowledgeRequirement { kind = KnowledgeRequirementKind.Knowledge, minimum = 100f }
                }
            };
            Check("requirement group shape", group.mode == KnowledgeRequirementGroupMode.Any && group.requirements.Count == 2, ref passed, failures);
            KnowledgeStageAggregate balancedOne = KnowledgeStageAggregation.Calculate(KnowledgeStageAggregationMode.Balanced,
                new[] { new KnowledgeStageFacetValue(100f, 100f, 0.9f, 1) });
            KnowledgeStageAggregate balancedSeveral = KnowledgeStageAggregation.Calculate(KnowledgeStageAggregationMode.Balanced,
                new[] { new KnowledgeStageFacetValue(100f, 100f, 0.9f, 1), new KnowledgeStageFacetValue(100f, 100f, 0.9f, 1) });
            KnowledgeStageAggregate balancedWithEmpty = KnowledgeStageAggregation.Calculate(KnowledgeStageAggregationMode.Balanced,
                new[] { new KnowledgeStageFacetValue(100f, 100f, 0.9f, 1), new KnowledgeStageFacetValue(0f, 100f, 0f, 0) });
            KnowledgeStageAggregate balancedHighAndPoor = KnowledgeStageAggregation.Calculate(KnowledgeStageAggregationMode.Balanced,
                new[] { new KnowledgeStageFacetValue(100f, 100f, 1f, 1), new KnowledgeStageFacetValue(100f, 100f, 0.1f, 1) });
            KnowledgeStageAggregate legacyAggregate = KnowledgeStageAggregation.Calculate(KnowledgeStageAggregationMode.LegacySumMax,
                new[] { new KnowledgeStageFacetValue(50f, 100f, 0.2f, 1), new KnowledgeStageFacetValue(50f, 100f, 0.8f, 1) });
            Check("balanced equivalent facets", Math.Abs(balancedOne.knowledge - balancedSeveral.knowledge) < 0.001f &&
                Math.Abs(balancedOne.confidence - balancedSeveral.confidence) < 0.001f, ref passed, failures);
            Check("balanced empty facet does not advance", balancedWithEmpty.knowledge < balancedOne.knowledge &&
                balancedWithEmpty.confidence < balancedOne.confidence, ref passed, failures);
            Check("balanced confidence requires broad support", balancedHighAndPoor.confidence < 0.8f, ref passed, failures);
            Check("legacy stage aggregation compatibility", Math.Abs(legacyAggregate.knowledge - 100f) < 0.001f &&
                Math.Abs(legacyAggregate.confidence - 0.8f) < 0.001f, ref passed, failures);
            KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef
            {
                defName = "VerificationPureParent",
                stableId = "verification.pure.parent"
            }, true);
            KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef
            {
                defName = "VerificationPureChild",
                stableId = "verification.pure.child",
                parentTypeId = "verification.pure.parent"
            }, true);
            KnowledgeContextKey pureChild = new KnowledgeContextKey("verification.pure.child", "one");
            IReadOnlyList<KnowledgeContextKey> exactGlobal = KnowledgeContextRegistry.Chain(KnowledgeContextKey.Empty,
                KnowledgeContextFallbackMode.ExactOnly);
            IReadOnlyList<KnowledgeContextKey> parentGlobal = KnowledgeContextRegistry.Chain(pureChild,
                KnowledgeContextFallbackMode.ParentThenGlobal);
            Check("global exact context chain", exactGlobal.Count == 1 && exactGlobal[0].IsEmpty, ref passed, failures);
            Check("deduplicated parent/global chain", parentGlobal.Count == 3 && parentGlobal.Count(item => item.IsEmpty) == 1,
                ref passed, failures);
            Check("partial context is not global", KnowledgeContextRegistry.Chain(new KnowledgeContextKey("verification.pure.child", null),
                KnowledgeContextFallbackMode.ParentThenGlobal).Count == 0, ref passed, failures);
            return new KnowledgeVerificationResult(passed, failures, KnowledgeVerificationTrace.End(), 0, 0,
                passed, failures.Count);
        }

        internal static KnowledgeVerificationResult RunGameTests(Pawn pawn)
        {
            KnowledgeVerificationTrace.Begin();
            KnowledgeVerificationResult pure = RunPureTests();
            int passed = pure.passed;
            List<string> failures = pure.failures.ToList();
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component == null || pawn == null)
            {
                return new KnowledgeVerificationResult(passed, failures,
                    KnowledgeVerificationTrace.End().Concat(pure.passedTests), 0, 1,
                    pure.passed, pure.failures.Count, 0, 0);
            }
            KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "VerificationRegion", stableId = "verification.region", label = "Region" }, true);
            KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef
            {
                defName = "VerificationChildRegion",
                stableId = "verification.region.child",
                label = "Child region",
                parentTypeId = "verification.region"
            }, true);
            KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef
            {
                defName = "VerificationNoFallback",
                stableId = "verification.region.nofallback",
                parentTypeId = "verification.region",
                allowFallback = false
            }, true);
            KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef
            {
                defName = "VerificationThrowingContext",
                stableId = "verification.region.throwing",
                label = "Throwing context"
            }, true);
            VerificationContextPresentationProvider.EmitLarge = false;
            VerificationContextPresentationProvider.ResetCounters();
            KnowledgeContextRegistry.RegisterPresentationProvider("verification.region",
                new VerificationContextPresentationProvider(), true);
            KnowledgeContextRegistry.RegisterPresentationProvider("verification.region.child",
                new VerificationContextPresentationProvider(), true);
            KnowledgeContextRegistry.RegisterPresentationProvider("verification.region.throwing",
                new ThrowingContextPresentationProvider(), true);
            KnowledgeRelationService.RegisterType(new KnowledgeSubjectRelationTypeDef { defName = "VerificationParent", stableId = "verification.parent", parentage = true }, true);
            KnowledgeExpertiseNamespaceDef namespaceDef = new KnowledgeExpertiseNamespaceDef { defName = "VerificationField", stableId = "verification.field", adept = 1f, expert = 2f, master = 3f };
            KnowledgeSharedExpertiseService.RegisterNamespace(namespaceDef, true);
            KnowledgeFacetDef identity = Facet("identity", 100f);
            KnowledgeFacetDef biology = Facet("biology", 100f);
            KnowledgeFacetDef secret = Facet("secret", 100f);
            secret.label = "Secret";
            secret.hiddenUntilRevealed = true;
            secret.revealKnowledge = 100f;
            secret.revealConfidence = 0.4f;
            KnowledgeClaimDef size = Claim("size", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.Highest, "biology");
            KnowledgeClaimDef traits = Claim("traits", KnowledgeClaimValueType.SetOfIds, KnowledgeClaimAggregation.Union, "biology");
            KnowledgeClaimDef hiddenSecret = Claim("hidden-secret", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.Highest, "secret");
            size.label = "Size";
            traits.revealedByDefault = false;
            hiddenSecret.revealedByDefault = false;
            KnowledgeExpertiseTrackDef field = new KnowledgeExpertiseTrackDef { defName = "field", stableId = "field", label = "Field work", adept = 1f, expert = 2f, master = 3f };
            KnowledgeSubjectArchetypeDef specimen = new KnowledgeSubjectArchetypeDef
            {
                defName = "specimen",
                stableId = "specimen",
                applicableFacetIds = new List<string> { "biology", "identity", "secret" },
                applicableClaimIds = new List<string> { "size", "traits", "hidden-secret" }
            };
            KnowledgeObservationDef recipe = new KnowledgeObservationDef
            {
                defName = "study",
                stableId = "study",
                facetIds = new List<string> { "identity", "biology" },
                successOutcomes = new List<KnowledgeObservationOutcome>
                {
                    new KnowledgeObservationOutcome { facetId = "identity", knowledge = 3f },
                    new KnowledgeObservationOutcome { facetId = "biology", knowledge = 7f, claimMeasurements = new List<KnowledgeMeasurement>
                    {
                        new KnowledgeMeasurement { claimId = "size", value = KnowledgeClaimValue.Float(42f), summary = "measured" },
                        new KnowledgeMeasurement { claimId = "traits", value = KnowledgeClaimValue.Set(new[] { "hardy", "early" }), summary = "observed" }
                    } }
                },
                expertiseOutcomes = new List<KnowledgeExpertiseOutcome>
                {
                    new KnowledgeExpertiseOutcome { trackId = "field", expertise = 2f, namespaceId = "verification.field" }
                },
                accrualPolicy = new KnowledgeAccrualPolicy { lifetimeCap = 1, stateLimit = 8 }
            };
            KnowledgeObservationDef contextless = new KnowledgeObservationDef
            {
                defName = "contextless",
                stableId = "contextless",
                facetIds = new List<string> { "biology" },
                propagateContext = false,
                successOutcomes = new List<KnowledgeObservationOutcome>
                {
                    new KnowledgeObservationOutcome
                    {
                        facetId = "biology",
                        knowledge = 2f,
                        claimMeasurements = new List<KnowledgeMeasurement>
                        {
                            new KnowledgeMeasurement { claimId = "size", value = KnowledgeClaimValue.Float(7f) }
                        }
                    }
                },
                expertiseOutcomes = new List<KnowledgeExpertiseOutcome>
                {
                    new KnowledgeExpertiseOutcome { trackId = "field", expertise = 1f, namespaceId = "verification.field" }
                },
                witnessDistribution = new KnowledgeWitnessDistribution
                {
                    policy = KnowledgeWitnessDistributionPolicy.WitnessesReduced,
                    includeObserver = true,
                    efficiency = 0.5f
                }
            };
            KnowledgeObservationDef cooldown = AccrualObservation("cooldown", new KnowledgeAccrualPolicy
            {
                cooldownTicks = 100,
                stateLimit = 8
            });
            KnowledgeObservationDef daily = AccrualObservation("daily", new KnowledgeAccrualPolicy
            {
                dailyCap = 2,
                stateLimit = 8
            });
            KnowledgeObservationDef lifetime = AccrualObservation("lifetime", new KnowledgeAccrualPolicy
            {
                lifetimeCap = 2,
                stateLimit = 8
            });
            KnowledgeObservationDef noOp = AccrualObservation("no-op", new KnowledgeAccrualPolicy
            {
                firstObservationBonus = 0f,
                stateLimit = 8
            });
            KnowledgeObservationDef diminishing = AccrualObservation("diminishing", new KnowledgeAccrualPolicy
            {
                diminishingReturns = 0.5f,
                stateLimit = 8
            }, "biology");
            KnowledgeObservationDef firstOutcome = AccrualObservation("first-outcome", new KnowledgeAccrualPolicy
            {
                firstObservationBonus = 1f,
                firstSuccessBonus = 2f,
                firstFailureBonus = 3f,
                stateLimit = 8
            });
            firstOutcome.failureKnowledgeFactor = 1f;
            KnowledgeObservationDef distinct = AccrualObservation("distinct", new KnowledgeAccrualPolicy
            {
                differentSpecimenBonus = 2f,
                differentContextBonus = 2f,
                independentSourceConfidenceBonus = 2f,
                repeatedSourceConfidencePenalty = 0.5f,
                stateLimit = 8
            });
            KnowledgeObservationDef bounded = AccrualObservation("bounded", new KnowledgeAccrualPolicy
            {
                stateLimit = 2
            });
            bounded.facetIds.Add("biology");
            KnowledgeObservationDef legacyUnique = AccrualObservation("legacy-unique", new KnowledgeAccrualPolicy
            {
                uniquePerSourceInstance = true,
                lifetimeCap = 2,
                stateLimit = 4
            });
            legacyUnique.facetIds.Add("identity");
            KnowledgeObservationDef legacyCap = AccrualObservation("legacy-cap", new KnowledgeAccrualPolicy
            {
                uniquePerSubjectAndContext = true,
                lifetimeCap = 2,
                stateLimit = 4
            });
            legacyCap.facetIds.Add("identity");
            KnowledgeObservationDef legacyShared = AccrualObservation("legacy-shared", new KnowledgeAccrualPolicy
            {
                uniquePerSubjectAndContext = true,
                lifetimeCap = 1,
                stateLimit = 4
            });
            legacyShared.facetIds.Add("identity");
            KnowledgeMilestoneTrackDef track = new KnowledgeMilestoneTrackDef
            {
                defName = "progress",
                stableId = "progress",
                milestones = new List<KnowledgeMilestoneDef> { new KnowledgeMilestoneDef { stableId = "established", label = "Established" } }
            };
            KnowledgeDomainRegistration registration = new KnowledgeDomainRegistration
            {
                id = DomainId,
                label = "V3 Verification",
                enableUncertainty = true,
                enableFamiliarity = true,
                sharingModel = KnowledgeSharingModel.Custom,
                stageAggregationMode = KnowledgeStageAggregationMode.Balanced,
                facets = new[] { identity, biology, secret },
                claims = new[] { size, traits, hiddenSecret },
                archetypes = new[] { specimen },
                observations = new[] { recipe, contextless, cooldown, daily, lifetime, noOp, diminishing, firstOutcome, distinct, bounded, legacyUnique, legacyCap, legacyShared },
                expertiseTracks = new[] { field },
                milestoneTracks = new[] { track },
                expertiseNamespaces = new[] { namespaceDef },
                subjectResolver = id => id == "source" || id == "child" ? new KnowledgeSubjectRegistration
                {
                    id = id,
                    label = id == "source" ? "Source" : "Child",
                    archetypeId = "specimen"
                } : null,
                subjectSource = () => new[]
                {
                     new KnowledgeSubjectRegistration { id = "source", label = "Source", archetypeId = "specimen" },
                     new KnowledgeSubjectRegistration { id = "child", label = "Child", archetypeId = "specimen" },
                     new KnowledgeSubjectRegistration { id = "hidden-subject", label = "Hidden Subject", archetypeId = "specimen", state = KnowledgeSubjectState.Hidden },
                     new KnowledgeSubjectRegistration { id = "archived-subject", label = "Archived Subject", archetypeId = "specimen", state = KnowledgeSubjectState.Archived },
                     new KnowledgeSubjectRegistration { id = "missing-subject", label = "Missing Subject", archetypeId = "specimen", state = KnowledgeSubjectState.MissingContent }
                 },
                source = "verification-v3"
            };
            KnowledgeRegistrationOptions options = new KnowledgeRegistrationOptions { source = "verification-v3", priority = int.MaxValue, conflict = KnowledgeRegistrationConflict.Replace };
            try
            {
                Check("v3 domain registration", KnowledgeRegistry.RegisterDomain(registration, options), ref passed, failures);
                bool aggregationRegistered = KnowledgeRegistry.RegisterDomain(AggregationRegistration(), new KnowledgeRegistrationOptions
                {
                    source = "verification-v3-aggregation",
                    priority = int.MaxValue,
                    conflict = KnowledgeRegistrationConflict.Replace
                });
                Check("balanced aggregation domain registration", aggregationRegistered, ref passed, failures);
                KnowledgeContextKey context = new KnowledgeContextKey("verification.region", "alpha");
                if (aggregationRegistered)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        Check("balanced one facet input " + i, SubmitStageKnowledge(pawn, "one", "a", 10f), ref passed, failures);
                        Check("balanced several facet input a " + i, SubmitStageKnowledge(pawn, "many", "a", 10f), ref passed, failures);
                        Check("balanced several facet input b " + i, SubmitStageKnowledge(pawn, "many", "b", 10f), ref passed, failures);
                        Check("balanced empty facet input " + i, SubmitStageKnowledge(pawn, "empty", "a", 10f), ref passed, failures);
                        Check("balanced confidence input a " + i, SubmitStageKnowledge(pawn, "confidence", "a", 10f), ref passed, failures);
                        Check("balanced confidence input b " + i, SubmitStageKnowledge(pawn, "confidence", "b", 10f, i > 0), ref passed, failures);
                    }
                    string oneStage = KnowledgeDiscovery.CurrentStage(AggregationDomainId, "one", pawn);
                    string severalStage = KnowledgeDiscovery.CurrentStage(AggregationDomainId, "many", pawn);
                    string emptyStage = KnowledgeDiscovery.CurrentStage(AggregationDomainId, "empty", pawn);
                    string confidenceStage = KnowledgeDiscovery.CurrentStage(AggregationDomainId, "confidence", pawn);
                    KnowledgeFacetSnapshotV2 confidenceA = KnowledgeQuery.Facet(AggregationDomainId, "confidence", "a", pawn);
                    KnowledgeFacetSnapshotV2 confidenceB = KnowledgeQuery.Facet(AggregationDomainId, "confidence", "b", pawn);
                    Check("balanced equivalent stage eligibility [one=" + oneStage + ",several=" + severalStage + "]", oneStage == "balanced" && severalStage == "exact", ref passed, failures);
                    Check("empty facet cannot advance stage", emptyStage == "base", ref passed, failures);
                    Check("poorly supported facet blocks confidence stage [stage=" + confidenceStage + ",a=" + confidenceA.amount + "/" + confidenceA.confidence + "/" + confidenceA.evidenceCount +
                        ",b=" + confidenceB.amount + "/" + confidenceB.confidence + "/" + confidenceB.evidenceCount + "]", confidenceStage == "base", ref passed, failures);
                    string globalManyStage = KnowledgeDiscovery.CurrentStage(AggregationDomainId, "many", pawn,
                        KnowledgeScope.Personal, context);
                    Check("global stage ignores contextual-only stage [stage=" + globalManyStage + "]", globalManyStage == "exact", ref passed, failures);
                    KnowledgeContextKey aggregationContext = new KnowledgeContextKey("verification.region", "aggregation");
                    for (int i = 0; i < 10; i++)
                    {
                        SubmitStageKnowledge(pawn, "many", "a", 10f, false, aggregationContext);
                        SubmitStageKnowledge(pawn, "many", "b", 10f, false, aggregationContext);
                        SubmitStageKnowledge(pawn, "context-only", "a", 10f, false, aggregationContext);
                        SubmitStageKnowledge(pawn, "context-only", "b", 10f, false, aggregationContext);
                        SubmitStageKnowledge(pawn, "global-context-only", "a", 10f);
                    }
                    string contextualStageBeforeRebuild = KnowledgeDiscovery.CurrentStage(AggregationDomainId, "many", pawn,
                        KnowledgeScope.Personal, aggregationContext);
                    component.RebuildV3Indexes();
                    string contextualStageAfterRebuild = KnowledgeDiscovery.CurrentStage(AggregationDomainId, "many", pawn,
                        KnowledgeScope.Personal, aggregationContext);
                    Check("stage survives save/index reconstruction", contextualStageBeforeRebuild == contextualStageAfterRebuild &&
                        contextualStageAfterRebuild == "contextual", ref passed, failures);
                    Check("contextual evidence does not advance global stage", KnowledgeDiscovery.CurrentStage(AggregationDomainId,
                        "context-only", pawn) == "base" && KnowledgeDiscovery.CurrentStage(AggregationDomainId, "context-only", pawn,
                        KnowledgeScope.Personal, aggregationContext) == "contextual", ref passed, failures);
                    KnowledgeContextKey childAggregationContext = new KnowledgeContextKey("verification.region.child", "aggregation");
                    KnowledgeStageSnapshot exactContextStage = KnowledgeDiscovery.StageSnapshot(AggregationDomainId, "many", pawn,
                        KnowledgeScope.Personal, aggregationContext, KnowledgeContextFallbackMode.ExactOnly);
                    KnowledgeStageSnapshot parentContextStage = KnowledgeDiscovery.StageSnapshot(AggregationDomainId, "many", pawn,
                        KnowledgeScope.Personal, childAggregationContext, KnowledgeContextFallbackMode.ParentThenGlobal);
                    KnowledgeStageSnapshot globalContextStage = KnowledgeDiscovery.StageSnapshot(AggregationDomainId, "many", pawn,
                        KnowledgeScope.Personal, new KnowledgeContextKey("verification.region", "other"),
                        KnowledgeContextFallbackMode.ParentThenGlobal);
                       Check("contextual stage exact parent global lookup [exact=" + exactContextStage.stageId + "/" + exactContextStage.provenance +
                           ",parent=" + parentContextStage.stageId + "/" + parentContextStage.provenance + ",global=" + globalContextStage.stageId + "/" + globalContextStage.provenance + "]", exactContextStage.stageId == "contextual" &&
                          !exactContextStage.usedContextFallback && exactContextStage.provenance == KnowledgeStageProvenance.PersistedExact &&
                          parentContextStage.stageId == "contextual" && parentContextStage.provenance == KnowledgeStageProvenance.InheritedParent &&
                          parentContextStage.usedContextFallback && parentContextStage.resolvedContext.Equals(aggregationContext) &&
                          globalContextStage.stageId == "exact" && globalContextStage.provenance == KnowledgeStageProvenance.InheritedGlobal &&
                          globalContextStage.usedContextFallback &&
                          globalContextStage.resolvedContext.IsEmpty &&
                          !component.StageRecordsV3(AggregationDomainId, "many", pawn, false).Any(item => item.Context.Equals(childAggregationContext)),
                           ref passed, failures);
                       KnowledgeStageSnapshot globalContextSensitiveStage = KnowledgeDiscovery.StageSnapshot(AggregationDomainId,
                           "global-context-only", pawn, KnowledgeScope.Personal,
                           new KnowledgeContextKey("verification.region", "other"), KnowledgeContextFallbackMode.ParentThenGlobal);
                       Check("context-sensitive global fallback keeps provenance", globalContextSensitiveStage.stageId == "global-contextual" &&
                           globalContextSensitiveStage.contextSensitive && globalContextSensitiveStage.usedContextFallback &&
                           globalContextSensitiveStage.provenance == KnowledgeStageProvenance.InheritedGlobal &&
                           globalContextSensitiveStage.resolvedContext.IsEmpty &&
                           globalContextSensitiveStage.requestedContext.Equals(new KnowledgeContextKey("verification.region", "other")),
                           ref passed, failures);
                       KnowledgeStageSnapshot noQualifyingContextStage = KnowledgeDiscovery.StageSnapshot(AggregationDomainId,
                           "context-only", pawn, KnowledgeScope.Personal,
                           new KnowledgeContextKey("verification.region", "unavailable"), KnowledgeContextFallbackMode.ExactOnly);
                       Check("no qualifying contextual stage uses global result", noQualifyingContextStage.stageId == "base" &&
                           noQualifyingContextStage.usedContextFallback && noQualifyingContextStage.resolvedContext.IsEmpty &&
                           noQualifyingContextStage.provenance == KnowledgeStageProvenance.InheritedGlobal,
                           ref passed, failures);
                      component.RemoveStageV3(AggregationDomainId, "many", pawn, false, aggregationContext);
                      KnowledgeStageSnapshot calculatedContextStage = KnowledgeDiscovery.StageSnapshot(AggregationDomainId, "many", pawn,
                          KnowledgeScope.Personal, aggregationContext, KnowledgeContextFallbackMode.ExactOnly);
                      Check("contextual stage calculated without persisted record", calculatedContextStage.stageId == "contextual" &&
                          calculatedContextStage.provenance == KnowledgeStageProvenance.CalculatedExact &&
                          calculatedContextStage.resolvedContext.Equals(aggregationContext) &&
                          !component.StageRecordsV3(AggregationDomainId, "many", pawn, false).Any(item => item.Context.Equals(aggregationContext)),
                          ref passed, failures);
                     KnowledgeBrowserRow contextualBrowserRow = KnowledgeBrowserModels.BuildSubject(new KnowledgeBrowserFilter
                     {
                         domainId = AggregationDomainId,
                         pawn = pawn,
                         scope = KnowledgeScope.Personal,
                         context = childAggregationContext,
                         fallback = KnowledgeContextFallbackMode.ParentThenGlobal
                     }, "many");
                     Check("browser contextual stage presentation [row=" + (contextualBrowserRow == null ? "null" : contextualBrowserRow.currentStageId + "/" + contextualBrowserRow.resolvedContext) + "]", contextualBrowserRow != null &&
                         contextualBrowserRow.currentStageId == "contextual" && contextualBrowserRow.usedContextFallback &&
                         contextualBrowserRow.requestedContext.Equals(childAggregationContext) &&
                         contextualBrowserRow.resolvedContext.Equals(aggregationContext), ref passed, failures);
                 }
                KnowledgeTransactionResult recipeResult = KnowledgeEngine.Submit(new KnowledgeTransaction { source = "v3" }.Add(new KnowledgeObservation
                {
                    observer = pawn,
                    domainId = DomainId,
                    subjectId = "source",
                    observationId = "study",
                    context = context,
                    source = "v3",
                    sourceInstanceId = "study-1",
                    sharedExpertiseNamespaceId = "verification.field",
                    directFamiliarity = 1f,
                    directExpertise = 2f,
                    expertiseTrackId = "field"
                }));
                KnowledgeClaimSnapshot sizeSnapshot = KnowledgeClaimService.Snapshot(DomainId, "source", "biology", "size", pawn,
                    KnowledgeScope.Personal, context);
                Check("multi-facet recipe", recipeResult.success && KnowledgeContextQuery.Facet(DomainId, "source", "identity", context, pawn).amount >= 3f &&
                    KnowledgeContextQuery.Facet(DomainId, "source", "biology", context, pawn).amount >= 7f, ref passed, failures);
                Check("typed claim persistence", sizeSnapshot.observationCount == 1 && sizeSnapshot.value?.numericValue == 42f, ref passed, failures);
                Check("context facet", KnowledgeContextQuery.Facet(DomainId, "source", "biology", context, pawn).amount >= 7f, ref passed, failures);
                Check("shared expertise", KnowledgeSharedExpertiseService.Snapshot("verification.field", pawn).total > 0f, ref passed, failures);
                KnowledgeAccrualStateRecord studyAccrual = component.AccrualRecordsV3().FirstOrDefault(item => item != null &&
                    item.domainId == DomainId && item.policyNamespace == "study");
                Check("one logical recipe event consumes accrual once", studyAccrual != null && studyAccrual.count == 1,
                    ref passed, failures);
                Check("global personal claim apply/query", Observe(pawn, "biology", KnowledgeContextKey.Empty, false, true, null, "global-personal", new KnowledgeMeasurement
                {
                    claimId = "size",
                    value = KnowledgeClaimValue.Float(11f)
                }).success && KnowledgeClaimService.Snapshot(DomainId, "source", "biology", "size", pawn,
                    KnowledgeScope.Personal, KnowledgeContextKey.Empty).observationCount > 0, ref passed, failures);
                Check("global colony claim apply/query", Observe(null, "biology", KnowledgeContextKey.Empty, true, true, null, "global-colony", new KnowledgeMeasurement
                {
                    claimId = "size",
                    value = KnowledgeClaimValue.Float(12f),
                    scope = KnowledgeScope.Colony
                }).success && KnowledgeClaimService.Snapshot(DomainId, "source", "biology", "size", null,
                    KnowledgeScope.Colony, KnowledgeContextKey.Empty).observationCount > 0, ref passed, failures);
                Check("global claims enumerate", KnowledgeClaimService.ForSubject(DomainId, "source", "biology", pawn,
                    KnowledgeScope.Personal, KnowledgeContextKey.Empty).Any(item => item.claimId == "size" && item.observationCount > 0) &&
                    KnowledgeQuery.Claims(DomainId, "source", "biology", pawn, KnowledgeScope.Personal,
                        KnowledgeContextKey.Empty).Any(item => item.claimId == "size" && item.observationCount > 0), ref passed, failures);
                KnowledgeContextKey parentContext = new KnowledgeContextKey("verification.region", "shared");
                KnowledgeContextKey childContext = new KnowledgeContextKey("verification.region.child", "shared");
                Observe(pawn, "biology", parentContext, false, true, null, "parent-claim", new KnowledgeMeasurement
                {
                    claimId = "size",
                    value = KnowledgeClaimValue.Float(55f)
                });
                Observe(pawn, "biology", parentContext, false, true, null, "parent-facet");
                KnowledgeTransactionResult browserIdentity = Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null, "browser-global-identity");
                KnowledgeClaimSnapshot exactChild = KnowledgeClaimService.Snapshot(DomainId, "source", "biology", "size", pawn,
                    KnowledgeScope.Personal, childContext, KnowledgeContextFallbackMode.ExactOnly);
                KnowledgeClaimSnapshot fallbackChild = KnowledgeClaimService.Snapshot(DomainId, "source", "biology", "size", pawn,
                    KnowledgeScope.Personal, childContext, KnowledgeContextFallbackMode.ParentThenGlobal);
                Check("contextual exact and parent fallback", exactChild.observationCount == 0 && exactChild.context.Equals(childContext) && fallbackChild.observationCount > 0 &&
                    fallbackChild.context.Equals(parentContext) &&
                    KnowledgeContextQuery.Facet(DomainId, "source", "biology", childContext, pawn,
                        KnowledgeScope.Personal, KnowledgeContextFallbackMode.ParentThenGlobal).amount > 0f &&
                    KnowledgeContextQuery.Facet(DomainId, "source", "biology", childContext, pawn,
                        KnowledgeScope.Personal, KnowledgeContextFallbackMode.ExactOnly).amount == 0f, ref passed, failures);
                Window_KnowledgeBrowser browserWindow = new Window_KnowledgeBrowser(DomainId, pawn, "source", childContext);
                KnowledgeBrowserRow browserRow = KnowledgeBrowserModels.BuildSubject(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = childContext,
                    fallback = KnowledgeContextFallbackMode.ParentThenGlobal
                }, "source");
                Check("browser Open context reaches model [identity=" + browserIdentity.success + ",window=" + browserWindow.RequestedContext +
                    ",row=" + (browserRow == null ? "null" : browserRow.requestedContext + "/" + browserRow.usedContextFallback + "/" + browserRow.displayLabel) +
                    ",contextLabel=" + KnowledgeBrowserLabels.Context(childContext) + "]", browserIdentity.success && browserWindow.RequestedContext.Equals(childContext) && browserRow != null &&
                    browserRow.requestedContext.Equals(childContext) && browserRow.usedContextFallback &&
                    browserRow.displayLabel == "Source" && KnowledgeBrowserLabels.Context(childContext) != childContext.stableId,
                    ref passed, failures);
                IReadOnlyList<KnowledgeBrowserRow> visibleRows = KnowledgeBrowserModels.Build(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = childContext,
                    fallback = KnowledgeContextFallbackMode.ParentThenGlobal
                });
                Check("browser visibility and labels [row=" + (browserRow == null ? "null" : string.Join(",", browserRow.applicableFacets.Select(item => item.id)) + "/" + string.Join(",", browserRow.claims.Select(item => item.claimId))) +
                    ",rows=" + string.Join(",", visibleRows.Select(item => item.subject.id)) + "]", browserRow != null && visibleRows.Any(item => item.subject.id == "source") &&
                    !visibleRows.Any(item => item.subject.id == "hidden-subject") &&
                    !visibleRows.Any(item => item.subject.id == "archived-subject" || item.subject.id == "missing-subject") &&
                    !browserRow.applicableFacets.Any(item => item.id == "secret") &&
                    !browserRow.claims.Any(item => item.claimId == "traits") &&
                    KnowledgeBrowserLabels.Claim(KnowledgeRegistry.Schema(DomainId), "size") == "Size",
                    ref passed, failures);
                KnowledgeBrowserRow sourceWithHidden = KnowledgeBrowserModels.BuildSubject(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = childContext,
                    includeHidden = true
                }, "source");
                Check("hidden facet search does not disclose", !KnowledgeBrowserModels.Build(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = childContext,
                    search = "secret"
                }).Any() && sourceWithHidden != null && !sourceWithHidden.applicableFacets.Any(item => item.id == "secret"), ref passed, failures);
                IReadOnlyList<KnowledgeBrowserRow> developerRows = KnowledgeBrowserModels.Build(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = childContext,
                    fallback = KnowledgeContextFallbackMode.ParentThenGlobal,
                    developerMode = true
                });
                Check("browser developer visibility [rows=" + string.Join(",", developerRows.Select(item => item.subject.id)) +
                    ",sourceFacets=" + string.Join(",", developerRows.FirstOrDefault(item => item.subject.id == "source")?.applicableFacets.Select(item => item.id) ?? Enumerable.Empty<string>()) + "]", developerRows.Any(item => item.subject.id == "hidden-subject") &&
                    developerRows.Any(item => item.applicableFacets.Any(facet => facet.id == "secret")), ref passed, failures);
                KnowledgeTransactionResult secretObservation = KnowledgeEngine.Submit(new KnowledgeObservation
                {
                    observer = pawn,
                    domainId = DomainId,
                    subjectId = "source",
                    facetId = "secret",
                    directKnowledge = 100f,
                    disposition = KnowledgeEvidenceDisposition.Supporting,
                    source = "v3-verification",
                    sourceInstanceId = "reveal-secret"
                });
                KnowledgeBrowserRow revealedRow = KnowledgeBrowserModels.BuildSubject(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = childContext,
                    fallback = KnowledgeContextFallbackMode.ParentThenGlobal
                }, "source");
                KnowledgeBrowserRow developerHiddenRow = KnowledgeBrowserModels.BuildSubject(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = childContext,
                    fallback = KnowledgeContextFallbackMode.ParentThenGlobal,
                    developerMode = true,
                    includeHidden = true
                }, "source");
                KnowledgeBrowserRow hiddenSubjectRow = KnowledgeBrowserModels.BuildSubject(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = childContext
                }, "hidden-subject");
                Check("revealed hidden facet is normally visible [submit=" + secretObservation.success + ",row=" + (revealedRow == null ? "null" : string.Join(",", revealedRow.applicableFacets.Select(item => item.id))) +
                    ",hiddenRow=" + (hiddenSubjectRow == null ? "null" : string.Join(",", hiddenSubjectRow.applicableFacets.Select(item => item.id))) + "]", secretObservation.success && revealedRow != null &&
                    revealedRow.applicableFacets.Any(item => item.id == "secret") &&
                    KnowledgeBrowserLabels.Facet(revealedRow.applicableFacets.First(item => item.id == "secret")) == "Secret" &&
                    (hiddenSubjectRow == null || !hiddenSubjectRow.applicableFacets.Any(item => item.id == "secret")),
                    ref passed, failures);
                Check("developer hidden facet override remains explicit [row=" + (developerHiddenRow == null ? "null" : string.Join(",", developerHiddenRow.applicableFacets.Select(item => item.id))) + "]", developerHiddenRow != null &&
                    developerHiddenRow.applicableFacets.Any(item => item.id == "secret"), ref passed, failures);
                KnowledgeContextKey hiddenClaimContext = new KnowledgeContextKey("verification.region", "hidden-claim");
                KnowledgeTransactionResult hiddenClaimObservation = KnowledgeEngine.Submit(new KnowledgeObservation
                {
                    observer = pawn,
                    domainId = DomainId,
                    subjectId = "source",
                    facetId = "secret",
                    context = hiddenClaimContext,
                    directKnowledge = 1f,
                    disposition = KnowledgeEvidenceDisposition.Supporting,
                    claimMeasurements = new[] { new KnowledgeMeasurement
                    {
                        claimId = "hidden-secret",
                        facetId = "secret",
                        value = KnowledgeClaimValue.Float(7f)
                    } },
                    source = "v3-verification",
                    sourceInstanceId = "hidden-claim-context"
                });
                KnowledgeContextKey hiddenRelationContext = new KnowledgeContextKey("verification.region", "hidden-relation");
                bool hiddenRelation = KnowledgeRelationService.Add(new KnowledgeSubjectRelation
                {
                    domainId = DomainId,
                    fromSubjectId = "source",
                    toDomainId = DomainId,
                    toSubjectId = "child",
                    relationTypeId = "verification.parent",
                    context = hiddenRelationContext,
                    revealed = false,
                    confidence = 1f
                });
                IReadOnlyList<KnowledgeContextKey> hiddenContextOptions = KnowledgeBrowserModels.ContextOptions(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = hiddenClaimContext
                }, "source");
                Check("hidden claim and relationship do not reveal context [submit=" + hiddenClaimObservation.success + ",relation=" + hiddenRelation +
                    ",options=" + string.Join(",", hiddenContextOptions.Select(item => item.ToString())) + "]", hiddenClaimObservation.success && hiddenRelation &&
                    !hiddenContextOptions.Contains(hiddenClaimContext) && !hiddenContextOptions.Contains(hiddenRelationContext),
                    ref passed, failures);
                KnowledgeContextKey revealedFacetContext = new KnowledgeContextKey("verification.region", "revealed-facet-only");
                KnowledgeTransactionResult revealedFacetObservation = KnowledgeEngine.Submit(new KnowledgeObservation
                {
                    observer = pawn,
                    domainId = DomainId,
                    subjectId = "source",
                    facetId = "secret",
                    context = revealedFacetContext,
                    directKnowledge = 100f,
                    disposition = KnowledgeEvidenceDisposition.Supporting,
                    source = "v3-verification",
                    sourceInstanceId = "revealed-facet-context"
                });
                IReadOnlyList<KnowledgeContextKey> revealedContextOptions = KnowledgeBrowserModels.ContextOptions(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = revealedFacetContext
                }, "source");
                Check("revealed facet authorizes context [submit=" + revealedFacetObservation.success + ",options=" + string.Join(",", revealedContextOptions.Select(item => item.ToString())) + "]", revealedFacetObservation.success &&
                    revealedContextOptions.Contains(revealedFacetContext), ref passed, failures);
                KnowledgeFacetSnapshotV2 blockedContext = KnowledgeContextQuery.Facet(DomainId, "source", "biology",
                    new KnowledgeContextKey("verification.region.nofallback", "shared"), pawn, KnowledgeScope.Personal,
                    KnowledgeContextFallbackMode.ParentThenGlobal);
                Check("context allowFallback control", blockedContext.amount == 0f, ref passed, failures);
                IReadOnlyList<KnowledgeContextKey> contextOptions = KnowledgeBrowserModels.ContextOptions(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = childContext
                }, "source");
                Check("browser context selector and labels [options=" + string.Join(",", contextOptions.Select(item => item.ToString())) + "]", contextOptions.Contains(KnowledgeContextKey.Empty) &&
                    contextOptions.Contains(childContext) &&
                    KnowledgeBrowserLabels.ContextValue(childContext, DomainId, "source", pawn, KnowledgeScope.Personal) == "Shared region" &&
                    KnowledgeBrowserLabels.ContextSelection(childContext, DomainId, "source", pawn, KnowledgeScope.Personal).Contains("Shared region"),
                    ref passed, failures);
                VerificationContextPresentationProvider.ResetCounters();
                KnowledgeContextRegistry.RegisterPresentationProvider("verification.region",
                    new VerificationContextPresentationProvider(), true);
                IReadOnlyList<KnowledgeContextKey> orderedContextOptions = KnowledgeBrowserModels.ContextOptions(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = childContext
                }, "source");
                int providerCallsAfterFirstOptions = VerificationContextPresentationProvider.KnownContextsCalls;
                IReadOnlyList<KnowledgeContextKey> repeatedContextOptions = KnowledgeBrowserModels.ContextOptions(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = childContext
                }, "source");
                Check("context provider enumeration is cached and bounded [calls=" + providerCallsAfterFirstOptions + ",repeat=" + VerificationContextPresentationProvider.KnownContextsCalls +
                    ",first=" + string.Join(",", orderedContextOptions.Select(item => item.ToString())) + ",second=" + string.Join(",", repeatedContextOptions.Select(item => item.ToString())) + "]", providerCallsAfterFirstOptions > 0 &&
                    providerCallsAfterFirstOptions <= 3 &&
                    VerificationContextPresentationProvider.KnownContextsCalls == providerCallsAfterFirstOptions &&
                    orderedContextOptions.SequenceEqual(repeatedContextOptions), ref passed, failures);
                Check("throwing context provider is isolated", KnowledgeContextRegistry.ValueLabel(
                    new KnowledgeContextKey("verification.region.throwing", "any"), DomainId, "source", pawn,
                    KnowledgeScope.Personal) == null, ref passed, failures);
                VerificationContextPresentationProvider.EmitLarge = true;
                VerificationContextPresentationProvider.ResetCounters();
                KnowledgeContextRegistry.RegisterPresentationProvider("verification.region",
                    new VerificationContextPresentationProvider(), true);
                IReadOnlyList<KnowledgeContextKey> largeProviderContexts = KnowledgeContextRegistry.KnownContexts(DomainId, "source",
                    pawn, KnowledgeScope.Personal);
                Check("large context provider is safely bounded", largeProviderContexts.Count <= 512 &&
                    VerificationContextPresentationProvider.KnownContextsCalls <= 3, ref passed, failures);
                VerificationContextPresentationProvider.EmitLarge = false;
                KnowledgeContextRegistry.RegisterPresentationProvider("verification.region",
                    new VerificationContextPresentationProvider(), true);
                KnowledgeContextKey unauthorizedContext = new KnowledgeContextKey("verification.region", "not-authorized");
                Check("unauthorized context never exposes stable id", KnowledgeContextRegistry.ValueLabel(unauthorizedContext,
                    DomainId, "source", pawn, KnowledgeScope.Personal) == null &&
                    KnowledgeBrowserLabels.ContextValue(unauthorizedContext, DomainId, "source", pawn, KnowledgeScope.Personal) !=
                    unauthorizedContext.stableId, ref passed, failures);
                IReadOnlyList<KnowledgeContextKey> cachedContextOptionsBefore = browserWindow.ContextOptionsForVerification(KnowledgeRegistry.Schema(DomainId));
                KnowledgeContextKey cacheContext = new KnowledgeContextKey("verification.region", "cache-only");
                Check("context option cache invalidates on new knowledge", Observe(pawn, "biology", cacheContext, false, true,
                    null, "cache-context").success &&
                    browserWindow.ContextOptionsForVerification(KnowledgeRegistry.Schema(DomainId)).Count > cachedContextOptionsBefore.Count,
                    ref passed, failures);
                Check("global claim requirement", KnowledgeRequirementService.Evaluate(new KnowledgeRequirement
                {
                    kind = KnowledgeRequirementKind.ClaimExists,
                    facetId = "biology",
                    claimId = "size"
                }, DomainId, "source", pawn, KnowledgeScope.Personal, KnowledgeContextKey.Empty), ref passed, failures);
                KnowledgeClaimSnapshot globalColonyClaim = KnowledgeClaimService.Snapshot(DomainId, "source", "biology", "size", null,
                    KnowledgeScope.Colony, KnowledgeContextKey.Empty);
                Check("global colony claim requirement [count=" + globalColonyClaim.observationCount + ",value=" + globalColonyClaim.value?.numericValue + ",scope=" + globalColonyClaim.scope + "]", KnowledgeRequirementService.Evaluate(new KnowledgeRequirement
                {
                    kind = KnowledgeRequirementKind.ClaimValue,
                    facetId = "biology",
                    claimId = "size",
                    colony = true,
                    comparison = KnowledgeRequirementComparison.GreaterOrEqual,
                    value = KnowledgeClaimValue.Float(12f)
                }, DomainId, "source", pawn, KnowledgeScope.Personal, KnowledgeContextKey.Empty), ref passed, failures);
                Check("claim requirement fallback control", KnowledgeRequirementService.Evaluate(new KnowledgeRequirement
                {
                    kind = KnowledgeRequirementKind.ClaimExists,
                    facetId = "biology",
                    claimId = "size",
                    contextTypeId = childContext.typeId,
                    contextId = childContext.stableId,
                    allowContextFallback = true
                }, DomainId, "source", pawn, KnowledgeScope.Personal, KnowledgeContextKey.Empty) &&
                    !KnowledgeRequirementService.Evaluate(new KnowledgeRequirement
                    {
                        kind = KnowledgeRequirementKind.ClaimExists,
                        facetId = "biology",
                        claimId = "size",
                        contextTypeId = childContext.typeId,
                        contextId = childContext.stableId,
                        allowContextFallback = false
                    }, DomainId, "source", pawn, KnowledgeScope.Personal, KnowledgeContextKey.Empty), ref passed, failures);
                Check("requirement maximum", KnowledgeRequirementService.Evaluate(new KnowledgeRequirement
                {
                    kind = KnowledgeRequirementKind.Knowledge,
                    facetId = "biology",
                    minimum = 0f,
                    maximum = 2f
                }, DomainId, "source", pawn, KnowledgeScope.Personal, KnowledgeContextKey.Empty) &&
                    !KnowledgeRequirementService.Evaluate(new KnowledgeRequirement
                    {
                        kind = KnowledgeRequirementKind.Knowledge,
                        facetId = "biology",
                        minimum = 0f,
                        maximum = 0.1f
                    }, DomainId, "source", pawn, KnowledgeScope.Personal, KnowledgeContextKey.Empty), ref passed, failures);
                float before = KnowledgeQuery.Facet(DomainId, "source", "biology", pawn).amount;
                KnowledgeTransactionResult invalid = KnowledgeEngine.Submit(new KnowledgeTransaction().Add(new KnowledgeObservation
                {
                    observer = pawn,
                    domainId = DomainId,
                    subjectId = "source",
                    facetId = "biology",
                    directKnowledge = 99f,
                    claimMeasurements = new[] { new KnowledgeMeasurement { claimId = "size", value = KnowledgeClaimValue.Float(float.NaN) } }
                }));
                Check("claim batch atomicity", !invalid.success && Math.Abs(KnowledgeQuery.Facet(DomainId, "source", "biology", pawn).amount - before) < 0.001f, ref passed, failures);
                KnowledgeContextKey discardedContext = new KnowledgeContextKey("verification.region", "discarded");
                int globalClaimsBeforeDiscard = KnowledgeClaimService.Snapshot(DomainId, "source", "biology", "size", pawn,
                    KnowledgeScope.Personal, KnowledgeContextKey.Empty).observationCount;
                KnowledgeTransactionResult discarded = Observe(pawn, "biology", discardedContext, false, true, null,
                    "contextless-1", null, "contextless", new[] { pawn });
                Check("propagateContext discard", discarded.success &&
                    KnowledgeContextQuery.Facet(DomainId, "source", "biology", discardedContext, pawn, KnowledgeScope.Personal,
                        KnowledgeContextFallbackMode.ExactOnly).amount == 0f &&
                    KnowledgeQuery.Facet(DomainId, "source", "biology", pawn).amount > 0f &&
                    KnowledgeClaimService.Snapshot(DomainId, "source", "biology", "size", pawn,
                        KnowledgeScope.Personal, KnowledgeContextKey.Empty).observationCount > globalClaimsBeforeDiscard &&
                    KnowledgeClaimService.Snapshot(DomainId, "source", "biology", "size", pawn,
                        KnowledgeScope.Personal, discardedContext, KnowledgeContextFallbackMode.ExactOnly).observationCount == 0,
                    ref passed, failures);

                float cooldownBefore = KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount;
                Check("cooldown without uniqueness", Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null,
                    "cooldown-1", null, "cooldown").success && Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null,
                    "cooldown-2", null, "cooldown").success &&
                    Math.Abs(KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount - cooldownBefore - 1f) < 0.001f,
                    ref passed, failures);
                float dailyBefore = KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount;
                Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null, "daily-1", null, "daily");
                Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null, "daily-2", null, "daily");
                Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null, "daily-3", null, "daily");
                Check("daily cap boundary", Math.Abs(KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount - dailyBefore - 2f) < 0.001f,
                    ref passed, failures);
                float lifetimeBefore = KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount;
                Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null, "lifetime-1", null, "lifetime");
                Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null, "lifetime-2", null, "lifetime");
                Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null, "lifetime-3", null, "lifetime");
                Check("lifetime cap boundary", Math.Abs(KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount - lifetimeBefore - 2f) < 0.001f,
                    ref passed, failures);
                float noOpBefore = KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount;
                Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null, "no-op-1", null, "no-op");
                Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null, "no-op-2", null, "no-op");
                Check("blocked no-op does not accrue", Math.Abs(KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount - noOpBefore) < 0.001f &&
                    !component.AccrualRecordsV3().Any(item => item != null && item.domainId == DomainId && item.policyNamespace == "no-op"),
                    ref passed, failures);
                float diminishingBefore = KnowledgeQuery.Facet(DomainId, "source", "biology", pawn).amount;
                Observe(pawn, "biology", KnowledgeContextKey.Empty, false, true, null, "diminishing-1", null, "diminishing");
                Observe(pawn, "biology", KnowledgeContextKey.Empty, false, true, null, "diminishing-2", null, "diminishing");
                float diminishingAfter = KnowledgeQuery.Facet(DomainId, "source", "biology", pawn).amount;
                Check("diminishing returns without uniqueness [before=" + diminishingBefore + ",after=" + diminishingAfter + "]", Math.Abs(diminishingAfter - diminishingBefore - 1.5f) < 0.001f,
                    ref passed, failures);
                float outcomeBefore = KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount;
                Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null, "first-success", null, "first-outcome");
                Observe(pawn, "identity", KnowledgeContextKey.Empty, false, false, null, "first-failure", null, "first-outcome");
                Check("first success and failure bonuses", Math.Abs(KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount - outcomeBefore - 5f) < 0.001f,
                    ref passed, failures);
                KnowledgeContextKey distinctA = new KnowledgeContextKey("verification.region", "distinct-a");
                KnowledgeContextKey distinctB = new KnowledgeContextKey("verification.region", "distinct-b");
                Func<float> distinctAmount = () => KnowledgeContextQuery.Facet(DomainId, "source", "identity", distinctA,
                    pawn, KnowledgeScope.Personal, KnowledgeContextFallbackMode.ExactOnly).amount +
                    KnowledgeContextQuery.Facet(DomainId, "source", "identity", distinctB,
                        pawn, KnowledgeScope.Personal, KnowledgeContextFallbackMode.ExactOnly).amount;
                float distinctBefore = distinctAmount();
                Observe(pawn, "identity", distinctA, false, true, "specimen-a", "distinct-1", null, "distinct");
                Observe(pawn, "identity", distinctB, false, true, "specimen-b", "distinct-2", null, "distinct");
                float afterIndependent = distinctAmount();
                Observe(pawn, "identity", distinctB, false, true, "specimen-b", "distinct-2", null, "distinct");
                float afterRepeated = distinctAmount();
                Check("different specimen/context/source behavior [before=" + distinctBefore + ",independent=" + afterIndependent + ",repeated=" + afterRepeated + "]", afterIndependent - distinctBefore > 7.9f &&
                    afterRepeated - afterIndependent > 0.49f && afterRepeated - afterIndependent < 0.51f, ref passed, failures);
                int stateBefore = component.AccrualRecordsV3().Count(item => item != null && item.domainId == DomainId && item.policyNamespace == "bounded");
                Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null, "bounded-1", null, "bounded");
                Observe(pawn, "biology", KnowledgeContextKey.Empty, false, true, null, "bounded-2", null, "bounded");
                Observe(pawn, "identity", KnowledgeContextKey.Empty, true, true, null, "bounded-3", null, "bounded");
                int stateAfter = component.AccrualRecordsV3().Count(item => item != null && item.domainId == DomainId && item.policyNamespace == "bounded");
                List<string> boundedKeys = component.AccrualRecordsV3().Where(item => item != null && item.domainId == DomainId && item.policyNamespace == "bounded")
                    .Select(item => item.key).OrderBy(item => item, StringComparer.Ordinal).ToList();
                component.RebuildV3Indexes();
                List<string> rebuiltBoundedKeys = component.AccrualRecordsV3().Where(item => item != null && item.domainId == DomainId && item.policyNamespace == "bounded")
                    .Select(item => item.key).OrderBy(item => item, StringComparer.Ordinal).ToList();
                Check("deterministic stateLimit enforcement", stateBefore == 0 && stateAfter == 2 && boundedKeys.SequenceEqual(rebuiltBoundedKeys), ref passed, failures);
                foreach (KnowledgeAccrualStateRecord existingCooldown in component.AccrualRecordsV3().Where(item => item != null &&
                    item.domainId == DomainId && item.policyNamespace == "cooldown").ToList())
                    component.RemoveAccrualV3(existingCooldown.key);
                KnowledgeAccrualStateRecord legacyAccrual = component.AccrualV3("legacy-accrual", true);
                legacyAccrual.domainId = DomainId;
                legacyAccrual.subjectId = "source";
                legacyAccrual.observationId = "cooldown";
                legacyAccrual.policyNamespace = "cooldown";
                legacyAccrual.count = -5;
                legacyAccrual.dailyCount = 99;
                legacyAccrual.sourceInstanceIds = new List<string> { "duplicate", "duplicate", null };
                legacyAccrual.contextKeys = new List<string> { "<global>", "<global>" };
                string legacyCapKey = string.Join("\n", DomainId, "source", "identity", string.Empty);
                KnowledgeAccrualStateRecord oldCap = component.AccrualV3(legacyCapKey, true);
                oldCap.domainId = DomainId;
                oldCap.subjectId = "source";
                oldCap.count = 2;
                oldCap.dailyCount = 2;
                oldCap.lastTick = Find.TickManager?.TicksGame ?? 0;
                string provableLegacyKey = string.Join("\n", DomainId, "source", "identity", "legacy-source");
                KnowledgeAccrualStateRecord provableLegacy = component.AccrualV3(provableLegacyKey, true);
                provableLegacy.domainId = DomainId;
                provableLegacy.subjectId = "source";
                provableLegacy.observationId = "legacy-unique";
                provableLegacy.policyNamespace = "legacy-unique";
                provableLegacy.facetId = "identity";
                provableLegacy.pawnId = pawn?.thingIDNumber ?? 0;
                provableLegacy.sourceInstanceId = "legacy-source";
                provableLegacy.count = 1;
                component.RebuildV3Indexes();
                KnowledgeAccrualStateRecord rebuiltLegacy = component.AccrualRecordsV3().FirstOrDefault(item => item != null &&
                    item.domainId == DomainId && item.policyNamespace == "cooldown" && item.subjectId == "source");
                Check("save/index rebuild normalization [record=" + (rebuiltLegacy == null ? "null" : rebuiltLegacy.count + "/" + rebuiltLegacy.dailyCount + "/" + rebuiltLegacy.sourceInstanceIds.Count + "/" + rebuiltLegacy.contextKeys.Count) + "]", rebuiltLegacy != null && rebuiltLegacy.count == 0 &&
                    rebuiltLegacy.dailyCount == 0 && rebuiltLegacy.sourceInstanceIds.Count == 1 && rebuiltLegacy.contextKeys.Count == 1,
                    ref passed, failures);
                KnowledgeObservationDef provableDefinition = KnowledgeRegistry.Schema(DomainId)?.Observation("legacy-unique");
                string provableModernKey = KnowledgeAccrualService.BuildKey(DomainId, "source", "identity", false,
                    pawn?.thingIDNumber ?? 0, "legacy-source", "<global>", provableDefinition?.accrualPolicy, "legacy-unique");
                KnowledgeAccrualStateRecord rebuiltProvable = component.AccrualRecordsV3().FirstOrDefault(item => item != null &&
                    item.legacyKey == provableLegacyKey);
                Check("legacy accrual migration canonicalizes provable ownership", rebuiltProvable != null &&
                    rebuiltProvable.key == provableModernKey && rebuiltProvable.ownershipMetadataComplete, ref passed, failures);
                Check("legacy alias registration", KnowledgeRegistry.RegisterDomainAlias("verification-old", DomainId) &&
                    KnowledgeRegistry.RegisterSubjectAlias(DomainId, "old-source", "source"), ref passed, failures);
                string aliasedLegacyKey = string.Join("\n", "verification-old", "old-source", "identity", "alias-source");
                KnowledgeAccrualStateRecord aliasedLegacy = component.AccrualV3(aliasedLegacyKey, true);
                aliasedLegacy.domainId = "verification-old";
                aliasedLegacy.subjectId = "old-source";
                aliasedLegacy.observationId = "legacy-unique";
                aliasedLegacy.policyNamespace = "legacy-unique";
                aliasedLegacy.facetId = "identity";
                aliasedLegacy.sourceInstanceId = "alias-source";
                aliasedLegacy.pawnId = pawn?.thingIDNumber ?? 0;
                aliasedLegacy.count = 1;
                aliasedLegacy.dailyCount = 1;
                component.RebuildV3Indexes();
                KnowledgeAccrualStateRecord canonicalAliasState = component.AccrualRecordsV3().FirstOrDefault(item => item != null &&
                    item.domainId == DomainId && item.subjectId == "source" && item.policyNamespace == "legacy-unique" &&
                    item.sourceInstanceId == "alias-source");
                int aliasCountBefore = canonicalAliasState?.count ?? -1;
                KnowledgeTransactionResult aliasObservation = Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true,
                    null, "alias-source", null, "legacy-unique");
                component.RebuildV3Indexes();
                KnowledgeAccrualStateRecord canonicalAliasAfter = component.AccrualRecordsV3().FirstOrDefault(item => item != null &&
                    item.domainId == DomainId && item.subjectId == "source" && item.policyNamespace == "legacy-unique" &&
                    item.sourceInstanceId == "alias-source");
                Check("legacy alias accrual preserves canonical cap state [submit=" + aliasObservation.success + ",before=" + aliasCountBefore + ",after=" + (canonicalAliasAfter == null ? "null" : canonicalAliasAfter.count + "/" + canonicalAliasAfter.legacyKey) + "]", aliasObservation.success && aliasCountBefore == 1 &&
                    canonicalAliasAfter != null && canonicalAliasAfter.count == 2 && canonicalAliasAfter.legacyKey == aliasedLegacyKey,
                    ref passed, failures);
                string sharedLegacyKey = string.Join("\n", "verification-old", "old-source", "identity", string.Empty);
                KnowledgeAccrualStateRecord sharedLegacy = component.AccrualV3(sharedLegacyKey, true);
                sharedLegacy.domainId = "verification-old";
                sharedLegacy.subjectId = "old-source";
                sharedLegacy.observationId = "legacy-shared";
                sharedLegacy.policyNamespace = "legacy-shared";
                sharedLegacy.facetId = "identity";
                sharedLegacy.count = 0;
                component.RebuildV3Indexes();
                KnowledgeTransaction sharedTransaction = new KnowledgeTransaction { source = "v3-verification" };
                sharedTransaction.Add(new KnowledgeObservation
                {
                    observer = pawn,
                    domainId = DomainId,
                    subjectId = "source",
                    facetId = "identity",
                    observationId = "legacy-shared",
                    source = "v3-verification",
                    sourceInstanceId = "shared-source",
                    directKnowledge = 1f
                });
                sharedTransaction.Add(new KnowledgeObservation
                {
                    observer = pawn,
                    domainId = DomainId,
                    subjectId = "source",
                    facetId = "identity",
                    observationId = "legacy-shared",
                    source = "v3-verification",
                    sourceInstanceId = "shared-source",
                    targetColony = true,
                    directKnowledge = 1f
                });
                KnowledgeTransactionResult sharedResult = KnowledgeEngine.Submit(sharedTransaction);
                component.RebuildV3Indexes();
                KnowledgeAccrualStateRecord sharedAfter = component.AccrualRecordsV3().FirstOrDefault(item => item != null &&
                    item.legacyKey == sharedLegacyKey);
                Check("shared legacy reservation is consumed once per transaction", sharedResult.success && sharedAfter != null &&
                    sharedAfter.count == 1 && KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount > 0f,
                    ref passed, failures);
                KnowledgeAccrualStateRecord rebuiltOldCap = component.AccrualRecordsV3().FirstOrDefault(item => item != null &&
                    item.legacyKey == legacyCapKey);
                Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null, null, null, "legacy-cap");
                int oldCapCount = rebuiltOldCap?.count ?? -1;
                component.RebuildV3Indexes();
                component.RebuildV3Indexes();
                KnowledgeAccrualStateRecord idempotentOldCap = component.AccrualRecordsV3().FirstOrDefault(item => item != null &&
                    item.legacyKey == legacyCapKey);
                Check("legacy accrual migration preserves caps and is idempotent", rebuiltOldCap != null && oldCapCount == 2 &&
                    idempotentOldCap != null && idempotentOldCap.count == 2 && idempotentOldCap.keyFormatVersion == 1 &&
                    idempotentOldCap.outcomeHistoryComplete == false, ref passed, failures);
                KnowledgeObservationDef legacyCapDefinition = KnowledgeRegistry.Schema(DomainId)?.Observation("legacy-cap");
                string duplicateModernKey = KnowledgeAccrualService.BuildKey(DomainId, "source", "identity", false,
                    pawn?.thingIDNumber ?? 0, null, "<global>", legacyCapDefinition?.accrualPolicy, "legacy-cap");
                List<KnowledgeAccrualStateRecord> rawAccrual = component.AccrualRecordsV3() as List<KnowledgeAccrualStateRecord>;
                if (rawAccrual != null)
                {
                    rawAccrual.Add(new KnowledgeAccrualStateRecord { key = duplicateModernKey, domainId = DomainId,
                        subjectId = "source", observationId = "legacy-cap", policyNamespace = "legacy-cap", facetId = "identity",
                        count = 1, dailyCount = 1, keyFormatVersion = 2, outcomeHistoryComplete = true, ownershipMetadataComplete = true });
                    rawAccrual.Add(new KnowledgeAccrualStateRecord { key = duplicateModernKey, domainId = DomainId,
                        subjectId = "source", observationId = "legacy-cap", policyNamespace = "legacy-cap", facetId = "identity",
                        count = 4, dailyCount = 4, keyFormatVersion = 2, outcomeHistoryComplete = true, ownershipMetadataComplete = true });
                }
                component.RebuildV3Indexes();
                Check("legacy migration deduplicates bounded state", component.AccrualRecordsV3().Count(item => item != null &&
                    item.key == duplicateModernKey) == 1 && component.AccrualRecordsV3().FirstOrDefault(item => item?.key == duplicateModernKey)?.count == 4,
                    ref passed, failures);
                Check("accrual ownership metadata", component.AccrualRecordsV3().Where(item => item != null && item.domainId == DomainId &&
                    item.policyNamespace != "bounded" && item.key != "legacy-accrual").All(item => !item.subjectId.NullOrEmpty() &&
                    !item.observationId.NullOrEmpty() && !item.facetId.NullOrEmpty() && item.domainId == DomainId), ref passed, failures);
                Check("witness learning", KnowledgeEngine.Submit(new KnowledgeObservation
                {
                    observer = pawn,
                    domainId = DomainId,
                    subjectId = "source",
                    facetId = "identity",
                    directKnowledge = 2f,
                    witnesses = new[] { pawn },
                    witnessDistribution = new KnowledgeWitnessDistribution { policy = KnowledgeWitnessDistributionPolicy.WitnessesReduced, includeObserver = true, efficiency = 0.5f }
                }).success, ref passed, failures);
                Check("milestone confirmation", KnowledgeMilestoneService.Confirm(DomainId, "source", "progress", "established", pawn) &&
                    KnowledgeMilestoneService.IsCompleted(DomainId, "source", "progress", "established", pawn), ref passed, failures);
                KnowledgeMilestoneService.Confirm(DomainId, "source", "progress", "established", pawn, context);
                bool structuralRelationAdded = KnowledgeRelationService.Add(new KnowledgeSubjectRelation
                {
                    domainId = DomainId,
                    fromSubjectId = "child",
                    toDomainId = DomainId,
                    toSubjectId = "source",
                    relationTypeId = "verification.parent",
                    confidence = 1f
                });
                Check("structural relation [added=" + structuralRelationAdded + "]", structuralRelationAdded, ref passed, failures);
                bool structuralCycleAdded = KnowledgeRelationService.Add(new KnowledgeSubjectRelation
                {
                    domainId = DomainId,
                    fromSubjectId = "source",
                    toDomainId = DomainId,
                    toSubjectId = "child",
                    relationTypeId = "verification.parent",
                    confidence = 1f
                });
                Check("structural cycle rejection [added=" + structuralCycleAdded + "]", !structuralCycleAdded, ref passed, failures);
                Check("dynamic subject lifecycle", KnowledgeRegistry.UpdateSubject(DomainId, "child", new KnowledgeSubjectUpdate { label = "Archived child" }) &&
                    KnowledgeRegistry.SetSubjectState(DomainId, "child", KnowledgeSubjectState.Archived) &&
                    KnowledgeRegistry.ResolveSubject(DomainId, "child").state == KnowledgeSubjectState.Archived, ref passed, failures);
                KnowledgeComparisonSnapshot comparison = KnowledgeComparisonService.Compare(DomainId, "source", "child", pawn);
                Check("structured comparison", comparison.rows.Count >= 2, ref passed, failures);
                Check("filtered transmission", KnowledgeTransmission.Transfer(new KnowledgeTransmissionRequest
                {
                    kind = KnowledgeTransmissionKind.Report,
                    domainId = DomainId,
                    subjectId = "source",
                    sourcePawn = pawn,
                    facetIds = new[] { "biology" },
                    claimIds = new[] { "size" },
                    context = context,
                    includeMilestones = true,
                    source = "v3"
                }), ref passed, failures);
                Check("milestone transmission control", KnowledgeMilestoneService.IsCompleted(DomainId, "source", "progress", "established", null, context),
                    ref passed, failures);
                Check("migration helper", KnowledgeMigrationService.ImportMinimum("verification-consumer", 1, DomainId, "source", pawn, 1f, 0f, 0f) &&
                    KnowledgeMigrationService.IsCommitted("verification-consumer", 1), ref passed, failures);

                Check("migration unknown relation type", !KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
                {
                    consumerId = "verification-migration-unknown-relation",
                    version = 1,
                    relations = new[] { MigrationRelation("verification.unknown", "child", "source") }
                }) && !KnowledgeMigrationService.IsCommitted("verification-migration-unknown-relation", 1), ref passed, failures);
                KnowledgeRelationService.RegisterType(new KnowledgeSubjectRelationTypeDef
                {
                    defName = "VerificationMissingInverse",
                    stableId = "verification.missing-inverse",
                    inverseTypeId = "verification.inverse-does-not-exist"
                }, true);
                Check("migration inverse type failure", !KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
                {
                    consumerId = "verification-migration-inverse",
                    version = 1,
                    relations = new[] { MigrationRelation("verification.missing-inverse", "child", "source") }
                }) && !KnowledgeMigrationService.IsCommitted("verification-migration-inverse", 1), ref passed, failures);
                bool migrationCycleImported = KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
                {
                    consumerId = "verification-migration-cycle",
                    version = 1,
                    relations = new[] { MigrationRelation("verification.parent", "source", "child") }
                });
                Check("migration relation cycle [imported=" + migrationCycleImported + ",committed=" + KnowledgeMigrationService.IsCommitted("verification-migration-cycle", 1) + "]", !migrationCycleImported && !KnowledgeMigrationService.IsCommitted("verification-migration-cycle", 1), ref passed, failures);
                Check("migration invalid milestone", !KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
                {
                    consumerId = "verification-migration-milestone",
                    version = 1,
                    milestones = new[]
                    {
                        new KnowledgeMilestoneConditionSample
                        {
                            domainId = DomainId,
                            subjectId = "source",
                            trackId = "progress",
                            milestoneId = "established",
                            conditionMet = true,
                            value = float.NaN
                        }
                    }
                }) && !KnowledgeMigrationService.IsCommitted("verification-migration-milestone", 1), ref passed, failures);
                Check("migration failed subject registration", !KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
                {
                    consumerId = "verification-migration-subject",
                    version = 1,
                    domainId = DomainId,
                    subjects = new[] { new KnowledgeSubjectRegistration { id = "invalid\nsubject" } }
                }) && !KnowledgeMigrationService.IsCommitted("verification-migration-subject", 1), ref passed, failures);
                Check("migration invalid claim", !KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
                {
                    consumerId = "verification-migration-claim",
                    version = 1,
                    claims = new[]
                    {
                        new KnowledgeMeasurement
                        {
                            domainId = DomainId,
                            subjectId = "source",
                            facetId = "biology",
                            claimId = "size",
                            observer = pawn,
                            value = KnowledgeClaimValue.Boolean(true)
                        }
                    }
                }) && !KnowledgeMigrationService.IsCommitted("verification-migration-claim", 1), ref passed, failures);
                string lateConsumer = "verification-migration-late";
                float lateBefore = KnowledgeService.GetPawnKnowledgeExperience(DomainId, "source", pawn);
                KnowledgeConsumerMigration lateMigration = new KnowledgeConsumerMigration
                {
                    consumerId = lateConsumer,
                    version = 1,
                    domainId = DomainId,
                    subjectId = "source",
                    pawn = pawn,
                    personalKnowledge = lateBefore + 11f,
                    milestones = new[]
                    {
                        new KnowledgeMilestoneConditionSample
                        {
                            domainId = DomainId,
                            subjectId = "source",
                            trackId = "progress",
                            milestoneId = "established",
                            conditionMet = false
                        }
                    }
                };
                if (!KnowledgeMigrationService.IsCommitted(lateConsumer, 1))
                {
                    bool lateImport = KnowledgeMigrationService.Import(lateMigration);
                    float lateAfterFailure = KnowledgeService.GetPawnKnowledgeExperience(DomainId, "source", pawn);
                    Check("migration late failure remains uncommitted [imported=" + lateImport + ",committed=" + KnowledgeMigrationService.IsCommitted(lateConsumer, 1) + ",before=" + lateBefore + ",after=" + lateAfterFailure + "]",
                        !lateImport && !KnowledgeMigrationService.IsCommitted(lateConsumer, 1) &&
                        lateAfterFailure >= lateBefore + 11f, ref passed, failures);
                    bool lateRetry = KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
                    {
                        consumerId = lateConsumer,
                        version = 1,
                        domainId = DomainId,
                        subjectId = "source",
                        pawn = pawn,
                        personalKnowledge = lateBefore + 11f
                    });
                    Check("migration retry after partial monotonic state", lateRetry && KnowledgeMigrationService.IsCommitted(lateConsumer, 1), ref passed, failures);
                    Check("migration late commit occurs exactly once", component.ConsumerMigrationCountV3(lateConsumer) == 1, ref passed, failures);
                }
                else
                {
                    int lateCountBefore = component.ConsumerMigrationCountV3(lateConsumer);
                    bool lateRepeat = KnowledgeMigrationService.Import(lateMigration);
                    float lateAfterRepeat = KnowledgeService.GetPawnKnowledgeExperience(DomainId, "source", pawn);
                    int lateCountAfter = component.ConsumerMigrationCountV3(lateConsumer);
                    Check("migration late-retry commit persists and remains idempotent after reload [before=" + lateBefore + ",after=" + lateAfterRepeat + ",count=" + lateCountBefore + "->" + lateCountAfter + "]",
                        lateRepeat && KnowledgeMigrationService.IsCommitted(lateConsumer, 1) &&
                        Math.Abs(lateAfterRepeat - lateBefore) < 0.001f && lateCountBefore == 1 && lateCountAfter == 1,
                        ref passed, failures);
                }
                string successfulConsumer = "verification-migration-success";
                KnowledgeConsumerMigration successfulMigration = new KnowledgeConsumerMigration
                {
                    consumerId = successfulConsumer,
                    version = 1,
                    domainId = DomainId,
                    subjectId = "source",
                    pawn = pawn,
                    personalKnowledge = lateBefore + 13f,
                    colonyKnowledge = 4f,
                    expertise = 1f,
                    eventCounts = new Dictionary<string, int> { { "migration", 2 } }
                };
                bool successfulImport = KnowledgeMigrationService.Import(successfulMigration);
                float successfulAmount = KnowledgeService.GetPawnKnowledgeExperience(DomainId, "source", pawn);
                bool successfulRetry = KnowledgeMigrationService.Import(successfulMigration);
                float successfulRetryAmount = KnowledgeService.GetPawnKnowledgeExperience(DomainId, "source", pawn);
                Check("migration successful import", successfulImport && KnowledgeMigrationService.IsCommitted(successfulConsumer, 1), ref passed, failures);
                Check("migration successful import is idempotent", successfulRetry &&
                    Math.Abs(successfulRetryAmount - successfulAmount) < 0.001f, ref passed, failures);
                Check("migration commit occurs exactly once", component.ConsumerMigrationCountV3(successfulConsumer) == 1, ref passed, failures);
                string aliasDomain = "verification-v3-alias-old";
                string aliasSubject = "verification-v3-alias-old-subject";
                string overrideOldSubject = "verification-v3-override-old";
                string overrideCurrentSubject = "verification-v3-override-current";
                KnowledgeContextKey aliasContext = new KnowledgeContextKey("verification.alias", "collision");
                KnowledgeClaimStateRecord aliasClaim = component.ClaimV3(aliasDomain, aliasSubject, "biology", "size", pawn, false,
                    aliasContext, true);
                aliasClaim.measurements.Add(new KnowledgeMeasurementRecord
                {
                    domainId = aliasDomain,
                    subjectId = aliasSubject,
                    facetId = "biology",
                    claimId = "size",
                    observer = pawn,
                    scope = (int)KnowledgeScope.Personal,
                    valueType = (int)KnowledgeClaimValueType.Float,
                    numericValue = 4f,
                    quality = 1f,
                    evidenceWeight = 1f,
                    confidenceFactor = 1f,
                    contextTypeId = aliasContext.typeId,
                    contextId = aliasContext.stableId,
                    tick = 10
                });
                KnowledgeClaimStateRecord canonicalClaim = component.ClaimV3(DomainId, "source", "biology", "size", pawn, false,
                    aliasContext, true);
                canonicalClaim.measurements.Add(new KnowledgeMeasurementRecord
                {
                    domainId = DomainId,
                    subjectId = "source",
                    facetId = "biology",
                    claimId = "size",
                    observer = pawn,
                    scope = (int)KnowledgeScope.Personal,
                    valueType = (int)KnowledgeClaimValueType.Float,
                    numericValue = 8f,
                    quality = 1f,
                    evidenceWeight = 1f,
                    confidenceFactor = 1f,
                    contextTypeId = aliasContext.typeId,
                    contextId = aliasContext.stableId,
                    tick = 20
                });
                KnowledgeContextFacetStateRecord aliasFacet = component.ContextFacetV3(aliasDomain, aliasSubject, "biology", pawn, false,
                    aliasContext, true);
                aliasFacet.amount = 3f;
                KnowledgeContextFacetStateRecord canonicalFacet = component.ContextFacetV3(DomainId, "source", "biology", pawn, false,
                    aliasContext, true);
                canonicalFacet.amount = 7f;
                KnowledgeMilestoneStateRecord aliasMilestone = component.MilestoneV3(aliasDomain, aliasSubject, "progress", "established",
                    pawn, aliasContext, true);
                aliasMilestone.completed = true;
                aliasMilestone.progress = 0.4f;
                aliasMilestone.revision = 1;
                KnowledgeMilestoneStateRecord canonicalMilestone = component.MilestoneV3(DomainId, "source", "progress", "established",
                    pawn, aliasContext, true);
                canonicalMilestone.progress = 0.8f;
                canonicalMilestone.revision = 2;
                KnowledgeStageStateRecord aliasStage = component.StageV3(aliasDomain, aliasSubject, pawn, false, aliasContext, true);
                aliasStage.stageId = "verification-alias-old-stage";
                aliasStage.lastTick = 10;
                KnowledgeStageStateRecord canonicalStage = component.StageV3(DomainId, "source", pawn, false, aliasContext, true);
                canonicalStage.stageId = "verification-alias-current-stage";
                canonicalStage.lastTick = 20;
                Check("alias relation type registration", KnowledgeRelationService.RegisterType(new KnowledgeSubjectRelationTypeDef
                {
                    defName = "VerificationAliasRelation",
                    stableId = "verification.alias-relation"
                }, true), ref passed, failures);
                Check("alias relation records created", component.AddRelationV3(new KnowledgeSubjectRelation
                {
                    domainId = aliasDomain,
                    fromSubjectId = aliasSubject,
                    toDomainId = aliasDomain,
                    toSubjectId = aliasSubject,
                    relationTypeId = "verification.alias-relation",
                    source = "legacy",
                    confidence = 0.5f,
                    tick = 10
                }) && component.AddRelationV3(new KnowledgeSubjectRelation
                {
                    domainId = DomainId,
                    fromSubjectId = "source",
                    toDomainId = DomainId,
                    toSubjectId = "source",
                    relationTypeId = "verification.alias-relation",
                    source = "canonical",
                    confidence = 1f,
                    tick = 20
                }), ref passed, failures);
                KnowledgeObservationDef aliasAccrualDefinition = KnowledgeRegistry.Schema(DomainId)?.Observation("legacy-unique");
                string aliasAccrualKey = KnowledgeAccrualService.BuildKey(aliasDomain, aliasSubject, "identity", false,
                    pawn?.thingIDNumber ?? 0, "alias-collision-source", "<global>", aliasAccrualDefinition?.accrualPolicy, "legacy-unique");
                string canonicalAccrualKey = KnowledgeAccrualService.BuildKey(DomainId, "source", "identity", false,
                    pawn?.thingIDNumber ?? 0, "alias-collision-source", "<global>", aliasAccrualDefinition?.accrualPolicy, "legacy-unique");
                KnowledgeAccrualStateRecord aliasAccrual = component.AccrualV3(aliasAccrualKey, true);
                aliasAccrual.domainId = aliasDomain;
                aliasAccrual.subjectId = aliasSubject;
                aliasAccrual.observationId = "legacy-unique";
                aliasAccrual.policyNamespace = "legacy-unique";
                aliasAccrual.facetId = "identity";
                aliasAccrual.sourceInstanceId = "alias-collision-source";
                aliasAccrual.keyFormatVersion = 2;
                aliasAccrual.ownershipMetadataComplete = true;
                aliasAccrual.count = 2;
                KnowledgeAccrualStateRecord canonicalAccrual = component.AccrualV3(canonicalAccrualKey, true);
                canonicalAccrual.domainId = DomainId;
                canonicalAccrual.subjectId = "source";
                canonicalAccrual.observationId = "legacy-unique";
                canonicalAccrual.policyNamespace = "legacy-unique";
                canonicalAccrual.facetId = "identity";
                canonicalAccrual.sourceInstanceId = "alias-collision-source";
                canonicalAccrual.keyFormatVersion = 2;
                canonicalAccrual.ownershipMetadataComplete = true;
                canonicalAccrual.count = 5;
                KnowledgeSharedExpertiseStateRecord aliasExpertise = component.SharedExpertiseV3("verification.alias", aliasDomain,
                    "collision", pawn, true);
                aliasExpertise.amount = 2f;
                KnowledgeSharedExpertiseStateRecord canonicalExpertise = component.SharedExpertiseV3("verification.alias", DomainId,
                    "collision", pawn, true);
                canonicalExpertise.amount = 5f;
                component.PersistSubjectOverrideV3(aliasDomain, new KnowledgeSubjectRegistration
                {
                    id = overrideOldSubject,
                    label = "Legacy override",
                    applicableFacetIds = new[] { "biology" }
                });
                component.PersistSubjectOverrideV3(DomainId, new KnowledgeSubjectRegistration
                {
                    id = overrideCurrentSubject,
                    label = "Canonical override",
                    applicableFacetIds = new[] { "biology" }
                });
                component.CommitConsumerMigrationV3("verification-alias-preserved", 1);
                int aliasMigrationCount = component.ConsumerMigrationCountV3("verification-alias-preserved");
                Check("alias domain migration", KnowledgeRegistry.RegisterDomainAlias(aliasDomain, DomainId), ref passed, failures);
                Check("alias subject migration", KnowledgeRegistry.RegisterSubjectAlias(DomainId, aliasSubject, "source"), ref passed, failures);
                Check("alias override migration", KnowledgeRegistry.RegisterSubjectAlias(DomainId, overrideOldSubject, overrideCurrentSubject), ref passed, failures);
                Check("alias before content registration", KnowledgeRegistry.RegisterSubjectAlias(DomainId, "verification-before-content-old",
                    "verification-before-content-current") && KnowledgeRegistry.RegisterSubject(DomainId, new KnowledgeSubjectRegistration
                    {
                        id = "verification-before-content-current",
                        label = "Registered after alias"
                    }, new KnowledgeRegistrationOptions
                    {
                        source = "verification-v3-alias",
                        priority = int.MaxValue,
                        conflict = KnowledgeRegistrationConflict.Replace
                    }) && KnowledgeRegistry.ResolveSubject(DomainId, "verification-before-content-old")?.id == "verification-before-content-current",
                    ref passed, failures);
                KnowledgeClaimStateRecord mergedAliasClaim = component.ClaimRecordsV3(DomainId, "source", pawn).FirstOrDefault(item => item != null &&
                    item.facetId == "biology" && item.claimId == "size" && item.contextTypeId == aliasContext.typeId && item.contextId == aliasContext.stableId);
                KnowledgeContextFacetStateRecord mergedAliasFacet = component.ContextFacetRecordsV3(DomainId, "source", "biology", pawn, false)
                    .FirstOrDefault(item => item != null && item.contextTypeId == aliasContext.typeId && item.contextId == aliasContext.stableId);
                KnowledgeMilestoneStateRecord mergedAliasMilestone = component.MilestoneRecordsV3(DomainId, "source", pawn).FirstOrDefault(item => item != null &&
                    item.trackId == "progress" && item.milestoneId == "established" && item.contextTypeId == aliasContext.typeId && item.contextId == aliasContext.stableId);
                KnowledgeStageStateRecord mergedAliasStage = component.StageRecordsV3(DomainId, "source", pawn, false).FirstOrDefault(item => item != null &&
                    item.contextTypeId == aliasContext.typeId && item.contextId == aliasContext.stableId);
                List<KnowledgeSubjectRelationStateRecord> mergedAliasRelations = component.RelationRecordsV3(DomainId, "source").Where(item => item != null &&
                    item.relationTypeId == "verification.alias-relation").ToList();
                List<KnowledgeAccrualStateRecord> mergedAliasAccruals = component.AccrualRecordsV3().Where(item => item != null &&
                    item.domainId == DomainId && item.subjectId == "source" && item.policyNamespace == "legacy-unique" &&
                    item.sourceInstanceId == "alias-collision-source").ToList();
                Check("alias migration immediate records", mergedAliasClaim != null && component.ClaimRecordsV3(DomainId, "source", pawn)
                    .Count(item => item != null && item.facetId == "biology" && item.claimId == "size" && item.contextTypeId == aliasContext.typeId &&
                        item.contextId == aliasContext.stableId) == 1 && mergedAliasClaim.measurements.Count >= 2 &&
                    mergedAliasClaim.measurements.All(item => item.domainId == DomainId && item.subjectId == "source") && mergedAliasFacet?.amount == 7f &&
                    mergedAliasMilestone?.completed == true && mergedAliasMilestone.progress == 0.8f && mergedAliasStage != null &&
                    mergedAliasStage.domainId == DomainId && mergedAliasStage.subjectId == "source" && mergedAliasRelations.Count == 1 &&
                    mergedAliasAccruals.Count == 1 && mergedAliasAccruals[0].count == 5 && mergedAliasAccruals[0].legacyKey == aliasAccrualKey &&
                    aliasMigrationCount == component.ConsumerMigrationCountV3("verification-alias-preserved") &&
                    component.HasConsumerMigrationV3("verification-alias-preserved", 1), ref passed, failures);
                Check("alias shared expertise merge", component.SharedExpertiseRecordsV3("verification.alias", pawn).Count(item => item != null &&
                    item.domainId == DomainId && item.trackId == "collision") == 1 && component.SharedExpertiseRecordsV3("verification.alias", pawn)
                    .FirstOrDefault(item => item != null && item.domainId == DomainId && item.trackId == "collision")?.amount == 5f,
                    ref passed, failures);
                Check("alias override merge", component.SubjectOverrideRecordsV3().Count(item => item != null && item.domainId == DomainId &&
                    item.id == overrideCurrentSubject) == 1 && !component.SubjectOverrideRecordsV3().Any(item => item != null && item.domainId == DomainId &&
                    item.id == overrideOldSubject) && KnowledgeRegistry.ResolveSubject(DomainId, overrideOldSubject)?.id == overrideCurrentSubject,
                    ref passed, failures);
                int aliasClaimCount = component.V3ClaimCount;
                int aliasRelationCount = component.V3RelationCount;
                int aliasAccrualCount = component.V3AccrualCount;
                component.RebuildV3Indexes();
                component.RebuildV3Indexes();
                Check("alias rebuild is idempotent", component.V3ClaimCount == aliasClaimCount && component.V3RelationCount == aliasRelationCount &&
                    component.V3AccrualCount == aliasAccrualCount && component.ClaimRecordsV3(DomainId, "source", pawn).Count(item => item != null &&
                        item.contextTypeId == aliasContext.typeId && item.contextId == aliasContext.stableId) == 1 && component.RelationRecordsV3(DomainId, "source")
                        .Count(item => item != null && item.relationTypeId == "verification.alias-relation") == 1, ref passed, failures);
                bool ownedAccrual = component.AccrualRecordsV3().Any(item => item != null && item.domainId == DomainId);
                component.RemoveDomainDataV3(DomainId);
                bool aliasDomainClean = !component.ClaimRecordsV3(DomainId).Any() && !component.ContextFacetRecordsV3(DomainId, "source", "biology", pawn, false).Any() &&
                    !component.MilestoneRecordsV3(DomainId).Any() && !component.StageRecordsV3(DomainId, "source", pawn, false).Any() &&
                    !component.RelationRecordsV3(DomainId).Any() && !component.AccrualRecordsV3().Any(item => item != null &&
                     (item.domainId == DomainId || item.domainId == aliasDomain || item.legacyKey == aliasAccrualKey)) &&
                     !component.SubjectOverrideRecordsV3().Any(item => item != null && item.domainId == DomainId) &&
                     !component.SharedExpertiseRecordsV3("verification.alias", pawn).Any(item => item != null && item.domainId == DomainId) &&
                     KnowledgeRegistry.ResolveSubject(DomainId, overrideCurrentSubject) == null;
                Check("accrual ownership and domain cleanup", ownedAccrual && aliasDomainClean, ref passed, failures);
            }
            catch (Exception exception)
            {
                failures.Add("v3 game verification exception (expected=no exception actual=" + exception + ")");
            }
            finally
            {
                component.RemoveDomainDataV2(DomainId);
                component.RemoveDomainDataV2(AggregationDomainId);
                KnowledgeRegistry.UnregisterDomain(DomainId, "verification-v3");
                KnowledgeRegistry.UnregisterDomain(AggregationDomainId, "verification-v3-aggregation");
                KnowledgeRegistry.ClearDiagnostics(DomainId);
                KnowledgeRegistry.ClearDiagnostics(AggregationDomainId);
            }
            return new KnowledgeVerificationResult(passed, failures,
                KnowledgeVerificationTrace.End().Concat(pure.passedTests), 0, 0,
                pure.passed, pure.failures.Count,
                Math.Max(0, passed - pure.passed), Math.Max(0, failures.Count - pure.failures.Count));
        }

        private static KnowledgeTransactionResult Observe(Pawn pawn, string facetId, KnowledgeContextKey context = default(KnowledgeContextKey),
            bool targetColony = false, bool success = true, string specimenId = null, string sourceInstanceId = null,
            KnowledgeMeasurement measurement = null, string observationId = null, IReadOnlyList<Pawn> witnesses = null)
        {
            return KnowledgeEngine.Submit(new KnowledgeObservation
            {
                observer = targetColony ? null : pawn,
                domainId = DomainId,
                subjectId = "source",
                facetId = facetId,
                observationId = observationId,
                context = context,
                source = "v3-verification",
                sourceInstanceId = sourceInstanceId,
                specimenId = specimenId,
                targetColony = targetColony,
                success = success,
                quality = 1f,
                novelty = 1f,
                repetition = 1f,
                environmentalDifficulty = 1f,
                claimMeasurements = measurement == null ? null : new[] { measurement },
                witnesses = witnesses
            });
        }

        private static KnowledgeObservationDef AccrualObservation(string id, KnowledgeAccrualPolicy policy, params string[] facetIds) => new KnowledgeObservationDef
        {
            defName = id,
            stableId = id,
            baseKnowledge = 1f,
            facetIds = (facetIds == null || facetIds.Length == 0 ? new[] { "identity" } : facetIds).ToList(),
            accrualPolicy = policy
        };

        private static KnowledgeDomainRegistration AggregationRegistration()
        {
            KnowledgeFacetDef a = Facet("a", 100f);
            KnowledgeFacetDef b = Facet("b", 100f);
            KnowledgeFacetDef empty = Facet("empty", 100f);
            KnowledgeStageDef exactRequirements = new KnowledgeStageDef
            {
                defName = "exact",
                order = 2,
                minimumConfidence = 0.8f,
                requirementGroup = new KnowledgeRequirementGroup
                {
                    mode = KnowledgeRequirementGroupMode.All,
                    requirements = new List<KnowledgeRequirement>
                    {
                        new KnowledgeRequirement { kind = KnowledgeRequirementKind.Knowledge, facetId = "b", minimum = 100f }
                    }
                }
            };
            List<KnowledgeSubjectRegistration> subjects = new List<KnowledgeSubjectRegistration>
            {
                new KnowledgeSubjectRegistration { id = "one", label = "One", applicableFacetIds = new[] { "a" } },
                new KnowledgeSubjectRegistration { id = "many", label = "Many", applicableFacetIds = new[] { "a", "b" } },
                 new KnowledgeSubjectRegistration { id = "empty", label = "Empty", applicableFacetIds = new[] { "a", "empty" } },
                 new KnowledgeSubjectRegistration { id = "confidence", label = "Confidence", applicableFacetIds = new[] { "a", "b" } },
                 new KnowledgeSubjectRegistration { id = "context-only", label = "Context only", applicableFacetIds = new[] { "a", "b" } },
                 new KnowledgeSubjectRegistration { id = "global-context-only", label = "Global context only", applicableFacetIds = new[] { "a" } }
            };
            return new KnowledgeDomainRegistration
            {
                id = AggregationDomainId,
                label = "V3 Aggregation Verification",
                enableUncertainty = true,
                stageAggregationMode = KnowledgeStageAggregationMode.Balanced,
                facets = new[] { a, b, empty },
                stages = new[]
                {
                    new KnowledgeStageDef { defName = "base", order = 0 },
                    new KnowledgeStageDef { defName = "balanced", order = 1, minimumKnowledge = 75f, minimumConfidence = 0.8f },
                    exactRequirements,
                     new KnowledgeStageDef
                     {
                         defName = "global-contextual",
                         order = 3,
                         minimumKnowledge = 75f,
                         minimumConfidence = 0.8f,
                         contextSensitive = true
                     },
                     new KnowledgeStageDef
                     {
                         defName = "contextual",
                        order = 4,
                        minimumKnowledge = 75f,
                        minimumConfidence = 0.8f,
                        contextSensitive = true,
                        requirementGroup = new KnowledgeRequirementGroup
                        {
                            mode = KnowledgeRequirementGroupMode.All,
                            requirements = new List<KnowledgeRequirement>
                            {
                                new KnowledgeRequirement
                                {
                                    kind = KnowledgeRequirementKind.Knowledge,
                                    facetId = "b",
                                    minimum = 100f,
                                    contextTypeId = "verification.region",
                                    contextId = "aggregation",
                                    allowContextFallback = false
                                }
                            }
                        }
                    }
                },
                subjectResolver = id => subjects.FirstOrDefault(item => item.id == id),
                subjectSource = () => subjects,
                source = "verification-v3-aggregation"
            };
        }

        private static bool SubmitStageKnowledge(Pawn pawn, string subjectId, string facetId, float knowledge,
            bool contradictory = false, KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            return KnowledgeEngine.Submit(new KnowledgeTransaction { source = "v3-stage-aggregation" }.Add(new KnowledgeObservation
            {
                observer = pawn,
                domainId = AggregationDomainId,
                subjectId = subjectId,
                facetId = facetId,
                context = context,
                directKnowledge = knowledge,
                disposition = contradictory ? KnowledgeEvidenceDisposition.Contradictory : KnowledgeEvidenceDisposition.Supporting,
                source = "v3-stage-aggregation",
                quality = 1f,
                novelty = 1f,
                repetition = 1f,
                environmentalDifficulty = 1f
            })).success;
        }

        private static KnowledgeClaimDef Claim(string id, KnowledgeClaimValueType type, KnowledgeClaimAggregation aggregation, string facetId = null) => new KnowledgeClaimDef
        {
            defName = id,
            stableId = id,
            valueType = type,
            aggregation = aggregation,
            facetId = facetId,
            provisionalConfidence = 0.1f
        };

        private static KnowledgeFacetDef Facet(string id, float completeness) => new KnowledgeFacetDef
        {
            defName = id,
            stableId = id,
            label = id,
            completenessAmount = completeness,
            personallyKnowable = true,
            shareable = true,
            documentable = true
        };

        private static KnowledgeMeasurement Measurement(KnowledgeClaimValue value, float weight, int tick) => new KnowledgeMeasurement
        {
            claimId = "test",
            value = value,
            quality = weight,
            evidenceWeight = 1f,
            tick = tick
        };

        private static KnowledgeSubjectRelation MigrationRelation(string relationTypeId, string fromSubjectId, string toSubjectId) => new KnowledgeSubjectRelation
        {
            domainId = DomainId,
            fromSubjectId = fromSubjectId,
            toDomainId = DomainId,
            toSubjectId = toSubjectId,
            relationTypeId = relationTypeId,
            confidence = 1f
        };

        private sealed class VerificationContextPresentationProvider : IKnowledgeContextPresentationProvider
        {
            internal static bool EmitLarge;
            internal static int KnownContextsCalls { get; private set; }

            internal static void ResetCounters() => KnownContextsCalls = 0;

            public IEnumerable<KnowledgeContextKey> KnownContexts(string domainId, string subjectId, Pawn pawn, KnowledgeScope scope)
            {
                KnownContextsCalls++;
                if (domainId != DomainId && domainId != AggregationDomainId) return Enumerable.Empty<KnowledgeContextKey>();
                IEnumerable<KnowledgeContextKey> values = new[]
                {
                    new KnowledgeContextKey("verification.region", "alpha"),
                    new KnowledgeContextKey("verification.region", "shared"),
                    new KnowledgeContextKey("verification.region", "aggregation"),
                    new KnowledgeContextKey("verification.region.child", "shared")
                };
                if (EmitLarge)
                    values = values.Concat(Enumerable.Range(0, 2048).Select(index =>
                        new KnowledgeContextKey("verification.region", "large-" + index.ToString("D4"))));
                return values;
            }

            public string ValueLabel(KnowledgeContextKey context, string domainId, string subjectId, Pawn pawn, KnowledgeScope scope)
            {
                switch (context.stableId)
                {
                    case "alpha": return "Alpha region";
                    case "shared": return "Shared region";
                    case "aggregation": return "Aggregation region";
                    default: return null;
                }
            }
        }

        private sealed class ThrowingContextPresentationProvider : IKnowledgeContextPresentationProvider
        {
            public IEnumerable<KnowledgeContextKey> KnownContexts(string domainId, string subjectId, Pawn pawn, KnowledgeScope scope)
            {
                throw new InvalidOperationException("verification provider failure");
            }

            public string ValueLabel(KnowledgeContextKey context, string domainId, string subjectId, Pawn pawn, KnowledgeScope scope)
            {
                throw new InvalidOperationException("verification label failure");
            }
        }

        private static void Check(string name, bool condition, ref int passed, List<string> failures)
        {
            KnowledgeVerificationTrace.Record(name, condition);
            if (condition) passed++;
            else failures.Add(name + " (expected=true, actual=false)");
        }
    }
}
