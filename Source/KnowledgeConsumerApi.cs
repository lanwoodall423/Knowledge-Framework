using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Verse;

namespace KnowledgeFramework
{
    public enum KnowledgeFrameworkReadinessState
    {
        NotInitialized,
        Ready,
        Unavailable,
        InitializationFailed
    }

    public enum KnowledgeFrameworkReadinessReason
    {
        None,
        InitializationPending,
        NoCurrentGame,
        NoFrameworkState,
        SchemaBuildPending,
        SchemaBuildFailed,
        GameTransition
    }

    /// <summary>Immutable readiness information for consumer registration and queries.</summary>
    public sealed class KnowledgeFrameworkReadinessStatus
    {
        public readonly KnowledgeFrameworkReadinessState state;
        public readonly KnowledgeFrameworkReadinessReason reason;
        public readonly int apiVersion;
        public readonly int registryRevision;
        public readonly int globalRevision;

        public bool IsReady => state == KnowledgeFrameworkReadinessState.Ready;

        internal KnowledgeFrameworkReadinessStatus(KnowledgeFrameworkReadinessState state,
            KnowledgeFrameworkReadinessReason reason, int registryRevision, int globalRevision)
        {
            this.state = state;
            this.reason = reason;
            apiVersion = KnowledgeFrameworkApi.ApiVersion;
            this.registryRevision = registryRevision;
            this.globalRevision = globalRevision;
        }
    }

    public enum KnowledgeDomainRegistrationState
    {
        InvalidRequest,
        FrameworkUnavailable,
        Unregistered,
        RegisteredBySameOwner,
        RegisteredByOtherOwner,
        Incompatible
    }

    public enum KnowledgeDomainOwnerRelation
    {
        None,
        SameOwner,
        OtherOwner
    }

    /// <summary>Read-only ownership and compatibility metadata for a requested domain registration.</summary>
    public sealed class KnowledgeDomainRegistrationInspection
    {
        public readonly KnowledgeDomainRegistrationState state;
        public readonly KnowledgeDomainOwnerRelation ownerRelation;
        public readonly KnowledgeFrameworkReadinessStatus readiness;
        public readonly string domainId;
        public readonly string requestedOwner;
        public readonly string registeredOwner;
        public readonly int requestedPriority;
        public readonly int registeredPriority;
        public readonly bool compatible;

        internal KnowledgeDomainRegistrationInspection(KnowledgeDomainRegistrationState state,
            KnowledgeDomainOwnerRelation ownerRelation, KnowledgeFrameworkReadinessStatus readiness,
            string domainId, string requestedOwner, string registeredOwner, int requestedPriority,
            int registeredPriority, bool compatible)
        {
            this.state = state;
            this.ownerRelation = ownerRelation;
            this.readiness = readiness;
            this.domainId = domainId;
            this.requestedOwner = requestedOwner;
            this.registeredOwner = registeredOwner;
            this.requestedPriority = requestedPriority;
            this.registeredPriority = registeredPriority;
            this.compatible = compatible;
        }
    }

    public enum KnowledgeConsumerRegistrationResultCode
    {
        Registered,
        AlreadyRegistered,
        FrameworkUnavailable,
        RejectedForeignOwner,
        RejectedIncompatible,
        InvalidRequest,
        Failed
    }

    /// <summary>Immutable result from safe consumer domain registration.</summary>
    public sealed class KnowledgeConsumerRegistrationResult
    {
        public readonly KnowledgeConsumerRegistrationResultCode code;
        public readonly KnowledgeDomainRegistrationInspection inspection;
        public readonly KnowledgeFrameworkReadinessStatus readiness;
        public readonly int registryRevision;

        public bool Success => code == KnowledgeConsumerRegistrationResultCode.Registered ||
            code == KnowledgeConsumerRegistrationResultCode.AlreadyRegistered;

        internal KnowledgeConsumerRegistrationResult(KnowledgeConsumerRegistrationResultCode code,
            KnowledgeDomainRegistrationInspection inspection, KnowledgeFrameworkReadinessStatus readiness)
        {
            this.code = code;
            this.inspection = inspection;
            this.readiness = readiness;
            registryRevision = KnowledgeRegistry.Revision;
        }
    }

