using System;
using System.Collections.Generic;
using KnowledgeFramework;
using Verse;

namespace ReferenceConsumer
{
    public sealed class ReferenceConsumerMod : Mod
    {
        public ReferenceConsumerMod(ModContentPack content) : base(content)
        {
            LongEventHandler.ExecuteWhenFinished(ReferenceConsumerRuntime.Initialize);
        }
    }

    public static class ReferenceConsumerRuntime
    {
        public const string DomainId = "reference.field-guide";
        public const string DynamicSubjectId = "observed-specimen";
        public const string StaticSubjectId = "reference-specimen";
        public const string FacetId = "observation";
        public const string ClaimId = "temperature";
        public const string ObservationId = "field-observation";
        public const string ContextTypeId = "reference.region";
        public const string ContextId = "north-field";
        public const string MilestoneTrackId = "field-guide";
        public const string MilestoneId = "documented";
        public const string RelationTypeId = "reference.observed-from";
        public const string ConsumerId = "lan.knowledgeframework.referenceconsumer";
        public const string ConsumerDomainId = "reference.consumer-runtime";
        private static bool initialized;

        public static bool IsCompatible => KnowledgeFrameworkApi.Supports(3, KnowledgeFrameworkApi.TypedMeasurementsCapability) &&
            KnowledgeFrameworkApi.Supports(3, KnowledgeFrameworkApi.StructuralRelationsCapability) &&
            KnowledgeFrameworkApi.Supports(3, KnowledgeFrameworkApi.ReadinessInspectionCapability) &&
            KnowledgeFrameworkApi.Supports(3, KnowledgeFrameworkApi.SafeRegistrationCapability) &&
            KnowledgeFrameworkApi.Supports(3, KnowledgeFrameworkApi.RegistrationOwnershipCapability) &&
            KnowledgeFrameworkApi.Supports(3, KnowledgeFrameworkApi.TargetedInvalidationCapability) &&
            KnowledgeFrameworkApi.CapabilityVersion(KnowledgeFrameworkApi.TargetedInvalidationCapability) ==
                KnowledgeFrameworkApi.ThirdGenerationApiVersion;

        public static string Release => KnowledgeFrameworkApi.ReleaseVersion;
        public static KnowledgeFrameworkReadinessStatus Readiness => KnowledgeConsumerApi.Readiness;

        public static KnowledgeDomainRegistration RuntimeDomainRegistration() => new KnowledgeDomainRegistration
        {
            id = ConsumerDomainId,
            label = "Reference Consumer Runtime",
            description = "A consumer-owned runtime domain used to demonstrate safe registration.",
            source = ConsumerId,
            facets = new[] { new KnowledgeFacetDef { defName = "reference-runtime-facet", stableId = "runtime" } }
        };

        public static KnowledgeDomainRegistrationInspection RuntimeDomainOwnership() =>
            KnowledgeConsumerApi.InspectDomainRegistration(RuntimeDomainRegistration(), new KnowledgeRegistrationOptions
            {
                source = ConsumerId,
                priority = 100
            });

        public static void Initialize()
        {
            try
            {
                if (initialized) return;
                if (!IsCompatible)
                {
                    Log.Warning("[ReferenceConsumer] Knowledge Framework V3 capabilities are unavailable; optional content is disabled.");
                    return;
                }
                KnowledgeFrameworkReadinessStatus readiness = KnowledgeConsumerApi.PrepareRegistration();
                if (!readiness.IsReady)
                {
                    Log.Warning("[ReferenceConsumer] Knowledge Framework is not ready (" + readiness.reason + "); optional content is disabled.");
                    return;
                }
                KnowledgeConsumerRegistrationResult runtimeRegistration = KnowledgeConsumerApi.RegisterDomain(
                    RuntimeDomainRegistration(), new KnowledgeRegistrationOptions
                    {
                        source = ConsumerId,
                        priority = 100
                    });
                if (!runtimeRegistration.Success)
                {
                    Log.Warning("[ReferenceConsumer] Consumer-owned runtime domain was not registered (" + runtimeRegistration.code + ").");
                    return;
                }
                if (KnowledgeRegistry.Schema(DomainId) == null)
                {
                    Log.Warning("[ReferenceConsumer] Domain Def is unavailable; the reference consumer will remain inactive.");
                    return;
                }
                bool subjectRegistered = KnowledgeRegistry.RegisterSubject(DomainId, new KnowledgeSubjectRegistration
                {
                    id = DynamicSubjectId,
                    label = "ReferenceConsumer_DynamicSubject".Translate(),
                    description = "ReferenceConsumer_DynamicSubjectDescription".Translate(),
                    applicableFacetIds = new[] { FacetId, "habitat" },
                    applicableClaimIds = new[] { ClaimId },
                    source = ConsumerId,
                    state = KnowledgeSubjectState.Active
                }, new KnowledgeRegistrationOptions
                {
                    source = ConsumerId,
                    priority = 100,
                    conflict = KnowledgeRegistrationConflict.Reject
                });
                if (!subjectRegistered && KnowledgeRegistry.ResolveSubject(DomainId, DynamicSubjectId) == null)
                {
                    Log.Warning("[ReferenceConsumer] Dynamic subject registration was unavailable; optional content will retry on next use.");
                    return;
                }
                initialized = true;
            }
            catch (Exception exception)
            {
                Log.Warning("[ReferenceConsumer] Optional initialization failed safely: " + exception.Message);
            }
        }

