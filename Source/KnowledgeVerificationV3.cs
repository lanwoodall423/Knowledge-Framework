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
            return new KnowledgeVerificationResult(passed, failures);
        }

        internal static KnowledgeVerificationResult RunGameTests(Pawn pawn)
        {
            KnowledgeVerificationResult pure = RunPureTests();
            int passed = pure.passed;
            List<string> failures = pure.failures.ToList();
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component == null || pawn == null)
            {
                failures.Add("v3 active game and pawn required (expected=active GameComponent and non-null Pawn actual=missing)");
                return new KnowledgeVerificationResult(passed, failures);
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
            KnowledgeContextRegistry.RegisterPresentationProvider("verification.region",
                new VerificationContextPresentationProvider(), true);
            KnowledgeContextRegistry.RegisterPresentationProvider("verification.region.child",
                new VerificationContextPresentationProvider(), true);
            KnowledgeRelationService.RegisterType(new KnowledgeSubjectRelationTypeDef { defName = "VerificationParent", stableId = "verification.parent", parentage = true }, true);
            KnowledgeExpertiseNamespaceDef namespaceDef = new KnowledgeExpertiseNamespaceDef { defName = "VerificationField", stableId = "verification.field", adept = 1f, expert = 2f, master = 3f };
            KnowledgeSharedExpertiseService.RegisterNamespace(namespaceDef, true);
            KnowledgeFacetDef identity = Facet("identity", 100f);
            KnowledgeFacetDef biology = Facet("biology", 100f);
            KnowledgeFacetDef secret = Facet("secret", 100f);
            secret.hiddenUntilRevealed = true;
            secret.revealKnowledge = 100f;
            secret.revealConfidence = 0.4f;
            KnowledgeClaimDef size = Claim("size", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.Highest, "biology");
            KnowledgeClaimDef traits = Claim("traits", KnowledgeClaimValueType.SetOfIds, KnowledgeClaimAggregation.Union, "biology");
            size.label = "Size";
            traits.revealedByDefault = false;
            KnowledgeExpertiseTrackDef field = new KnowledgeExpertiseTrackDef { defName = "field", stableId = "field", label = "Field work", adept = 1f, expert = 2f, master = 3f };
            KnowledgeSubjectArchetypeDef specimen = new KnowledgeSubjectArchetypeDef
            {
                defName = "specimen",
                stableId = "specimen",
                applicableFacetIds = new List<string> { "biology", "identity", "secret" },
                applicableClaimIds = new List<string> { "size", "traits" }
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
            });
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
                claims = new[] { size, traits },
                archetypes = new[] { specimen },
                observations = new[] { recipe, contextless, cooldown, daily, lifetime, noOp, diminishing, firstOutcome, distinct, bounded, legacyUnique, legacyCap },
                expertiseTracks = new[] { field },
                milestoneTracks = new[] { track },
                expertiseNamespaces = new[] { namespaceDef },
                subjectResolver = id => id == "source" || id == "child" ? new KnowledgeSubjectRegistration { id = id, label = id, archetypeId = "specimen" } : null,
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
                    Check("balanced equivalent stage eligibility", oneStage == "balanced" && severalStage == "exact", ref passed, failures);
                    Check("empty facet cannot advance stage", emptyStage == "base", ref passed, failures);
                    Check("poorly supported facet blocks confidence stage", confidenceStage == "base", ref passed, failures);
                    Check("global stage ignores contextual-only stage", KnowledgeDiscovery.CurrentStage(AggregationDomainId, "many", pawn,
                        KnowledgeScope.Personal, context) == "exact", ref passed, failures);
                    KnowledgeContextKey aggregationContext = new KnowledgeContextKey("verification.region", "aggregation");
                    for (int i = 0; i < 10; i++)
                    {
                        SubmitStageKnowledge(pawn, "many", "a", 10f, false, aggregationContext);
                        SubmitStageKnowledge(pawn, "many", "b", 10f, false, aggregationContext);
                        SubmitStageKnowledge(pawn, "context-only", "a", 10f, false, aggregationContext);
                        SubmitStageKnowledge(pawn, "context-only", "b", 10f, false, aggregationContext);
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
                     Check("contextual stage exact parent global lookup", exactContextStage.stageId == "contextual" &&
                         !exactContextStage.usedContextFallback && parentContextStage.stageId == "contextual" &&
                         parentContextStage.usedContextFallback && parentContextStage.resolvedContext.Equals(aggregationContext) &&
                         globalContextStage.stageId == "exact" && globalContextStage.usedContextFallback &&
                         globalContextStage.resolvedContext.IsEmpty &&
                         !component.StageRecordsV3(AggregationDomainId, "many", pawn, false).Any(item => item.Context.Equals(childAggregationContext)),
                         ref passed, failures);
                     KnowledgeBrowserRow contextualBrowserRow = KnowledgeBrowserModels.BuildSubject(new KnowledgeBrowserFilter
                     {
                         domainId = AggregationDomainId,
                         pawn = pawn,
                         scope = KnowledgeScope.Personal,
                         context = childAggregationContext,
                         fallback = KnowledgeContextFallbackMode.ParentThenGlobal
                     }, "many");
                     Check("browser contextual stage presentation", contextualBrowserRow != null &&
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
                Check("multi-facet recipe", recipeResult.success && KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount >= 3f &&
                    KnowledgeQuery.Facet(DomainId, "source", "biology", pawn).amount >= 7f, ref passed, failures);
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
                Check("browser Open context reaches model", browserWindow.RequestedContext.Equals(childContext) && browserRow != null &&
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
                Check("browser visibility and labels", visibleRows.Any(item => item.subject.id == "source") &&
                    !visibleRows.Any(item => item.subject.id == "hidden-subject") &&
                    !visibleRows.Any(item => item.subject.id == "archived-subject" || item.subject.id == "missing-subject") &&
                    !browserRow.applicableFacets.Any(item => item.id == "secret") &&
                    !browserRow.claims.Any(item => item.claimId == "traits") &&
                    KnowledgeBrowserLabels.Claim(KnowledgeRegistry.Schema(DomainId), "size") == "Size",
                    ref passed, failures);
                Check("hidden facet search does not disclose", !KnowledgeBrowserModels.Build(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = childContext,
                    search = "secret"
                }).Any() && !KnowledgeBrowserModels.BuildSubject(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = childContext,
                    includeHidden = true
                }, "source").applicableFacets.Any(item => item.id == "secret"), ref passed, failures);
                IReadOnlyList<KnowledgeBrowserRow> developerRows = KnowledgeBrowserModels.Build(new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = pawn,
                    scope = KnowledgeScope.Personal,
                    context = childContext,
                    fallback = KnowledgeContextFallbackMode.ParentThenGlobal,
                    developerMode = true
                });
                Check("browser developer visibility", developerRows.Any(item => item.subject.id == "hidden-subject") &&
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
                    observationId = "reveal-secret"
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
                Check("revealed hidden facet is normally visible", secretObservation.success && revealedRow != null &&
                    revealedRow.applicableFacets.Any(item => item.id == "secret") &&
                    KnowledgeBrowserLabels.Facet(revealedRow.applicableFacets.First(item => item.id == "secret")) == "Secret" &&
                    (hiddenSubjectRow == null || !hiddenSubjectRow.applicableFacets.Any(item => item.id == "secret")),
                    ref passed, failures);
                Check("developer hidden facet override remains explicit", developerHiddenRow != null &&
                    developerHiddenRow.applicableFacets.Any(item => item.id == "secret"), ref passed, failures);
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
                Check("browser context selector and labels", contextOptions.Contains(KnowledgeContextKey.Empty) &&
                    contextOptions.Contains(childContext) &&
                    KnowledgeBrowserLabels.ContextValue(childContext, DomainId, "source", pawn, KnowledgeScope.Personal) == "Shared region" &&
                    KnowledgeBrowserLabels.ContextSelection(childContext, DomainId, "source", pawn, KnowledgeScope.Personal).Contains("Shared region"),
                    ref passed, failures);
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
                Check("global colony claim requirement", KnowledgeRequirementService.Evaluate(new KnowledgeRequirement
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
                Check("diminishing returns without uniqueness", Math.Abs(KnowledgeQuery.Facet(DomainId, "source", "biology", pawn).amount - diminishingBefore - 1.5f) < 0.001f,
                    ref passed, failures);
                float outcomeBefore = KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount;
                Observe(pawn, "identity", KnowledgeContextKey.Empty, false, true, null, "first-success", null, "first-outcome");
                Observe(pawn, "identity", KnowledgeContextKey.Empty, false, false, null, "first-failure", null, "first-outcome");
                Check("first success and failure bonuses", Math.Abs(KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount - outcomeBefore - 5f) < 0.001f,
                    ref passed, failures);
                float distinctBefore = KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount;
                KnowledgeContextKey distinctA = new KnowledgeContextKey("verification.region", "distinct-a");
                KnowledgeContextKey distinctB = new KnowledgeContextKey("verification.region", "distinct-b");
                Observe(pawn, "identity", distinctA, false, true, "specimen-a", "distinct-1", null, "distinct");
                Observe(pawn, "identity", distinctB, false, true, "specimen-b", "distinct-2", null, "distinct");
                float afterIndependent = KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount;
                Observe(pawn, "identity", distinctB, false, true, "specimen-b", "distinct-2", null, "distinct");
                float afterRepeated = KnowledgeQuery.Facet(DomainId, "source", "identity", pawn).amount;
                Check("different specimen/context/source behavior", afterIndependent - distinctBefore > 7.9f &&
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
                Check("save/index rebuild normalization", rebuiltLegacy != null && rebuiltLegacy.count == 0 &&
                    rebuiltLegacy.dailyCount == 0 && rebuiltLegacy.sourceInstanceIds.Count == 1 && rebuiltLegacy.contextKeys.Count == 1,
                    ref passed, failures);
                KnowledgeObservationDef provableDefinition = KnowledgeRegistry.Schema(DomainId)?.Observation("legacy-unique");
                string provableModernKey = KnowledgeAccrualService.BuildKey(DomainId, "source", "identity", false,
                    pawn?.thingIDNumber ?? 0, "legacy-source", "<global>", provableDefinition?.accrualPolicy, "legacy-unique");
                KnowledgeAccrualStateRecord rebuiltProvable = component.AccrualRecordsV3().FirstOrDefault(item => item != null &&
                    item.legacyKey == provableLegacyKey);
                Check("legacy accrual migration canonicalizes provable ownership", rebuiltProvable != null &&
                    rebuiltProvable.key == provableModernKey && rebuiltProvable.ownershipMetadataComplete, ref passed, failures);
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
                Check("structural relation", KnowledgeRelationService.Add(new KnowledgeSubjectRelation
                {
                    domainId = DomainId,
                    fromSubjectId = "child",
                    toDomainId = DomainId,
                    toSubjectId = "source",
                    relationTypeId = "verification.parent",
                    confidence = 1f
                }), ref passed, failures);
                Check("structural cycle rejection", !KnowledgeRelationService.Add(new KnowledgeSubjectRelation
                {
                    domainId = DomainId,
                    fromSubjectId = "source",
                    toDomainId = DomainId,
                    toSubjectId = "child",
                    relationTypeId = "verification.parent",
                    confidence = 1f
                }), ref passed, failures);
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
                bool ownedAccrual = component.AccrualRecordsV3().Any(item => item != null && item.domainId == DomainId);
                component.RemoveDomainDataV3(DomainId);
                Check("accrual ownership and domain cleanup", ownedAccrual && !component.AccrualRecordsV3().Any(item => item != null && item.domainId == DomainId),
                    ref passed, failures);
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
            return new KnowledgeVerificationResult(passed, failures);
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
                new KnowledgeSubjectRegistration { id = "context-only", label = "Context only", applicableFacetIds = new[] { "a", "b" } }
            };
            return new KnowledgeDomainRegistration
            {
                id = AggregationDomainId,
                label = "V3 Aggregation Verification",
                stageAggregationMode = KnowledgeStageAggregationMode.Balanced,
                facets = new[] { a, b, empty },
                stages = new[]
                {
                    new KnowledgeStageDef { defName = "base", order = 0 },
                    new KnowledgeStageDef { defName = "balanced", order = 1, minimumKnowledge = 75f, minimumConfidence = 0.8f },
                    exactRequirements,
                    new KnowledgeStageDef
                    {
                        defName = "contextual",
                        order = 3,
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

        private sealed class VerificationContextPresentationProvider : IKnowledgeContextPresentationProvider
        {
            public IEnumerable<KnowledgeContextKey> KnownContexts(string domainId, string subjectId, Pawn pawn, KnowledgeScope scope)
            {
                if (domainId != DomainId && domainId != AggregationDomainId) return Enumerable.Empty<KnowledgeContextKey>();
                return new[]
                {
                    new KnowledgeContextKey("verification.region", "alpha"),
                    new KnowledgeContextKey("verification.region", "shared"),
                    new KnowledgeContextKey("verification.region", "aggregation"),
                    new KnowledgeContextKey("verification.region.child", "shared")
                };
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

        private static void Check(string name, bool condition, ref int passed, List<string> failures)
        {
            if (condition) passed++;
            else failures.Add(name + " (expected=true, actual=false)");
        }
    }
}