    public enum KnowledgeInvalidationResultCode
    {
        InvalidRequest,
        FrameworkUnavailable,
        SubjectNotFound,
        BoundExceeded,
        Invalidated
    }

    /// <summary>Immutable result from targeted consumer cache invalidation.</summary>
    public sealed class KnowledgeInvalidationResult
    {
        public readonly KnowledgeInvalidationResultCode code;
        public readonly string domainId;
        public readonly int requestedCount;
        public readonly int invalidatedCount;
        public readonly int registryRevision;
        public readonly int globalRevision;

        public bool Success => code == KnowledgeInvalidationResultCode.Invalidated;

        internal KnowledgeInvalidationResult(KnowledgeInvalidationResultCode code, string domainId,
            int requestedCount, int invalidatedCount)
        {
            this.code = code;
            this.domainId = domainId;
            this.requestedCount = requestedCount;
            this.invalidatedCount = invalidatedCount;
            registryRevision = KnowledgeRegistry.Revision;
            globalRevision = GameComponent_KnowledgeFramework.Current?.GlobalRevision ?? 0;
        }
    }

    /// <summary>
    /// Stable consumer boundary. It owns no schemas and never replaces another consumer's registration.
    /// </summary>
    public static class KnowledgeConsumerApi
    {
        public const int MaxTargetedInvalidationSubjects = 256;

        public static KnowledgeFrameworkReadinessStatus Readiness => KnowledgeFrameworkLifecycle.Status;

        /// <summary>
        /// Idempotently prepares the framework-owned schema lifecycle for consumer registration.
        /// Registration is supported only while this returns Ready.
        /// </summary>
        public static KnowledgeFrameworkReadinessStatus PrepareRegistration()
        {
            KnowledgeFrameworkReadinessStatus status = Readiness;
            if (status.IsReady || status.state == KnowledgeFrameworkReadinessState.Unavailable ||
                status.state == KnowledgeFrameworkReadinessState.InitializationFailed) return status;
            KnowledgeRegistry.BuildDefSchemas();
            return Readiness;
        }

        public static KnowledgeDomainRegistrationInspection InspectDomainRegistration(
            KnowledgeDomainRegistration registration, KnowledgeRegistrationOptions options = null)
        {
            options = options ?? new KnowledgeRegistrationOptions();
            KnowledgeFrameworkReadinessStatus readiness = Readiness;
            if (registration == null || !ValidId(registration.id))
                return new KnowledgeDomainRegistrationInspection(KnowledgeDomainRegistrationState.InvalidRequest,
                    KnowledgeDomainOwnerRelation.None, readiness, registration?.id, RequestedOwner(registration, options),
                    null, options.priority, 0, false);

            string domainId = KnowledgeRegistry.ResolveDomainId(registration.id) ?? registration.id.Trim();
            string requestedOwner = RequestedOwner(registration, options);
            KnowledgeSchema requestedSchema;
            try { requestedSchema = new KnowledgeSchema(registration, options.priority, requestedOwner); }
            catch (Exception)
            {
                return new KnowledgeDomainRegistrationInspection(KnowledgeDomainRegistrationState.InvalidRequest,
                    KnowledgeDomainOwnerRelation.None, readiness, domainId, requestedOwner, null,
                    options.priority, 0, false);
            }
            if (!KnowledgeRegistry.ValidateSchemaForConsumer(requestedSchema))
                return new KnowledgeDomainRegistrationInspection(KnowledgeDomainRegistrationState.InvalidRequest,
                    KnowledgeDomainOwnerRelation.None, readiness, domainId, requestedOwner, null,
                    options.priority, 0, false);

            KnowledgeSchema existing = KnowledgeRegistry.Schema(domainId);
            if (existing == null)
            {
                KnowledgeDomainRegistrationState state = readiness.IsReady
                    ? KnowledgeDomainRegistrationState.Unregistered
                    : KnowledgeDomainRegistrationState.FrameworkUnavailable;
                return new KnowledgeDomainRegistrationInspection(state, KnowledgeDomainOwnerRelation.None,
                    readiness, domainId, requestedOwner, null, options.priority, 0, state == KnowledgeDomainRegistrationState.Unregistered);
            }

            KnowledgeDomainOwnerRelation ownerRelation = string.Equals(existing.source, requestedOwner, StringComparison.Ordinal)
                ? KnowledgeDomainOwnerRelation.SameOwner : KnowledgeDomainOwnerRelation.OtherOwner;
            bool compatible = SchemaCompatibilityKey(existing) == SchemaCompatibilityKey(requestedSchema);
            KnowledgeDomainRegistrationState stateForExisting = compatible
                ? ownerRelation == KnowledgeDomainOwnerRelation.SameOwner
                    ? KnowledgeDomainRegistrationState.RegisteredBySameOwner
                    : KnowledgeDomainRegistrationState.RegisteredByOtherOwner
                : KnowledgeDomainRegistrationState.Incompatible;
            return new KnowledgeDomainRegistrationInspection(stateForExisting, ownerRelation, readiness, domainId,
                requestedOwner, existing.source, options.priority, existing.priority, compatible);
        }