        private static bool EnsureInitialized()
        {
            if (!initialized) Initialize();
            return initialized;
        }

        public static KnowledgeTransactionResult Observe(Pawn observer)
        {
            if (observer == null || !EnsureInitialized()) return null;
            KnowledgeContextKey context = new KnowledgeContextKey(ContextTypeId, ContextId);
            return KnowledgeEngine.Submit(new KnowledgeObservation
            {
                observer = observer,
                domainId = DomainId,
                subjectId = DynamicSubjectId,
                facetId = FacetId,
                observationId = ObservationId,
                logicalEventId = "reference-observation-" + DynamicSubjectId,
                source = ConsumerId,
                sourceInstanceId = ConsumerId,
                context = context,
                contextTypeId = context.typeId,
                contextId = context.stableId,
                quality = 1f,
                sourceReliability = 1f,
                directExpertise = 4f,
                expertiseTrackId = "fieldcraft",
                claimMeasurements = new List<KnowledgeMeasurement>
                {
                    new KnowledgeMeasurement
                    {
                        domainId = DomainId,
                        subjectId = DynamicSubjectId,
                        facetId = FacetId,
                        claimId = ClaimId,
                        observer = observer,
                        scope = KnowledgeScope.Personal,
                        value = KnowledgeClaimValue.Float(18.5f),
                        context = context,
                        quality = 1f,
                        evidenceWeight = 1f,
                        confidenceFactor = 1f,
                        source = ConsumerId,
                        sourceInstanceId = ConsumerId,
                        documented = false,
                        revealed = true
                    }
                }
            });
        }

        public static bool ReportAndDocument(Pawn observer)
        {
            if (observer == null || !EnsureInitialized()) return false;
            bool reported = KnowledgeTransmission.Report(DomainId, DynamicSubjectId, observer, ConsumerId);
            bool documented = KnowledgeTransmission.Document(DomainId, DynamicSubjectId, observer, ConsumerId);
            return reported && documented;
        }

        public static bool ConfirmMilestone(Pawn pawn)
        {
            if (pawn == null || !EnsureInitialized()) return false;
            return KnowledgeMilestoneService.Confirm(DomainId, DynamicSubjectId, MilestoneTrackId, MilestoneId, pawn,
                new KnowledgeContextKey(ContextTypeId, ContextId));
        }

        public static bool AddStructuralRelation()
        {
            if (!EnsureInitialized()) return false;
            return KnowledgeRelationService.Add(new KnowledgeSubjectRelation
            {
                domainId = DomainId,
                fromSubjectId = DynamicSubjectId,
                toDomainId = DomainId,
                toSubjectId = StaticSubjectId,
                relationTypeId = RelationTypeId,
                role = "observed-from",
                confidence = 1f,
                revealed = true,
                source = ConsumerId,
                metadata = new Dictionary<string, string> { { "fixture", "reference" } }
            }, false);
        }

        public static KnowledgeEffectResult QueryTypedEffect(Pawn pawn)
        {
            if (pawn == null || !EnsureInitialized()) return null;
            return KnowledgeEffects.Query(new KnowledgeEffectQuery
            {
                domainId = DomainId,
                subjectId = DynamicSubjectId,
                facetId = FacetId,
                channelId = "reference.field-guide.insight",
                pawn = pawn,
                scope = KnowledgeScope.Personal,
                baseValue = 1f,
                context = new KnowledgeContextKey(ContextTypeId, ContextId),
                includeExplanation = true
            });
        }

        public static KnowledgeMilestoneState Milestone(Pawn pawn)
        {
            if (pawn == null || !EnsureInitialized()) return null;
            return KnowledgeMilestoneService.State(DomainId, DynamicSubjectId, MilestoneTrackId, MilestoneId, pawn,
                new KnowledgeContextKey(ContextTypeId, ContextId));
        }

        public static KnowledgeInsightProgress OptionalInsight(Pawn pawn)
        {
            if (pawn == null || !EnsureInitialized()) return null;
            return KnowledgeInsightService.Progress("missing-optional-insight", DomainId, DynamicSubjectId, pawn);
        }

        public static void OpenGuide(Pawn pawn)
        {
            if (pawn == null || !EnsureInitialized()) return;
            KnowledgeV2Ui.Open(DomainId, pawn, DynamicSubjectId, new KnowledgeContextKey(ContextTypeId, ContextId));
        }

        public static bool MigrateConsumer(Pawn pawn)
        {
            if (pawn == null || !EnsureInitialized()) return false;
            return KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
            {
                consumerId = ConsumerId,
                version = 1,
                domainId = DomainId,
                subjectId = DynamicSubjectId,
                pawn = pawn,
                personalKnowledge = 0f,
                colonyKnowledge = 0f,
                expertise = 0f
            });
        }

        public static bool InvalidateObservedSpecimen() => EnsureInitialized() &&
            KnowledgeConsumerApi.InvalidateSubject(DomainId, DynamicSubjectId).Success;
    }
}
