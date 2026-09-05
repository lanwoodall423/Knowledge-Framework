using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace KnowledgeFramework.Development
{
    // This is intentionally a debug action rather than a gameplay feature. The
    // namespace, source owner, and cleanup contract keep the generated state
    // separate from player content.
    internal static class KnowledgeFrameworkStressVerification
    {
        private const string Owner = "KnowledgeFramework.Development.Stress";
        private const string DomainId = "knowledgeframework.development.stress";
        private const string LegacyDomainId = "knowledgeframework.development.stress.legacy";
        private const string LegacySubjectId = "subject-legacy";
        private const string SubjectPrefix = "subject-";
        private const string FacetObservation = "observation";
        private const string FacetContext = "context";
        private const string ClaimId = "temperature";
        private const string RecipeId = "field-observation";
        private const string ExpertiseTrackId = "fieldcraft";
        private const string MilestoneTrackId = "progress";
        private const string MilestoneId = "documented";
        private const string RelationTypeId = "observed-from";
        private const string ExpertiseNamespaceId = "stress-expertise";
        private const string ContextTypeId = "stress.context";
        private const string ConsumerId = "knowledgeframework.development.stress.consumer";
        private const int SubjectCount = 5000;
        private const int RequiredPawns = 20;
        private const int ClaimMeasurements = 80;
        private const int ContextCount = 8;

        private sealed class StressResult
        {
            internal readonly List<string> failures = new List<string>();
            internal readonly List<string> passed = new List<string>();
            internal readonly List<string> unavailableReasons = new List<string>();
            internal int skipped;
            internal bool unavailable;
            internal int subjectCount;
            internal int activePawnCount;
            internal int contextCount;
            internal int transactions;
            internal int claimMeasurements;
            internal int rebuilds;
            internal int deadOrWorldChecks;
            internal int orphanCount;
            internal long elapsedMilliseconds;
            internal long persistentBytes;
            internal KnowledgeDiagnosticsSnapshot diagnostics;

            internal void Check(string name, bool condition, string detail = null)
            {
                if (condition)
                {
                    passed.Add(name + (detail.NullOrEmpty() ? string.Empty : " (" + detail + ")"));
                    return;
                }
                failures.Add(name + (detail.NullOrEmpty() ? string.Empty : ": " + detail));
            }

            internal void Unavailable(string reason)
            {
                unavailable = true;
                skipped++;
                unavailableReasons.Add(reason);
            }

            internal string Status => unavailable ? "BLOCKED/UNAVAILABLE" : failures.Count == 0 ? "PASS" : "FAIL";
        }

        [DebugAction("Knowledge Framework", "Run bounded V3 stress validation", actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RunFromDebugMenu()
        {
            StressResult result = Run();
            string reportPath = WriteReport(result);
            Log.Message("[Knowledge Framework] V3 stress validation " + result.Status +
                ": subjects=" + result.subjectCount + " pawns=" + result.activePawnCount +
                " transactions=" + result.transactions + " claims=" + result.claimMeasurements +
                " rebuilds=" + result.rebuilds + " bytes=" + result.persistentBytes +
                " elapsedMs=" + result.elapsedMilliseconds + " report=" + reportPath);
            if (result.failures.Count > 0)
                Log.Warning("[Knowledge Framework] V3 stress failures: " + string.Join("; ", result.failures));
            if (result.unavailableReasons.Count > 0)
                Log.Warning("[Knowledge Framework] V3 stress unavailable checks: " + string.Join("; ", result.unavailableReasons));
            Messages.Message(result.Status == "PASS" ? "KnowledgeFramework_VerificationPassed".Translate() :
                "KnowledgeFramework_VerificationFailed".Translate(),
                result.Status == "PASS" ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput, false);
            if (result.Status != "PASS")
                throw new InvalidOperationException("Knowledge Framework V3 stress validation " + result.Status +
                    (result.failures.Count > 0 ? ": " + string.Join("; ", result.failures) :
                    result.unavailableReasons.Count > 0 ? ": " + string.Join("; ", result.unavailableReasons) : string.Empty));
        }

        internal static string ReportPath => Path.Combine(GenFilePaths.ConfigFolderPath, "KnowledgeFramework_Stress.txt");

        internal static object RunForVerification()
        {
            return Run();
        }

        private static StressResult Run()
        {
            StressResult result = new StressResult();
            Stopwatch stopwatch = Stopwatch.StartNew();
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            List<Pawn> pawns = Find.CurrentMap?.mapPawns?.AllPawnsSpawned
                ?.Where(pawn => pawn != null && !pawn.Dead && !pawn.Destroyed).Distinct().Take(50).ToList() ??
                new List<Pawn>();
            result.activePawnCount = pawns.Count;
            KnowledgeSchema existing = KnowledgeRegistry.Schema(DomainId);
            bool ownsExistingDomain = existing != null && existing.source == Owner;
            bool ownsRunDomain = false;

            if (component == null || Find.CurrentMap == null || pawns.Count < RequiredPawns)
            {
                result.Unavailable("requires an active map and at least " + RequiredPawns + " live pawns");
                result.elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                return result;
            }
            if (existing != null && !ownsExistingDomain)
            {
                result.Unavailable("stress domain is already owned by another registration");
                result.elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                return result;
            }

            try
            {
                if (ownsExistingDomain)
                {
                    component.RemoveDomainDataV2(DomainId);
                    KnowledgeRegistry.UnregisterDomain(DomainId, Owner);
                }

                ownsRunDomain = RegisterDomain();
                result.Check("domain registration", ownsRunDomain);
                if (!ownsRunDomain)
                {
                    result.Unavailable("stress domain registration was unavailable");
                    return result;
                }
                // Register aliases before dynamic subjects are enumerated so the
                // immediate migration path is exercised without leaking aliases
                // when domain registration itself fails.
                result.Check("domain alias before subject enumeration", KnowledgeRegistry.RegisterDomainAlias(LegacyDomainId, DomainId));
                result.Check("subject alias before subject enumeration", KnowledgeRegistry.RegisterSubjectAlias(DomainId, LegacySubjectId, SubjectPrefix + "00000"));
                result.Check("relation type registration", KnowledgeRelationService.RegisterType(new KnowledgeSubjectRelationTypeDef
                {
                    defName = RelationTypeId,
                    stableId = RelationTypeId,
                    metadataLimit = 4
                }, true));
                result.Check("expertise namespace registration", KnowledgeSharedExpertiseService.RegisterNamespace(new KnowledgeExpertiseNamespaceDef
                {
                    defName = ExpertiseNamespaceId,
                    stableId = ExpertiseNamespaceId,
                    contributionLimit = 64
                }, true));

                List<KnowledgeSubjectSnapshot> subjects = KnowledgeRegistry.Subjects(DomainId).ToList();
                result.subjectCount = subjects.Count;
                result.contextCount = ContextCount;
                result.Check("5000 dynamic subjects", subjects.Count == SubjectCount, subjects.Count.ToString());

                List<KnowledgeContextKey> contexts = Enumerable.Range(0, ContextCount)
                    .Select(index => new KnowledgeContextKey(ContextTypeId, "context-" + index.ToString("D3")))
                    .ToList();
                Pawn primary = pawns[0];
                List<Pawn> witnesses = pawns.Skip(1).Take(20).ToList();
                KnowledgeDiagnosticsSnapshot before = KnowledgeDiagnostics.Snapshot();

                for (int index = 0; index < ClaimMeasurements; index++)
                {
                    KnowledgeTransactionResult transaction = KnowledgeEngine.Submit(new KnowledgeObservation
                    {
                        observer = primary,
                        domainId = DomainId,
                        subjectId = SubjectPrefix + "00000",
                        observationId = RecipeId,
                        expertiseTrackId = ExpertiseTrackId,
                        directExpertise = 1f,
                        logicalEventId = "stress-event-" + index.ToString("D4"),
                        sourceInstanceId = "stress-source-" + index.ToString("D4"),
                        context = contexts[index < ContextCount ? index : 0],
                        witnesses = witnesses,
                        summary = "bounded stress observation",
                        specimenId = "specimen-" + (index % 16).ToString("D2"),
                        source = Owner,
                        notify = false
                    });
                    result.transactions++;
                    if (transaction == null || !transaction.success)
                    {
                        result.failures.Add("recipe transaction " + index + ": " + (transaction?.error ?? "no result"));
                        break;
                    }
                }
                result.claimMeasurements = component.V3MeasurementCount;
                result.Check("recipe and witness fan-out", result.transactions == ClaimMeasurements,
                    "transactions=" + result.transactions + " witnesses=" + witnesses.Count);
                result.Check("multiple contextual records", component.V3ContextCount >= ContextCount,
                    "contexts=" + component.V3ContextCount);
                result.Check("expertise track registration",
                    KnowledgeRegistry.Schema(DomainId)?.ExpertiseTrack(ExpertiseTrackId) != null);

                KnowledgeMeasurement longTickMeasurement = new KnowledgeMeasurement
                {
                    domainId = DomainId,
                    subjectId = SubjectPrefix + "00000",
                    facetId = FacetObservation,
                    claimId = ClaimId,
                    observer = primary,
                    value = KnowledgeClaimValue.Float(19f),
                    context = contexts[0],
                    source = Owner,
                    sourceInstanceId = "stress-long-tick",
                    tick = int.MaxValue - 1
                };
                KnowledgeObservation longTickObservation = new KnowledgeObservation
                {
                    observer = primary,
                    domainId = DomainId,
                    subjectId = SubjectPrefix + "00000",
                    context = contexts[0],
                    notify = false
                };
                string longTickError;
                bool longTickValid = KnowledgeClaimService.ValidateMeasurement(longTickMeasurement, longTickObservation, out longTickError);
                result.Check("long tick measurement validation", longTickValid, longTickError);
                result.Check("long tick measurement application", longTickValid && KnowledgeClaimService.Apply(component,
                    longTickMeasurement, longTickObservation));
                result.claimMeasurements = component.V3MeasurementCount;
                int stressAccrualCount = component.AccrualRecordsV3().Count(item => item != null &&
                    KnowledgeRegistry.ResolveDomainId(item.domainId) == DomainId);
                result.Check("accrual state limit", stressAccrualCount <= 64,
                    "accruals=" + stressAccrualCount);

                KnowledgeClaimSnapshot claim = KnowledgeClaimService.Snapshot(DomainId, SubjectPrefix + "00000",
                    FacetObservation, ClaimId, primary, KnowledgeScope.Personal, contexts[0]);
                result.Check("claim history bound", claim != null && claim.observationCount <= 64,
                    "observations=" + (claim?.observationCount ?? -1));
                result.Check("provenance bound", claim != null && claim.provenance.Count <= 8,
                    "provenance=" + (claim?.provenance.Count ?? -1));

                KnowledgeContextKey aliasContext = contexts[0];
                KnowledgeClaimSnapshot aliasClaim = KnowledgeClaimService.Snapshot(LegacyDomainId, LegacySubjectId,
                    FacetObservation, ClaimId, primary, KnowledgeScope.Personal, aliasContext);
                result.Check("alias query is immediate", aliasClaim != null && aliasClaim.observationCount > 0);

                KnowledgeSubjectRelation relation = new KnowledgeSubjectRelation
                {
                    domainId = DomainId,
                    fromSubjectId = SubjectPrefix + "00000",
                    toDomainId = DomainId,
                    toSubjectId = SubjectPrefix + "00001",
                    relationTypeId = RelationTypeId,
                    confidence = 1f,
                    context = contexts[0],
                    source = Owner,
                    tick = int.MaxValue - 2,
                    metadata = new Dictionary<string, string> { { "fixture", "stress" } }
                };
                result.Check("structural relation", KnowledgeRelationService.Add(relation, false));
                result.Check("milestone state", KnowledgeMilestoneService.Confirm(DomainId, SubjectPrefix + "00000",
                    MilestoneTrackId, MilestoneId, primary, contexts[0]));

                KnowledgeContextFacetStateRecord orphanFacet = component.ContextFacetV3(DomainId, "orphaned-subject",
                    FacetContext, primary, false, contexts[1], true);
                orphanFacet.amount = 1f;
                KnowledgeClaimStateRecord orphanClaim = component.ClaimV3(DomainId, "orphaned-subject",
                    FacetObservation, ClaimId, primary, false, contexts[1], true);
                orphanClaim.measurements.Add(KnowledgeMeasurementRecord.FromMeasurement(new KnowledgeMeasurement
                {
                    domainId = DomainId,
                    subjectId = "orphaned-subject",
                    facetId = FacetObservation,
                    claimId = ClaimId,
                    observer = primary,
                    value = KnowledgeClaimValue.Float(1f),
                    context = contexts[1],
                    tick = int.MaxValue - 1,
                    source = Owner
                }, DomainId, "orphaned-subject", FacetObservation, primary, KnowledgeScope.Personal));
                component.RebuildV3Indexes();
                result.orphanCount = component.V3OrphanCount;
                result.Check("orphaned content is reported", result.orphanCount > 0, "orphans=" + result.orphanCount);

                string indexFingerprint = IndexFingerprint(component);
                for (int index = 0; index < 3; index++)
                {
                    component.RebuildV3Indexes();
                    result.rebuilds++;
                    result.Check("repeated index reconstruction " + index, indexFingerprint == IndexFingerprint(component));
                }

                KnowledgeBrowserFilter filter = new KnowledgeBrowserFilter
                {
                    domainId = DomainId,
                    pawn = primary,
                    scope = KnowledgeScope.Personal,
                    context = contexts[0],
                    fallback = KnowledgeContextFallbackMode.ParentThenGlobal,
                    search = "subject-000"
                };
                IReadOnlyList<KnowledgeContextKey> firstOptions = KnowledgeBrowserModels.ContextOptions(filter);
                KnowledgeBrowserRow firstRow = KnowledgeBrowserModels.BuildSubject(filter, SubjectPrefix + "00000");
                IReadOnlyList<KnowledgeContextKey> secondOptions = KnowledgeBrowserModels.ContextOptions(filter);
                KnowledgeBrowserRow secondRow = KnowledgeBrowserModels.BuildSubject(filter, SubjectPrefix + "00000");
                result.Check("browser filtering and deterministic cache inputs", firstOptions.SequenceEqual(secondOptions) &&
                    firstRow != null && secondRow != null && firstRow.displayLabel == secondRow.displayLabel);

                List<Pawn> deadOrWorld = FindWorldPawns().Where(pawn => pawn != null && !pawns.Contains(pawn)).Take(4).ToList();
                foreach (Pawn pawn in deadOrWorld)
                {
                    KnowledgeClaimService.Snapshot(DomainId, SubjectPrefix + "00000", FacetObservation, ClaimId,
                        pawn, KnowledgeScope.Personal, contexts[0]);
                    result.deadOrWorldChecks++;
                }
                if (deadOrWorld.Count == 0)
                    result.Unavailable("no dead or world pawn was available for the safe query check");
                KnowledgeClaimSnapshot longTickClaim = KnowledgeClaimService.Snapshot(DomainId, SubjectPrefix + "00000",
                    FacetObservation, ClaimId, primary, KnowledgeScope.Personal, contexts[0]);
                result.Check("long-running tick query", longTickClaim != null &&
                    longTickClaim.lastConfirmedTick >= int.MaxValue - 10,
                    "lastTick=" + (longTickClaim?.lastConfirmedTick ?? -1));

                KnowledgeConsumerMigration migration = new KnowledgeConsumerMigration
                {
                    consumerId = ConsumerId,
                    version = 1,
                    domainId = DomainId,
                    subjectId = SubjectPrefix + "00000",
                    pawn = primary,
                    personalKnowledge = 1f,
                    colonyKnowledge = 1f,
                    expertise = 1f,
                    eventCounts = new Dictionary<string, int> { { "stress", 1 } }
                };
                result.Check("consumer migration import", KnowledgeMigrationService.Import(migration));
                component.RemoveDomainDataV2(DomainId);
                result.Check("consumer data removal", !component.ClaimRecordsV3(DomainId).Any() &&
                    !component.RelationRecordsV3(DomainId).Any());
                result.Check("consumer record survives removal", KnowledgeMigrationService.IsCommitted(ConsumerId, 1));
                result.Check("consumer restoration", KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
                {
                    consumerId = ConsumerId,
                    version = 2,
                    domainId = DomainId,
                    subjectId = SubjectPrefix + "00000",
                    pawn = primary,
                    personalKnowledge = 1f,
                    colonyKnowledge = 1f,
                    expertise = 1f,
                    eventCounts = new Dictionary<string, int> { { "stress", 2 } }
                }));

                KnowledgeDiagnosticsSnapshot after = KnowledgeDiagnostics.Snapshot();
                result.diagnostics = after;
                result.persistentBytes = component.ApproximatePersistentBytes();
                result.Check("persistence estimate", result.persistentBytes >= 0,
                    before.approximatePersistentBytes + "->" + result.persistentBytes);
            }
            catch (Exception exception)
            {
                result.failures.Add("unexpected stress exception: " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                try
                {
                    if (ownsRunDomain || ownsExistingDomain)
                    {
                        component.RemoveDomainDataV2(DomainId);
                        KnowledgeRegistry.UnregisterDomain(DomainId, Owner);
                    }
                    KnowledgeRegistry.ClearDiagnostics(Owner);
                }
                catch (Exception exception)
                {
                    result.failures.Add("stress cleanup: " + exception.GetType().Name + ": " + exception.Message);
                }
                result.elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            }
            return result;
        }

        private static bool RegisterDomain()
        {
            KnowledgeFacetDef observationFacet = new KnowledgeFacetDef
            {
                defName = FacetObservation,
                stableId = FacetObservation,
                label = FacetObservation,
                completenessAmount = 1f,
                personallyKnowable = true,
                shareable = true,
                documentable = true,
                claimIds = new List<string> { ClaimId }
            };
            KnowledgeFacetDef contextFacet = new KnowledgeFacetDef
            {
                defName = FacetContext,
                stableId = FacetContext,
                label = FacetContext,
                completenessAmount = 1f,
                personallyKnowable = true,
                shareable = true,
                documentable = true
            };
            KnowledgeClaimDef claim = new KnowledgeClaimDef
            {
                defName = ClaimId,
                stableId = ClaimId,
                domainId = DomainId,
                facetId = FacetObservation,
                valueType = KnowledgeClaimValueType.Float,
                measurementHistoryLimit = 64,
                provenanceLimit = 8,
                revealedByDefault = true,
                documentable = true
            };
            KnowledgeObservationDef recipe = new KnowledgeObservationDef
            {
                defName = RecipeId,
                stableId = RecipeId,
                baseKnowledge = 1f,
                baseExpertise = 1f,
                shareable = true,
                retainProvenance = true,
                facetIds = new List<string> { FacetObservation },
                witnessDistribution = new KnowledgeWitnessDistribution
                {
                    policy = KnowledgeWitnessDistributionPolicy.WitnessesReduced,
                    efficiency = 0.5f,
                    expertiseEfficiency = 0.5f,
                    maximumRecipients = 20
                },
                accrualPolicy = new KnowledgeAccrualPolicy
                {
                    uniquePerSourceInstance = true,
                    uniquePerSubjectAndContext = true,
                    stateLimit = 64,
                    cooldownTicks = 1
                },
                successOutcomes = new List<KnowledgeObservationOutcome>
                {
                    new KnowledgeObservationOutcome
                    {
                        facetId = FacetObservation,
                        knowledge = 1f,
                        evidenceWeight = 1f,
                        claimMeasurements = new List<KnowledgeMeasurement>
                        {
                            new KnowledgeMeasurement
                            {
                                facetId = FacetObservation,
                                claimId = ClaimId,
                                value = KnowledgeClaimValue.Float(18.5f),
                                quality = 1f,
                                evidenceWeight = 1f,
                                confidenceFactor = 1f
                            }
                        }
                    }
                },
                expertiseOutcomes = new List<KnowledgeExpertiseOutcome>
                {
                    new KnowledgeExpertiseOutcome { trackId = ExpertiseTrackId, expertise = 1f }
                }
            };
            KnowledgeDomainRegistration registration = new KnowledgeDomainRegistration
            {
                id = DomainId,
                label = DomainId,
                source = Owner,
                stageAggregationMode = KnowledgeStageAggregationMode.Balanced,
                sharingModel = KnowledgeSharingModel.Documented,
                provenanceLimit = 8,
                facets = new[] { observationFacet, contextFacet },
                claims = new[] { claim },
                observations = new[] { recipe },
                expertiseTracks = new[] { new KnowledgeExpertiseTrackDef { defName = ExpertiseTrackId, stableId = ExpertiseTrackId } },
                stages = new[]
                {
                    new KnowledgeStageDef { defName = "base", label = "base", order = 0, minimumKnowledge = 0.1f },
                    new KnowledgeStageDef { defName = "contextual", label = "contextual", order = 1, contextSensitive = true, minimumKnowledge = 0.1f }
                },
                milestoneTracks = new[]
                {
                    new KnowledgeMilestoneTrackDef
                    {
                        defName = MilestoneTrackId,
                        stableId = MilestoneTrackId,
                        domainId = DomainId,
                        milestones = new List<KnowledgeMilestoneDef>
                        {
                            new KnowledgeMilestoneDef { stableId = MilestoneId, label = MilestoneId, permanent = true }
                        }
                    }
                },
                subjectResolver = ResolveSubject,
                subjectSource = () => Enumerable.Range(0, SubjectCount).Select(CreateSubject).ToList()
            };
            return KnowledgeRegistry.RegisterDomain(registration, new KnowledgeRegistrationOptions
            {
                source = Owner,
                priority = int.MaxValue,
                conflict = KnowledgeRegistrationConflict.Replace
            });
        }

        private static KnowledgeSubjectRegistration ResolveSubject(string id)
        {
            int index;
            return TrySubjectIndex(id, out index) ? CreateSubject(index) : null;
        }

        private static KnowledgeSubjectRegistration CreateSubject(int index)
        {
            string id = SubjectPrefix + index.ToString("D5");
            return new KnowledgeSubjectRegistration
            {
                id = id,
                label = id,
                description = "Bounded development stress subject",
                source = Owner,
                state = KnowledgeSubjectState.Active,
                applicableFacetIds = new[] { FacetObservation, FacetContext },
                applicableClaimIds = new[] { ClaimId }
            };
        }

        private static bool TrySubjectIndex(string id, out int index)
        {
            index = -1;
            if (id.NullOrEmpty() || !id.StartsWith(SubjectPrefix, StringComparison.Ordinal) ||
                !int.TryParse(id.Substring(SubjectPrefix.Length), out index)) return false;
            return index >= 0 && index < SubjectCount;
        }

        private static IEnumerable<Pawn> FindWorldPawns()
        {
            object worldPawns = Find.WorldPawns;
            if (worldPawns == null) return Enumerable.Empty<Pawn>();
            PropertyInfo property = worldPawns.GetType().GetProperty("AllPawnsAliveOrDead",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property?.GetValue(worldPawns, null) as IEnumerable<Pawn> ?? Enumerable.Empty<Pawn>();
        }

        private static string IndexFingerprint(GameComponent_KnowledgeFramework component)
        {
            return component.V3ClaimCount + ":" + component.V3MeasurementCount + ":" + component.V3ContextCount + ":" +
                component.V3MilestoneCount + ":" + component.V3StageCount + ":" + component.V3RelationCount + ":" +
                component.V3AccrualCount + ":" + component.V3SubjectOverrideCount + ":" + component.ApproximatePersistentBytes();
        }

        private static string WriteReport(StressResult result)
        {
            try
            {
                StringBuilder report = new StringBuilder();
                report.AppendLine("Knowledge Framework bounded V3 stress validation");
                report.AppendLine("status=" + result.Status);
                report.AppendLine("subjects=" + result.subjectCount);
                report.AppendLine("active-pawns=" + result.activePawnCount);
                report.AppendLine("contexts=" + result.contextCount);
                report.AppendLine("transactions=" + result.transactions);
                report.AppendLine("claim-measurements=" + result.claimMeasurements);
                report.AppendLine("rebuilds=" + result.rebuilds);
                report.AppendLine("dead-world-pawn-checks=" + result.deadOrWorldChecks);
                report.AppendLine("orphan-count=" + result.orphanCount);
                report.AppendLine("approximate-persistent-bytes=" + result.persistentBytes);
                report.AppendLine("elapsed-milliseconds=" + result.elapsedMilliseconds);
                if (result.diagnostics != null)
                {
                    report.AppendLine("diagnostics.cache-hits=" + result.diagnostics.cacheHits);
                    report.AppendLine("diagnostics.cache-misses=" + result.diagnostics.cacheMisses);
                    report.AppendLine("diagnostics.witness-fanout=" + result.diagnostics.witnessFanout);
                    report.AppendLine("diagnostics.recipe-fanout=" + result.diagnostics.recipeFanout);
                    report.AppendLine("diagnostics.v3-claims=" + result.diagnostics.claimCount);
                    report.AppendLine("diagnostics.v3-measurements=" + result.diagnostics.measurementCount);
                    report.AppendLine("diagnostics.v3-contexts=" + result.diagnostics.contextCount);
                }
                report.AppendLine("skipped=" + result.skipped);
                report.AppendLine("unavailable=" + string.Join(";", result.unavailableReasons));
                report.AppendLine("passed=" + string.Join(";", result.passed));
                report.AppendLine("failures=" + string.Join(";", result.failures));
                File.WriteAllText(ReportPath, report.ToString());
                return ReportPath;
            }
            catch (Exception exception)
            {
                Log.Warning("[Knowledge Framework] Could not write stress report: " + exception);
                return "unavailable";
            }
        }
    }
}