        /// <summary>Registers only an unregistered, compatible domain; foreign registrations are never replaced.</summary>
        public static KnowledgeConsumerRegistrationResult RegisterDomain(KnowledgeDomainRegistration registration,
            KnowledgeRegistrationOptions options = null)
        {
            options = options ?? new KnowledgeRegistrationOptions();
            KnowledgeFrameworkReadinessStatus readiness = PrepareRegistration();
            KnowledgeDomainRegistrationInspection inspection = InspectDomainRegistration(registration, options);
            if (!readiness.IsReady)
            {
                KnowledgeConsumerRegistrationResultCode unavailableCode = readiness.state == KnowledgeFrameworkReadinessState.InitializationFailed
                    ? KnowledgeConsumerRegistrationResultCode.Failed : KnowledgeConsumerRegistrationResultCode.FrameworkUnavailable;
                return new KnowledgeConsumerRegistrationResult(unavailableCode, inspection, readiness);
            }
            switch (inspection.state)
            {
                case KnowledgeDomainRegistrationState.InvalidRequest:
                    return new KnowledgeConsumerRegistrationResult(KnowledgeConsumerRegistrationResultCode.InvalidRequest, inspection, readiness);
                case KnowledgeDomainRegistrationState.RegisteredBySameOwner:
                    return new KnowledgeConsumerRegistrationResult(KnowledgeConsumerRegistrationResultCode.AlreadyRegistered, inspection, readiness);
                case KnowledgeDomainRegistrationState.RegisteredByOtherOwner:
                    return new KnowledgeConsumerRegistrationResult(KnowledgeConsumerRegistrationResultCode.RejectedForeignOwner, inspection, readiness);
                case KnowledgeDomainRegistrationState.Incompatible:
                    return new KnowledgeConsumerRegistrationResult(KnowledgeConsumerRegistrationResultCode.RejectedIncompatible, inspection, readiness);
            }

            KnowledgeRegistrationOptions safeOptions = new KnowledgeRegistrationOptions
            {
                priority = options.priority,
                source = RequestedOwner(registration, options),
                conflict = KnowledgeRegistrationConflict.Reject
            };
            bool registered = KnowledgeRegistry.RegisterDomain(registration, safeOptions);
            return new KnowledgeConsumerRegistrationResult(registered ? KnowledgeConsumerRegistrationResultCode.Registered :
                KnowledgeConsumerRegistrationResultCode.Failed, inspection, Readiness);
        }

        public static KnowledgeInvalidationResult InvalidateSubject(string domainId, string subjectId)
        {
            return InvalidateSubjects(domainId, new[] { subjectId });
        }

