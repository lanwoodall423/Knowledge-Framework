using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    public enum KnowledgeTransmissionKind
    {
        Report,
        Document,
        Teach,
        Conversation,
        Read,
        ConsultArchive,
        RecruitKnowledge,
        Custom
    }

    public sealed class KnowledgeTransmissionRequest
    {
        public KnowledgeTransmissionKind kind;
        public string domainId;
        public string subjectId;
        public Pawn sourcePawn;
        public Pawn recipientPawn;
        public float knowledgeEfficiency = 1f;
        public float confidenceEfficiency = 0.8f;
        public string source;
        public bool document;
    }

    public static class KnowledgeTransmission
    {
        public static bool Report(string domainId, string subjectId, Pawn pawn, string source = null) =>
            Transfer(new KnowledgeTransmissionRequest { kind = KnowledgeTransmissionKind.Report, domainId = domainId,
                subjectId = subjectId, sourcePawn = pawn, source = source });

        public static bool Document(string domainId, string subjectId, Pawn pawn, string source = null) =>
            Transfer(new KnowledgeTransmissionRequest { kind = KnowledgeTransmissionKind.Document, domainId = domainId,
                subjectId = subjectId, sourcePawn = pawn, source = source, document = true });

        public static bool Teach(string domainId, string subjectId, Pawn teacher, Pawn student, float efficiency = 0.5f,
            string source = null) => Transfer(new KnowledgeTransmissionRequest { kind = KnowledgeTransmissionKind.Teach,
                domainId = domainId, subjectId = subjectId, sourcePawn = teacher, recipientPawn = student,
                knowledgeEfficiency = efficiency, confidenceEfficiency = efficiency, source = source });

        public static bool Mentor(string domainId, string subjectId, Pawn mentor, Pawn student, float efficiency = 0.75f,
            string source = null) => Transfer(new KnowledgeTransmissionRequest { kind = KnowledgeTransmissionKind.Teach,
                domainId = domainId, subjectId = subjectId, sourcePawn = mentor, recipientPawn = student,
                knowledgeEfficiency = efficiency, confidenceEfficiency = efficiency, source = source });

        public static bool Converse(string domainId, string subjectId, Pawn sourcePawn, Pawn recipientPawn, float efficiency = 0.25f,
            string source = null) => Transfer(new KnowledgeTransmissionRequest { kind = KnowledgeTransmissionKind.Conversation,
                domainId = domainId, subjectId = subjectId, sourcePawn = sourcePawn, recipientPawn = recipientPawn,
                knowledgeEfficiency = efficiency, confidenceEfficiency = efficiency, source = source });

        public static bool Read(string domainId, string subjectId, Pawn pawn, float efficiency = 0.75f,
            string source = null) => Transfer(new KnowledgeTransmissionRequest { kind = KnowledgeTransmissionKind.Read,
                domainId = domainId, subjectId = subjectId, recipientPawn = pawn, knowledgeEfficiency = efficiency,
                confidenceEfficiency = efficiency, source = source });

        public static bool Recruit(string domainId, string subjectId, Pawn recruitedPawn, float efficiency = 1f,
            string source = null) => Transfer(new KnowledgeTransmissionRequest { kind = KnowledgeTransmissionKind.RecruitKnowledge,
                domainId = domainId, subjectId = subjectId, sourcePawn = recruitedPawn, knowledgeEfficiency = efficiency,
                confidenceEfficiency = efficiency, source = source });

        public static bool ConsultArchive(string domainId, string subjectId, Pawn pawn, float efficiency = 0.5f,
            string source = null) => Transfer(new KnowledgeTransmissionRequest { kind = KnowledgeTransmissionKind.ConsultArchive,
                domainId = domainId, subjectId = subjectId, recipientPawn = pawn, knowledgeEfficiency = efficiency,
                confidenceEfficiency = efficiency, source = source });

        /// <summary>Call this from a consumer's pawn-loss/departure event when its schema opts into forgetting.</summary>
        public static int HandleObserverLoss(Pawn pawn)
        {
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component == null || pawn == null) return 0;
            int changed = 0;
            foreach (KnowledgeSchema schema in KnowledgeRegistry.SchemasSnapshot)
                if (schema.transmission?.survivesObserverLoss == false) changed += component.ForgetPawnDataV2(pawn, schema.id);
            if (changed > 0)
            {
                KnowledgeProviderRegistry.Invalidate(pawn);
                KnowledgeEngine.NotifyExternalChange("observer-loss");
            }
            return changed;
        }

        public static bool Transfer(KnowledgeTransmissionRequest request)
        {
            if (request == null || !KnowledgeMath.IsFinite(request.knowledgeEfficiency) ||
                !KnowledgeMath.IsFinite(request.confidenceEfficiency) || request.knowledgeEfficiency < 0f ||
                request.knowledgeEfficiency > 1f || request.confidenceEfficiency < 0f || request.confidenceEfficiency > 1f)
                return false;
            string domainId = KnowledgeRegistry.ResolveDomainId(request.domainId);
            string subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, request.subjectId);
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (schema == null || component == null || KnowledgeRegistry.ResolveSubject(domainId, subjectId) == null) return false;
            KnowledgeTransmissionDef def = schema.transmission;
            float knowledgeEfficiency = request.knowledgeEfficiency * (def?.knowledgeEfficiency ?? 1f);
            float confidenceEfficiency = request.confidenceEfficiency * (def?.confidenceEfficiency ?? 1f);

            switch (request.kind)
            {
                case KnowledgeTransmissionKind.Report:
                    if (schema.sharingModel != KnowledgeSharingModel.Reportable && schema.sharingModel != KnowledgeSharingModel.Custom) return false;
                    return CopyPersonalToColony(component, schema, subjectId, request.sourcePawn, knowledgeEfficiency,
                        confidenceEfficiency, false, request.source);
                case KnowledgeTransmissionKind.Document:
                    if (schema.sharingModel == KnowledgeSharingModel.Custom && !request.document) return false;
                    return CopyPersonalToColony(component, schema, subjectId, request.sourcePawn, knowledgeEfficiency,
                        confidenceEfficiency, true, request.source);
                case KnowledgeTransmissionKind.Teach:
                case KnowledgeTransmissionKind.Conversation:
                    return CopyPersonalToPersonal(component, schema, subjectId, request.sourcePawn, request.recipientPawn,
                        knowledgeEfficiency, confidenceEfficiency, request.source);
                case KnowledgeTransmissionKind.Read:
                case KnowledgeTransmissionKind.ConsultArchive:
                    return CopyColonyToPersonal(component, schema, subjectId, request.recipientPawn,
                        knowledgeEfficiency, confidenceEfficiency, request.source);
                case KnowledgeTransmissionKind.RecruitKnowledge:
                    return CopyPersonalToColony(component, schema, subjectId, request.sourcePawn, knowledgeEfficiency,
                        confidenceEfficiency, request.document, request.source);
                case KnowledgeTransmissionKind.Custom:
                    if (request.sourcePawn != null && request.recipientPawn != null)
                        return CopyPersonalToPersonal(component, schema, subjectId, request.sourcePawn, request.recipientPawn,
                            knowledgeEfficiency, confidenceEfficiency, request.source);
                    if (request.sourcePawn != null)
                        return CopyPersonalToColony(component, schema, subjectId, request.sourcePawn, knowledgeEfficiency,
                            confidenceEfficiency, request.document, request.source);
                    return CopyColonyToPersonal(component, schema, subjectId, request.recipientPawn,
                        knowledgeEfficiency, confidenceEfficiency, request.source);
                default: return false;
            }
        }

        private static bool CopyPersonalToColony(GameComponent_KnowledgeFramework component, KnowledgeSchema schema,
            string subjectId, Pawn pawn, float knowledgeEfficiency, float confidenceEfficiency, bool document, string source)
        {
            if (pawn == null) return false;
            bool changed = false;
            foreach (KnowledgeFacetSchema facetSchema in schema.facets.Where(item => item.shareable && (!document || item.documentable)))
            {
                PersonalFacetStateRecord from = component.PersonalFacetV2(schema.id, subjectId, facetSchema.id, pawn, false);
                if (from == null) continue;
                ColonyFacetStateRecord to = component.ColonyFacetV2(schema.id, subjectId, facetSchema.id, true);
                ColonySubjectStateRecord subject = component.ColonySubjectV2(schema.id, subjectId, true);
                bool facetChanged = CopyMinimum(from, to, knowledgeEfficiency, confidenceEfficiency);
                changed |= facetChanged;
                bool documentationChanged = document && (!subject.documented || subject.documentationSource != (source ?? pawn.ThingID));
                changed |= documentationChanged;
                if (document)
                {
                    subject.documented = true;
                    subject.documentationSource = source ?? pawn.ThingID;
                    PersonalSubjectStateRecord personal = component.PersonalSubjectV2(schema.id, subjectId, pawn, true);
                    personal.documented = true;
                    personal.documentationSource = subject.documentationSource;
                }
                KnowledgeEngine.UpdateStage(component, schema, schema.id, subjectId, null, true, subject);
                if (facetChanged || documentationChanged) component.Touch(subject, to);
            }
            if (changed)
            {
                component.RefreshDiagnosticsV2();
                KnowledgeProviderRegistry.InvalidateAll();
                KnowledgeEngine.NotifyExternalChange(source ?? "transmission");
            }
            return changed;
        }

        private static bool CopyPersonalToPersonal(GameComponent_KnowledgeFramework component, KnowledgeSchema schema,
            string subjectId, Pawn sourcePawn, Pawn recipientPawn, float knowledgeEfficiency, float confidenceEfficiency, string source)
        {
            if (sourcePawn == null || recipientPawn == null || sourcePawn == recipientPawn) return false;
            bool changed = false;
            foreach (KnowledgeFacetSchema facetSchema in schema.facets.Where(item => item.personallyKnowable && item.shareable))
            {
                PersonalFacetStateRecord from = component.PersonalFacetV2(schema.id, subjectId, facetSchema.id, sourcePawn, false);
                if (from == null) continue;
                PersonalFacetStateRecord to = component.PersonalFacetV2(schema.id, subjectId, facetSchema.id, recipientPawn, true);
                PersonalSubjectStateRecord subject = component.PersonalSubjectV2(schema.id, subjectId, recipientPawn, true);
                if (CopyMinimum(from, to, knowledgeEfficiency, confidenceEfficiency))
                {
                    KnowledgeEngine.UpdateStage(component, schema, schema.id, subjectId, recipientPawn, false, subject);
                    component.Touch(subject, to);
                    changed = true;
                }
            }
            if (changed)
            {
                component.RefreshDiagnosticsV2();
                KnowledgeProviderRegistry.Invalidate(recipientPawn);
                KnowledgeEngine.NotifyExternalChange(source ?? "transmission");
            }
            return changed;
        }

        private static bool CopyColonyToPersonal(GameComponent_KnowledgeFramework component, KnowledgeSchema schema,
            string subjectId, Pawn pawn, float knowledgeEfficiency, float confidenceEfficiency, string source)
        {
            if (pawn == null) return false;
            ColonySubjectStateRecord archive = component.ColonySubjectV2(schema.id, subjectId, false);
            if (schema.sharingModel == KnowledgeSharingModel.Documented && archive?.documented != true) return false;
            bool changed = false;
            foreach (KnowledgeFacetSchema facetSchema in schema.facets.Where(item => item.personallyKnowable))
            {
                ColonyFacetStateRecord from = component.ColonyFacetV2(schema.id, subjectId, facetSchema.id, false);
                if (from == null) continue;
                PersonalFacetStateRecord to = component.PersonalFacetV2(schema.id, subjectId, facetSchema.id, pawn, true);
                PersonalSubjectStateRecord subject = component.PersonalSubjectV2(schema.id, subjectId, pawn, true);
                if (CopyMinimum(from, to, knowledgeEfficiency, confidenceEfficiency))
                {
                    KnowledgeEngine.UpdateStage(component, schema, schema.id, subjectId, pawn, false, subject);
                    component.Touch(subject, to);
                    changed = true;
                }
            }
            if (changed)
            {
                component.RefreshDiagnosticsV2();
                KnowledgeProviderRegistry.Invalidate(pawn);
                KnowledgeEngine.NotifyExternalChange(source ?? "transmission");
            }
            return changed;
        }

        private static bool CopyMinimum(KnowledgeFacetStateRecord from, KnowledgeFacetStateRecord to,
            float knowledgeEfficiency, float confidenceEfficiency)
        {
            float amount = Math.Max(to.amount, from.amount * knowledgeEfficiency);
            float support = Math.Max(to.supportingEvidence, from.supportingEvidence * confidenceEfficiency);
            float contradiction = Math.Max(to.contradictoryEvidence, from.contradictoryEvidence * confidenceEfficiency);
            bool changed = amount > to.amount || support > to.supportingEvidence || contradiction > to.contradictoryEvidence;
            to.amount = amount;
            to.supportingEvidence = support;
            to.contradictoryEvidence = contradiction;
            to.evidenceCount = Math.Max(to.evidenceCount, (int)(from.evidenceCount * confidenceEfficiency));
            to.successCount = Math.Max(to.successCount, (int)(from.successCount * confidenceEfficiency));
            to.failureCount = Math.Max(to.failureCount, (int)(from.failureCount * confidenceEfficiency));
            foreach (KeyValuePair<string, int> pair in from.eventCounts)
                to.eventCounts[pair.Key] = Math.Max(to.eventCounts.TryGetValue(pair.Key, out int old) ? old : 0,
                    (int)(pair.Value * confidenceEfficiency));
            return changed;
        }
    }
}
