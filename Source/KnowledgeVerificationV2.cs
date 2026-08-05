using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace KnowledgeFramework
{
    public sealed class KnowledgeVerificationResult
    {
        public readonly int passed;
        public readonly IReadOnlyList<string> failures;
        public readonly IReadOnlyList<string> passedTests;
        public readonly int skipped;
        public readonly int unavailable;
        public readonly int purePassed;
        public readonly int pureFailed;
        public readonly int gamePassed;
        public readonly int gameFailed;
        public bool Success => failures.Count == 0 && unavailable == 0;

        internal KnowledgeVerificationResult(int passed, List<string> failures, IEnumerable<string> passedTests = null,
            int skipped = 0, int unavailable = 0, int purePassed = 0, int pureFailed = 0,
            int gamePassed = 0, int gameFailed = 0)
        {
            this.passed = passed;
            this.failures = failures.AsReadOnly();
            this.passedTests = (passedTests ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
            this.skipped = Math.Max(0, skipped);
            this.unavailable = Math.Max(0, unavailable);
            this.purePassed = Math.Max(0, purePassed);
            this.pureFailed = Math.Max(0, pureFailed);
            this.gamePassed = Math.Max(0, gamePassed);
            this.gameFailed = Math.Max(0, gameFailed);
        }

        public override string ToString() => Success ? passed + " checks passed" :
            passed + " passed; failures=" + failures.Count + "; unavailable=" + unavailable;
    }

    public static class KnowledgeFrameworkVerification
    {
        private const string TestDomain = "KnowledgeFramework.Verification";

        public static KnowledgeVerificationResult RunPureTests()
        {
            KnowledgeVerificationTrace.Begin();
            int passed = 0;
            List<string> failures = new List<string>();
            Check("rank transitions", KnowledgeRanks.ForExperience(99f, 100f, 300f, 700f) == KnowledgeRank.Novice &&
                KnowledgeRanks.ForExperience(100f, 100f, 300f, 700f) == KnowledgeRank.Adept &&
                KnowledgeRanks.ForExperience(700f, 100f, 300f, 700f) == KnowledgeRank.Master, ref passed, failures);
            Check("rank progress", Math.Abs(KnowledgeRanks.Progress(200f, 100f, 300f, 700f) - 0.5f) < 0.001f, ref passed, failures);
            float confidence1 = KnowledgeMath.Confidence(1f, 0f, true);
            float confidence2 = KnowledgeMath.Confidence(2f, 0f, true);
            float contradicted = KnowledgeMath.Confidence(2f, 2f, true);
            Check("confidence aggregation", confidence2 > confidence1 && contradicted < confidence2, ref passed, failures);
            Check("optional uncertainty", KnowledgeMath.Confidence(0.01f, 100f, false) == 1f, ref passed, failures);
            Check("invalid numbers", !KnowledgeMath.IsFinite(float.NaN) && !KnowledgeMath.IsFinite(float.PositiveInfinity), ref passed, failures);
            KnowledgeEffectAccumulator effects = new KnowledgeEffectAccumulator(10f, true);
            effects.Compose(KnowledgeEffectComposition.Add, 2f);
            effects.Compose(KnowledgeEffectComposition.Multiply, 3f);
            effects.Compose(KnowledgeEffectComposition.Maximum, 30f);
            effects.Compose(KnowledgeEffectComposition.Override, 25f, 10, "override");
            effects.Compose(KnowledgeEffectComposition.Override, 5f, 5, "lower");
            effects.Compose(KnowledgeEffectComposition.Deny, 0f);
            KnowledgeEffectResult effectResult = effects.Result();
            Check("typed effect composition", Math.Abs(effectResult.numericValue - 25f) < 0.001f && !effectResult.permitted &&
                effectResult.resultId == "override", ref passed, failures);
            Check("relationship cycle rejection", KnowledgeGraphValidation.HasCycle(new[]
            {
                new KeyValuePair<string, string>("a", "b"),
                new KeyValuePair<string, string>("b", "c"),
                new KeyValuePair<string, string>("c", "a")
            }), ref passed, failures);
            Check("acyclic relationships", !KnowledgeGraphValidation.HasCycle(new[]
            {
                new KeyValuePair<string, string>("a", "b"),
                new KeyValuePair<string, string>("b", "c")
            }), ref passed, failures);
            KnowledgeFacetStateRecord normalized = new ColonyFacetStateRecord
            {
                amount = float.NaN,
                supportingEvidence = float.PositiveInfinity,
                contradictoryEvidence = -5f,
                evidenceCount = -2,
                facetId = null
            };
            normalized.Normalize();
            Check("loaded record normalization", normalized.amount == 0f && normalized.supportingEvidence == 0f &&
                normalized.contradictoryEvidence == 0f && normalized.evidenceCount == 0 &&
                normalized.facetId == KnowledgeSchema.DefaultFacetId, ref passed, failures);
            KnowledgeSchema minimal = new KnowledgeSchema(new KnowledgeDomainRegistration { id = "pure.minimal", label = "Minimal" }, 0, "verification");
            Check("implicit facet", minimal.facets.Count == 1 && minimal.facets[0].id == KnowledgeSchema.DefaultFacetId, ref passed, failures);
            KnowledgeVerificationResult v3 = KnowledgeFrameworkVerificationV3.RunPureTests();
            passed += v3.passed;
            failures.AddRange(v3.failures.Select(value => "v3 " + value));
            return new KnowledgeVerificationResult(passed, failures,
                KnowledgeVerificationTrace.End().Concat(v3.passedTests), 0, 0,
                passed, failures.Count);
        }

        public static KnowledgeVerificationResult RunGameTests(Pawn pawn)
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

            KnowledgeFacetDef identity = NewFacet("identity", 100f);
            KnowledgeFacetDef habitat = NewFacet("habitat", 100f);
            KnowledgeExpertiseTrackDef field = new KnowledgeExpertiseTrackDef { defName = "field", label = "Field work", adept = 10f, expert = 20f, master = 30f };
            List<KnowledgeStageDef> stages = new List<KnowledgeStageDef>
            {
                new KnowledgeStageDef { defName = "rumored", label = "Rumored", order = 0 },
                new KnowledgeStageDef { defName = "encountered", label = "Encountered", order = 10, minimumKnowledge = 20f },
                new KnowledgeStageDef { defName = "documented", label = "Documented", order = 20, minimumKnowledge = 20f,
                    minimumConfidence = 0.5f, documented = true }
            };
            KnowledgeInsightDef insight = new KnowledgeInsightDef
            {
                defName = "verifiedHabitat",
                domainId = TestDomain,
                scope = KnowledgeInsightScope.Personal,
                requirements = new List<KnowledgeInsightRequirement>
                {
                    new KnowledgeInsightRequirement { kind = KnowledgeRequirementKind.EvidenceCount, facetId = "habitat", minimum = 2f }
                },
                outcomes = new List<KnowledgeInsightOutcome>()
            };
            KnowledgeRelationshipDef relationship = new KnowledgeRelationshipDef
            {
                defName = "related",
                domainId = TestDomain,
                fromSubjectId = "source",
                toSubjectId = "related",
                facetId = "habitat",
                coefficient = 0.5f,
                confidenceCoefficient = 0.5f
            };
            KnowledgeDomainRegistration registration = new KnowledgeDomainRegistration
            {
                id = TestDomain,
                label = "Verification",
                enableUncertainty = true,
                enableFamiliarity = true,
                sharingModel = KnowledgeSharingModel.Custom,
                facets = new[] { identity, habitat },
                stages = stages,
                expertiseTracks = new[] { field },
                insights = new[] { insight },
                relationships = new[] { relationship },
                subjectResolver = id => id == "source" || id == "related" ? new KnowledgeSubjectRegistration { id = id, label = id } : null,
                subjectSource = () => Enumerable.Range(0, 5000).Select(index => new KnowledgeSubjectRegistration
                {
                    id = index == 0 ? "source" : index == 1 ? "related" : "generated-" + index,
                    label = "Generated " + index
                }),
                source = "verification"
            };
            bool previousBioPanelEnabled = KnowledgeFrameworkMod.Settings?.BioPanelEnabled ?? true;
            bool bioProviderRegistered = false;
            try
            {
                bool registered = KnowledgeRegistry.RegisterDomain(registration, new KnowledgeRegistrationOptions
                {
                    source = "verification",
                    priority = int.MaxValue,
                    conflict = KnowledgeRegistrationConflict.Replace
                });
                Check("dynamic domain registration", registered, ref passed, failures);
                KnowledgeProviderRegistry.Register("verification.bio", int.MaxValue,
                    value => new KnowledgeEntry { label = "Verification", summary = value?.LabelShortCap });
                bioProviderRegistered = true;
                bool bioSettingsAvailable = KnowledgeFrameworkMod.Settings != null;
                if (bioSettingsAvailable) KnowledgeFrameworkMod.Settings.BioPanelEnabled = true;
                bool bioVisibleWhenEnabled = KnowledgeBioPanel.VisibleFor(pawn);
                bool priorBioPanelEnabled = KnowledgeFrameworkMod.Settings?.BioPanelEnabled ?? true;
                bool bioHiddenWhenDisabled = false;
                if (bioSettingsAvailable)
                {
                    KnowledgeFrameworkMod.Settings.BioPanelEnabled = false;
                    bioHiddenWhenDisabled = !KnowledgeBioPanel.VisibleFor(pawn);
                    KnowledgeFrameworkMod.Settings.BioPanelEnabled = true;
                }
                bool bioVisibleAfterRestore = KnowledgeBioPanel.VisibleFor(pawn);
                Check("Bio-panel setting behavior", bioSettingsAvailable && bioVisibleWhenEnabled &&
                    bioHiddenWhenDisabled && bioVisibleAfterRestore, ref passed, failures);
                if (bioSettingsAvailable) KnowledgeFrameworkMod.Settings.BioPanelEnabled = priorBioPanelEnabled;
                KnowledgeProviderRegistry.Unregister("verification.bio");
                bioProviderRegistered = false;
                KnowledgeTransaction batch = new KnowledgeTransaction { source = "verification" }
                    .Add(NewObservation(pawn, "habitat", 20f, true, 5f))
                    .Add(NewObservation(pawn, "habitat", 10f, false, 5f));
                KnowledgeTransactionResult batchResult = KnowledgeEngine.Submit(batch);
                KnowledgeFacetSnapshotV2 habitatValue = KnowledgeQuery.Facet(TestDomain, "source", "habitat", pawn);
                Check("batch and failed observations", batchResult.success && habitatValue.amount == 30f &&
                    habitatValue.evidenceCount == 2 && habitatValue.successCount == 1 && habitatValue.failureCount == 1, ref passed, failures);
                Check("discovery stage transition", KnowledgeQuery.Subject(TestDomain, "source", pawn).stageId == "encountered", ref passed, failures);
                Check("multiple expertise tracks", Math.Abs(KnowledgeQuery.Expertise(TestDomain, pawn, "field").amount - 10f) < 0.001f, ref passed, failures);
                Check("insight activation", batchResult.activatedInsightIds.Contains("verifiedHabitat") &&
                    KnowledgeInsightService.Progress("verifiedHabitat", TestDomain, "source", pawn).activated, ref passed, failures);
                Check("relationship transfer", Math.Abs(KnowledgeQuery.Facet(TestDomain, "related", "habitat", pawn).derivedAmount - 15f) < 0.001f,
                    ref passed, failures);
                float beforeInvalid = habitatValue.amount;
                KnowledgeTransaction invalid = new KnowledgeTransaction().Add(NewObservation(pawn, "habitat", 4f, true, 0f))
                    .Add(new KnowledgeObservation { observer = pawn, domainId = TestDomain, subjectId = "source", facetId = "identity", quality = float.NaN });
                Check("batch atomicity and NaN rejection", !KnowledgeEngine.Submit(invalid).success &&
                    Math.Abs(KnowledgeQuery.Facet(TestDomain, "source", "habitat", pawn).amount - beforeInvalid) < 0.001f, ref passed, failures);
                int safeSubscriberCalls = 0;
                Action<KnowledgeBatchChangedEvent> brokenSubscriber = value => throw new InvalidOperationException("verification callback");
                Action<KnowledgeBatchChangedEvent> safeSubscriber = value => safeSubscriberCalls++;
                KnowledgeEngine.Changed += brokenSubscriber;
                KnowledgeEngine.Changed += safeSubscriber;
                KnowledgeTransactionResult callbackResult = KnowledgeEngine.Submit(NewObservation(pawn, "identity", 1f, true, 0f));
                KnowledgeEngine.Changed -= brokenSubscriber;
                KnowledgeEngine.Changed -= safeSubscriber;
                Check("consumer callback isolation", callbackResult.success && safeSubscriberCalls == 1, ref passed, failures);
                Check("missing subject rejection", !KnowledgeEngine.Submit(new KnowledgeObservation
                {
                    observer = pawn,
                    domainId = TestDomain,
                    subjectId = "missing",
                    directKnowledge = 1f,
                    suppressConfiguredKnowledge = true
                }).success, ref passed, failures);
                bool reported = KnowledgeTransmission.Report(TestDomain, "source", pawn, "verification");
                bool documented = KnowledgeTransmission.Document(TestDomain, "source", pawn, "verification");
                bool colonyDocumented = KnowledgeQuery.Subject(TestDomain, "source", null, KnowledgeScope.Colony).documented;
                float colonyHabitat = KnowledgeQuery.Facet(TestDomain, "source", "habitat", null, KnowledgeScope.Colony).amount;
                Check("reporting and documentation", reported && documented && colonyDocumented && colonyHabitat > 0f, ref passed, failures);
                Check("documented stage transition", KnowledgeQuery.Subject(TestDomain, "source", null, KnowledgeScope.Colony).stageId == "documented", ref passed, failures);
                if (!(reported && documented && colonyDocumented && colonyHabitat > 0f))
                    failures.Add("reporting detail report=" + reported + " document=" + documented +
                        " documented=" + colonyDocumented + " colonyHabitat=" + colonyHabitat.ToString("0.##"));
                bool importOne = KnowledgeService.ImportMinimum(TestDomain, "source", pawn, 80f, 40f, 25f,
                    new Dictionary<string, int> { ["legacy"] = 4 });
                bool importTwo = KnowledgeService.ImportMinimum(TestDomain, "source", pawn, 20f, 10f, 5f,
                    new Dictionary<string, int> { ["legacy"] = 2 });
                Check("idempotent V1 import", importOne && importTwo &&
                    KnowledgeQuery.Facet(TestDomain, "source", "identity", pawn).amount >= 80f &&
                    KnowledgeQuery.Facet(TestDomain, "source", "identity", null, KnowledgeScope.Colony).amount >= 40f &&
                    KnowledgeQuery.Facet(TestDomain, "source", "identity", pawn).EventCount("legacy") == 4 &&
                    KnowledgeQuery.Expertise(TestDomain, pawn, "field").amount >= 25f, ref passed, failures);
                Check("duplicate registration rejection", !KnowledgeRegistry.RegisterDomain(registration), ref passed, failures);
                Check("large dynamic subject enumeration", KnowledgeRegistry.Subjects(TestDomain).Count >= 5000, ref passed, failures);
            }
            catch (Exception exception)
            {
                failures.Add("game verification exception (expected=no exception actual=" + exception + ")");
            }
            finally
            {
                if (KnowledgeFrameworkMod.Settings != null) KnowledgeFrameworkMod.Settings.BioPanelEnabled = previousBioPanelEnabled;
                if (bioProviderRegistered) KnowledgeProviderRegistry.Unregister("verification.bio");
                component.RemoveDomainDataV2(TestDomain);
                KnowledgeRegistry.UnregisterDomain(TestDomain, "verification");
                KnowledgeRegistry.ClearDiagnostics(TestDomain);
            }
            KnowledgeVerificationResult v3 = KnowledgeFrameworkVerificationV3.RunGameTests(pawn);
            int v2GamePassed = Math.Max(0, passed - pure.passed);
            int v2GameFailed = Math.Max(0, failures.Count - pure.failures.Count);
            passed += v3.passed;
            failures.AddRange(v3.failures.Select(value => "v3 " + value));
            return new KnowledgeVerificationResult(passed, failures,
                KnowledgeVerificationTrace.End().Concat(pure.passedTests).Concat(v3.passedTests), 0,
                v3.unavailable, pure.passed, pure.failures.Count,
                v2GamePassed + v3.gamePassed, v2GameFailed + v3.gameFailed);
        }

        [DebugAction("Knowledge Framework", "Run complete V2/V3 behavioral verification", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RunFromDebugMenu()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn ?? Find.CurrentMap?.mapPawns.FreeColonists.FirstOrDefault();
            KnowledgeVerificationResult result = RunGameTests(pawn);
            string reportPath = WriteReport(result);
            Log.Message("[Knowledge Framework] Behavioral verification " + (result.Success ? "PASS" : "FAIL") +
                ": pure=" + result.purePassed + "/" + (result.purePassed + result.pureFailed) +
                " game=" + result.gamePassed + "/" + (result.gamePassed + result.gameFailed) +
                " unavailable=" + result.unavailable + " report=" + reportPath);
            if (!result.Success)
                Log.Warning("[Knowledge Framework] Behavioral verification failures: " + string.Join("; ", result.failures));
            Messages.Message(result.Success ? "KnowledgeFramework_VerificationPassed".Translate() : "KnowledgeFramework_VerificationFailed".Translate(),
                result.Success ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput, false);
        }

        private static string WriteReport(KnowledgeVerificationResult result)
        {
            string path = Path.Combine(GenFilePaths.ConfigFolderPath, "KnowledgeFramework_Verification.txt");
            try
            {
                StringBuilder report = new StringBuilder();
                report.AppendLine("Knowledge Framework behavioral verification");
                report.AppendLine("pure passed=" + result.purePassed + " failed=" + result.pureFailed + " skipped=" + result.skipped + " unavailable=0");
                report.AppendLine("game-state passed=" + result.gamePassed + " failed=" + result.gameFailed + " skipped=0 unavailable=" + result.unavailable);
                report.AppendLine("manual-ui passed=0 failed=0 skipped=0 unavailable=1");
                report.AppendLine("passed-tests=" + string.Join(";", result.passedTests));
                report.AppendLine("failures=" + string.Join(";", result.failures));
                File.WriteAllText(path, report.ToString());
                return path;
            }
            catch (Exception exception)
            {
                Log.Warning("[Knowledge Framework] Could not write verification report: " + exception);
                return "unavailable";
            }
        }

        private static KnowledgeObservation NewObservation(Pawn pawn, string facetId, float knowledge, bool success, float expertise)
        {
            return new KnowledgeObservation
            {
                observer = pawn,
                domainId = TestDomain,
                subjectId = "source",
                facetId = facetId,
                directKnowledge = knowledge,
                directExpertise = expertise,
                expertiseTrackId = "field",
                suppressConfiguredKnowledge = true,
                success = success,
                reasonId = "observed",
                source = "verification"
            };
        }

        private static KnowledgeFacetDef NewFacet(string id, float amount) => new KnowledgeFacetDef
        {
            defName = id,
            label = id,
            stableId = id,
            completenessAmount = amount,
            personallyKnowable = true,
            shareable = true,
            documentable = true
        };

        private static void Check(string name, bool condition, ref int passed, List<string> failures)
        {
            KnowledgeVerificationTrace.Record(name, condition);
            if (condition) passed++;
            else failures.Add(name + " (expected=true, actual=false)");
        }
    }

    internal static class KnowledgeVerificationTrace
    {
        [ThreadStatic]
        private static Stack<List<string>> stacks;

        internal static void Begin()
        {
            if (stacks == null) stacks = new Stack<List<string>>();
            stacks.Push(new List<string>());
        }

        internal static void Record(string name, bool passed)
        {
            if (passed && stacks != null && stacks.Count > 0 && !name.NullOrEmpty())
                stacks.Peek().Add(name);
        }

        internal static IReadOnlyList<string> End()
        {
            if (stacks == null || stacks.Count == 0) return new List<string>().AsReadOnly();
            return stacks.Pop().Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
        }
    }
}
