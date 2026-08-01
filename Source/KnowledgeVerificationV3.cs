using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    internal static class KnowledgeFrameworkVerificationV3
    {
        private const string DomainId = "KnowledgeFramework.Verification.V3";

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
                failures.Add("v3 active game and pawn required");
                return new KnowledgeVerificationResult(passed, failures);
            }
            KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "VerificationRegion", stableId = "verification.region" }, true);
            KnowledgeRelationService.RegisterType(new KnowledgeSubjectRelationTypeDef { defName = "VerificationParent", stableId = "verification.parent", parentage = true }, true);
            KnowledgeExpertiseNamespaceDef namespaceDef = new KnowledgeExpertiseNamespaceDef { defName = "VerificationField", stableId = "verification.field", adept = 1f, expert = 2f, master = 3f };
            KnowledgeSharedExpertiseService.RegisterNamespace(namespaceDef, true);
            KnowledgeFacetDef identity = Facet("identity", 100f);
            KnowledgeFacetDef biology = Facet("biology", 100f);
            KnowledgeClaimDef size = Claim("size", KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.Highest, "biology");
            KnowledgeClaimDef traits = Claim("traits", KnowledgeClaimValueType.SetOfIds, KnowledgeClaimAggregation.Union, "biology");
            KnowledgeExpertiseTrackDef field = new KnowledgeExpertiseTrackDef { defName = "field", stableId = "field", label = "Field work", adept = 1f, expert = 2f, master = 3f };
            KnowledgeSubjectArchetypeDef specimen = new KnowledgeSubjectArchetypeDef
            {
                defName = "specimen",
                stableId = "specimen",
                applicableFacetIds = new List<string> { "biology", "identity" },
                applicableClaimIds = new List<string> { "size", "traits" },
                categoryId = "generated"
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
                }
            };
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
                facets = new[] { identity, biology },
                claims = new[] { size, traits },
                archetypes = new[] { specimen },
                observations = new[] { recipe },
                expertiseTracks = new[] { field },
                milestoneTracks = new[] { track },
                expertiseNamespaces = new[] { namespaceDef },
                subjectResolver = id => id == "source" || id == "child" ? new KnowledgeSubjectRegistration { id = id, label = id, archetypeId = "specimen" } : null,
                subjectSource = () => new[] { new KnowledgeSubjectRegistration { id = "source", label = "Source", archetypeId = "specimen" }, new KnowledgeSubjectRegistration { id = "child", label = "Child", archetypeId = "specimen" } },
                source = "verification-v3"
            };
            KnowledgeRegistrationOptions options = new KnowledgeRegistrationOptions { source = "verification-v3", priority = int.MaxValue, conflict = KnowledgeRegistrationConflict.Replace };
            try
            {
                Check("v3 domain registration", KnowledgeRegistry.RegisterDomain(registration, options), ref passed, failures);
                KnowledgeContextKey context = new KnowledgeContextKey("verification.region", "alpha");
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
                    source = "v3"
                }), ref passed, failures);
                Check("migration helper", KnowledgeMigrationService.ImportMinimum("verification-consumer", 1, DomainId, "source", pawn, 1f, 0f, 0f) &&
                    KnowledgeMigrationService.IsCommitted("verification-consumer", 1), ref passed, failures);
            }
            catch (Exception exception)
            {
                failures.Add("v3 game verification exception: " + exception);
            }
            finally
            {
                component.RemoveDomainDataV2(DomainId);
                KnowledgeRegistry.UnregisterDomain(DomainId, "verification-v3");
                KnowledgeRegistry.ClearDiagnostics(DomainId);
            }
            return new KnowledgeVerificationResult(passed, failures);
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

        private static void Check(string name, bool condition, ref int passed, List<string> failures)
        {
            if (condition) passed++;
            else failures.Add(name);
        }
    }
}