        /// <summary>Invalidates at most MaxTargetedInvalidationSubjects canonical subjects.</summary>
        public static KnowledgeInvalidationResult InvalidateSubjects(string domainId, IEnumerable<string> subjectIds)
        {
            KnowledgeFrameworkReadinessStatus readiness = Readiness;
            if (!readiness.IsReady) return new KnowledgeInvalidationResult(KnowledgeInvalidationResultCode.FrameworkUnavailable,
                domainId, 0, 0);
            string canonicalDomain = KnowledgeRegistry.ResolveDomainId(domainId);
            if (canonicalDomain.NullOrEmpty() || subjectIds == null)
                return new KnowledgeInvalidationResult(KnowledgeInvalidationResultCode.InvalidRequest, canonicalDomain, 0, 0);
            List<string> requested = subjectIds.Where(value => !value.NullOrEmpty()).Select(value => value.Trim()).Distinct(StringComparer.Ordinal)
                .Take(MaxTargetedInvalidationSubjects + 1).ToList();
            if (requested.Count > MaxTargetedInvalidationSubjects)
                return new KnowledgeInvalidationResult(KnowledgeInvalidationResultCode.BoundExceeded, canonicalDomain, requested.Count, 0);
            List<string> canonicalSubjects = requested.Select(value => KnowledgeRegistry.ResolveSubjectId(canonicalDomain, value))
                .Where(value => !value.NullOrEmpty() && KnowledgeRegistry.ResolveSubject(canonicalDomain, value) != null)
                .Distinct(StringComparer.Ordinal).ToList();
            if (canonicalSubjects.Count == 0)
                return new KnowledgeInvalidationResult(KnowledgeInvalidationResultCode.SubjectNotFound, canonicalDomain, requested.Count, 0);

            KnowledgeRegistry.InvalidateSubjectCaches(canonicalDomain, canonicalSubjects);
            InvalidateDependentCaches();
            return new KnowledgeInvalidationResult(KnowledgeInvalidationResultCode.Invalidated, canonicalDomain,
                requested.Count, canonicalSubjects.Count);
        }

        public static KnowledgeInvalidationResult InvalidateDomain(string domainId)
        {
            KnowledgeFrameworkReadinessStatus readiness = Readiness;
            if (!readiness.IsReady) return new KnowledgeInvalidationResult(KnowledgeInvalidationResultCode.FrameworkUnavailable,
                domainId, 0, 0);
            string canonicalDomain = KnowledgeRegistry.ResolveDomainId(domainId);
            if (canonicalDomain.NullOrEmpty() || KnowledgeRegistry.Schema(canonicalDomain) == null)
                return new KnowledgeInvalidationResult(KnowledgeInvalidationResultCode.InvalidRequest, canonicalDomain, 0, 0);
            KnowledgeDomainRegistry.InvalidateDomain(canonicalDomain);
            InvalidateDependentCaches();
            return new KnowledgeInvalidationResult(KnowledgeInvalidationResultCode.Invalidated, canonicalDomain, 0, 0);
        }

        private static void InvalidateDependentCaches()
        {
            GameComponent_KnowledgeFramework.Current?.TouchV3();
            KnowledgeProviderRegistry.InvalidateAll();
            KnowledgeBioPanel.ResetGameState();
            KnowledgeMenuUI.ResetGameState();
            KnowledgeContextRegistry.InvalidateConsumerCaches();
        }

        private static string RequestedOwner(KnowledgeDomainRegistration registration, KnowledgeRegistrationOptions options)
        {
            return options.source ?? registration?.source ?? "dynamic";
        }

        private static bool ValidId(string value) => !value.NullOrEmpty() && value.Trim() == value && !value.Contains("\n");

        private static string SchemaCompatibilityKey(KnowledgeSchema schema) => schema?.compatibilityKey ?? string.Empty;
    }

    internal static class KnowledgeSchemaCompatibility
    {
        public static string Key(KnowledgeDomainRegistration registration)
        {
            StringBuilder builder = new StringBuilder();
            AppendValue(builder, registration, new HashSet<object>(ReferenceComparer.Instance));
            return builder.ToString();
        }

        private static void AppendValue(StringBuilder builder, object value, HashSet<object> active)
        {
            if (value == null)
            {
                builder.Append("<null>");
                return;
            }
            if (value is string text)
            {
                builder.Append("s:").Append(text.Length).Append(':').Append(text);
                return;
            }
            Type type = value.GetType();
            if (type.IsEnum)
            {
                builder.Append("e:").Append(type.FullName).Append(':').Append(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                return;
            }
            if (type.IsPrimitive || value is decimal)
            {
                builder.Append("p:").Append(type.FullName).Append(':')
                    .Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }
            if (value is Delegate callback)
            {
                MethodInfo method = callback.Method;
                builder.Append("d:").Append(type.FullName).Append(':')
                    .Append(method.DeclaringType?.FullName).Append(':').Append(method.Name);
                return;
            }
            if (value is IDictionary dictionary)
            {
                List<KeyValuePair<string, object>> entries = new List<KeyValuePair<string, object>>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    StringBuilder key = new StringBuilder();
                    AppendValue(key, entry.Key, new HashSet<object>(ReferenceComparer.Instance));
                    entries.Add(new KeyValuePair<string, object>(key.ToString(), entry.Value));
                }
                foreach (KeyValuePair<string, object> entry in entries.OrderBy(item => item.Key, StringComparer.Ordinal))
                {
                    builder.Append("k:").Append(entry.Key).Append('=');
                    AppendValue(builder, entry.Value, active);
                }
                return;
            }
            if (value is IEnumerable sequence)
            {
                builder.Append("[");
                foreach (object item in sequence) AppendValue(builder, item, active);
                builder.Append("]");
                return;
            }
            if (!active.Add(value))
            {
                builder.Append("<cycle:").Append(type.FullName).Append('>');
                return;
            }
            builder.Append("o:").Append(type.FullName).Append('{');
            foreach (FieldInfo field in Fields(type))
            {
                if (!Include(field)) continue;
                builder.Append(field.Name).Append('=');
                AppendValue(builder, field.GetValue(value), active);
                builder.Append(';');
            }
            builder.Append('}');
            active.Remove(value);
        }

        private static IEnumerable<FieldInfo> Fields(Type type)
        {
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
                foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .OrderBy(item => item.Name, StringComparer.Ordinal)) yield return field;
        }

        private static bool Include(FieldInfo field)
        {
            if (field.IsStatic) return false;
            if (field.Name == "modContentPack" || field.Name == "shortHash" || field.Name == "index" ||
                field.Name == "postLoadActions" || field.Name == "specialDisplayStats") return false;
            Type type = field.FieldType;
            return type == typeof(string) || type.IsPrimitive || type.IsEnum || type == typeof(decimal) ||
                typeof(Delegate).IsAssignableFrom(type) || typeof(IEnumerable).IsAssignableFrom(type) ||
                type.Namespace != null && type.Namespace.StartsWith("KnowledgeFramework", StringComparison.Ordinal);
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object left, object right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }

    internal static class KnowledgeFrameworkLifecycle
    {
        private static Verse.Game observedGame;
        private static Verse.Game initializedGame;
        private static bool schemaBuildFailed;

        internal static KnowledgeFrameworkReadinessStatus Status
        {
            get
            {
                Verse.Game currentGame = Verse.Current.Game;
                if (!ReferenceEquals(currentGame, observedGame))
                {
                    observedGame = currentGame;
                    initializedGame = null;
                    schemaBuildFailed = false;
                }
                if (currentGame == null)
                    return Create(KnowledgeFrameworkReadinessState.Unavailable, KnowledgeFrameworkReadinessReason.NoCurrentGame);
                if (schemaBuildFailed)
                    return Create(KnowledgeFrameworkReadinessState.InitializationFailed, KnowledgeFrameworkReadinessReason.SchemaBuildFailed);
                if (GameComponent_KnowledgeFramework.Current == null)
                    return Create(KnowledgeFrameworkReadinessState.Unavailable, KnowledgeFrameworkReadinessReason.NoFrameworkState);
                if (!KnowledgeRegistry.DefSchemasReady)
                    return Create(KnowledgeFrameworkReadinessState.NotInitialized, KnowledgeFrameworkReadinessReason.SchemaBuildPending);
                if (!ReferenceEquals(initializedGame, currentGame))
                    return Create(KnowledgeFrameworkReadinessState.NotInitialized, KnowledgeFrameworkReadinessReason.InitializationPending);
                return Create(KnowledgeFrameworkReadinessState.Ready, KnowledgeFrameworkReadinessReason.None);
            }
        }

        internal static void GameInitialized()
        {
            initializedGame = Verse.Current.Game;
        }

        internal static void SchemaBuildFailed()
        {
            schemaBuildFailed = true;
        }

        internal static void ClearSchemaBuildFailureForVerification()
        {
            schemaBuildFailed = false;
        }

        private static KnowledgeFrameworkReadinessStatus Create(KnowledgeFrameworkReadinessState state,
            KnowledgeFrameworkReadinessReason reason)
        {
            return new KnowledgeFrameworkReadinessStatus(state, reason, KnowledgeRegistry.Revision,
                GameComponent_KnowledgeFramework.Current?.GlobalRevision ?? 0);
        }
    }
}
