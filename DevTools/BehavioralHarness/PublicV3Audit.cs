using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using KnowledgeFramework;

internal static class PublicV3Audit
{
    private const string BaselineResourceSuffix = "PublicApiBaseline.txt";

    internal static bool Run(Assembly assembly, KnowledgeVerificationResult pureResult)
    {
        bool valid = true;
        string[] actual = BuildSignatures(assembly).ToArray();
        string[] baseline = ReadBaseline();
        if (baseline.Length == 0)
        {
            Console.Error.WriteLine("API FAIL baseline expected=embedded human-readable public API baseline actual=missing");
            return false;
        }

        string expectedRelease = baseline.FirstOrDefault(line => line.StartsWith("# release=", StringComparison.Ordinal))?.Substring(10);
        string actualRelease = AssemblyReleaseVersion(assembly);
        if (!string.Equals(expectedRelease, actualRelease, StringComparison.Ordinal))
        {
            valid = false;
            Console.Error.WriteLine("API FAIL release expected={0} actual={1}", expectedRelease ?? "missing", actualRelease ?? "missing");
        }

        string[] expectedDeclarations = baseline.Where(IsDeclaration).ToArray();
        valid &= CompareDeclarationSets(expectedDeclarations, actual, true);
        Console.WriteLine("API summary types={0} declarations={1} release={2} baseline={3}",
            actual.Count(line => line.StartsWith("TYPE|", StringComparison.Ordinal)), actual.Length,
            actualRelease ?? "missing", valid ? "PASS" : "FAIL");
        return valid;
    }

    internal static bool RunRegressionTests()
    {
        string[] baseline =
        {
            "TYPE|KnowledgeFramework.IProvider|kind=interface|visibility=public|base=|interfaces=|generic=",
            "METHOD|KnowledgeFramework.IProvider|Read|visibility=public|modifiers=abstract|generic=|return=System.String|params=",
            "FIELD|KnowledgeFramework.Options|Count|visibility=public|modifiers=|type=System.Int32",
            "METHOD|KnowledgeFramework.Options|Run|visibility=public|modifiers=static|generic=|return=System.Boolean|params=",
            "ENUM|KnowledgeFramework.Mode|Fast|value=1",
            "TYPE|KnowledgeFramework.Options|kind=class|visibility=public|base=System.Object|interfaces=|generic="
        };
        bool valid = true;
        valid &= MutationDetected("field-type", baseline, "FIELD|KnowledgeFramework.Options|Count|visibility=public|modifiers=|type=System.Int64");
        valid &= MutationDetected("method-signature", baseline, "METHOD|KnowledgeFramework.Options|Run|visibility=public|modifiers=static|generic=|return=System.Int32|params=");
        valid &= MutationDetected("enum-value", baseline, "ENUM|KnowledgeFramework.Mode|Fast|value=2");
        valid &= MutationDetected("visibility", baseline, "TYPE|KnowledgeFramework.Options|kind=class|visibility=internal|base=System.Object|interfaces=|generic=");
        valid &= MutationDetected("interface-signature", baseline, "METHOD|KnowledgeFramework.IProvider|Read|visibility=public|modifiers=abstract|generic=|return=System.Int32|params=");
        Console.WriteLine("API regression tests={0}", valid ? "PASS" : "FAIL");
        return valid;
    }

    internal static void WriteBaseline(Assembly assembly, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Baseline path is required.", nameof(path));
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        List<string> lines = new List<string>
        {
            "# Knowledge Framework public API baseline",
            "# format=1",
            "# release=" + (AssemblyReleaseVersion(assembly) ?? string.Empty),
            "# Additive or incompatible changes require an intentional baseline update.",
            string.Empty
        };
        lines.AddRange(BuildSignatures(assembly).OrderBy(line => line, StringComparer.Ordinal));
        File.WriteAllText(fullPath, string.Join(Environment.NewLine, lines) + Environment.NewLine, new UTF8Encoding(false));
    }

    private static bool MutationDetected(string name, IEnumerable<string> baseline, string replacement)
    {
        string[] expected = baseline.ToArray();
        string old = expected.First(line => line.StartsWith(replacement.Substring(0, replacement.IndexOf('|', replacement.IndexOf('|') + 1) + 1), StringComparison.Ordinal));
        string[] mutated = expected.Select(line => line == old ? replacement : line).ToArray();
        bool detected = !CompareDeclarationSets(expected, mutated, false);
        Console.WriteLine("API regression {0}={1}", name, detected ? "PASS" : "FAIL");
        return detected;
    }

    private static bool CompareDeclarationSets(IEnumerable<string> expected, IEnumerable<string> actual, bool diagnostics)
    {
        HashSet<string> expectedSet = new HashSet<string>(expected.Where(IsDeclaration), StringComparer.Ordinal);
        HashSet<string> actualSet = new HashSet<string>(actual.Where(IsDeclaration), StringComparer.Ordinal);
        string[] removed = expectedSet.Except(actualSet, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] added = actualSet.Except(expectedSet, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (diagnostics)
        {
            foreach (string declaration in removed) Console.Error.WriteLine("API FAIL removed={0}", declaration);
            foreach (string declaration in added) Console.Error.WriteLine("API FAIL added={0}", declaration);
        }
        return removed.Length == 0 && added.Length == 0;
    }

    private static string[] ReadBaseline()
    {
        string resourceName = typeof(PublicV3Audit).Assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(BaselineResourceSuffix, StringComparison.Ordinal));
        if (resourceName == null) return new string[0];
        using (Stream stream = typeof(PublicV3Audit).Assembly.GetManifestResourceStream(resourceName))
        using (StreamReader reader = new StreamReader(stream))
            return reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
    }

    private static bool IsDeclaration(string line) => line.StartsWith("TYPE|", StringComparison.Ordinal) ||
        line.StartsWith("CTOR|", StringComparison.Ordinal) || line.StartsWith("METHOD|", StringComparison.Ordinal) ||
        line.StartsWith("FIELD|", StringComparison.Ordinal) || line.StartsWith("PROPERTY|", StringComparison.Ordinal) ||
        line.StartsWith("EVENT|", StringComparison.Ordinal) || line.StartsWith("ENUM|", StringComparison.Ordinal) ||
        line.StartsWith("DELEGATE|", StringComparison.Ordinal);

    private static List<string> BuildSignatures(Assembly assembly)
    {
        List<string> result = new List<string>();
        Type[] types = assembly.GetTypes().Where(IsPublicApiType)
            .OrderBy(TypeName, StringComparer.Ordinal).ToArray();
        foreach (Type type in types)
        {
            result.Add(TypeSignature(type));
            if (type.IsEnum)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .OrderBy(field => field.Name, StringComparer.Ordinal))
                    result.Add("ENUM|" + TypeName(type) + "|" + field.Name + "|value=" + Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture) + ObsoleteSuffix(field));
                continue;
            }
            if (IsDelegate(type))
            {
                MethodInfo invoke = type.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);
                result.Add("DELEGATE|" + TypeName(type) + "|return=" + FormatType(invoke?.ReturnType) + "|params=" + FormatParameters(invoke?.GetParameters()) + ObsoleteSuffix(type));
                continue;
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(IsContractMember).OrderBy(Signature, StringComparer.Ordinal))
                result.Add("CTOR|" + TypeName(type) + "|visibility=" + Visibility(constructor) + "|modifiers=" + MethodModifiers(constructor) + "|params=" + FormatParameters(constructor.GetParameters()) + ObsoleteSuffix(constructor));
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(IsContractMember).OrderBy(field => field.Name, StringComparer.Ordinal))
                result.Add("FIELD|" + TypeName(type) + "|" + field.Name + "|visibility=" + Visibility(field) + "|modifiers=" + FieldModifiers(field) + "|type=" + FormatType(field.FieldType) + "|constant=" + FieldConstant(field) + ObsoleteSuffix(field));
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(IsContractProperty).OrderBy(property => property.Name, StringComparer.Ordinal))
                result.Add("PROPERTY|" + TypeName(type) + "|" + property.Name + "|type=" + FormatType(property.PropertyType) + "|index=" + FormatParameters(property.GetIndexParameters()) +
                    "|get=" + AccessorVisibility(property.GetMethod) + "|set=" + AccessorVisibility(property.SetMethod) + ObsoleteSuffix(property));
            foreach (EventInfo @event in type.GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(IsContractEvent).OrderBy(@event => @event.Name, StringComparer.Ordinal))
                result.Add("EVENT|" + TypeName(type) + "|" + @event.Name + "|type=" + FormatType(@event.EventHandlerType) +
                    "|add=" + AccessorVisibility(@event.AddMethod) + "|remove=" + AccessorVisibility(@event.RemoveMethod) + ObsoleteSuffix(@event));
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => IsContractMember(method) && !method.IsSpecialName).OrderBy(Signature, StringComparer.Ordinal))
                result.Add("METHOD|" + TypeName(type) + "|" + method.Name + "|visibility=" + Visibility(method) + "|modifiers=" + MethodModifiers(method) +
                    "|generic=" + GenericParameters(method.GetGenericArguments()) + "|return=" + FormatType(method.ReturnType) + "|params=" + FormatParameters(method.GetParameters()) + ObsoleteSuffix(method));
        }
        return result;
    }

    private static string TypeSignature(Type type) => "TYPE|" + TypeName(type) + "|kind=" + TypeKind(type) + "|visibility=" + Visibility(type) + "|modifiers=" + TypeModifiers(type) +
        "|base=" + FormatType(type.BaseType) + "|interfaces=" + string.Join(",", type.GetInterfaces().Select(FormatType).OrderBy(value => value, StringComparer.Ordinal)) +
        "|enumUnderlying=" + (type.IsEnum ? FormatType(Enum.GetUnderlyingType(type)) : string.Empty) +
        "|generic=" + GenericParameters(type.IsGenericTypeDefinition ? type.GetGenericArguments() : new Type[0]) + ObsoleteSuffix(type);

    private static bool IsPublicApiType(Type type)
    {
        return type.IsPublic || type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem;
    }

    private static bool IsDelegate(Type type) => typeof(MulticastDelegate).IsAssignableFrom(type.BaseType);

    private static string TypeKind(Type type) => type.IsEnum ? "enum" : IsDelegate(type) ? "delegate" : type.IsInterface ? "interface" :
        type.IsValueType ? "struct" : "class";

    private static string TypeName(Type type)
    {
        if (type == null) return string.Empty;
        if (type.IsByRef) return FormatType(type.GetElementType()) + "&";
        if (type.IsArray) return FormatType(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsGenericParameter) return "`" + type.Name;
        if (type.IsGenericType)
        {
            string name = type.GetGenericTypeDefinition().FullName ?? type.Name;
            int tick = name.IndexOf('`');
            if (tick >= 0) name = name.Substring(0, tick);
            return name + "<" + string.Join(",", type.GetGenericArguments().Select(FormatType)) + ">";
        }
        return type.FullName ?? type.Name;
    }

    private static string FormatType(Type type) => TypeName(type);

    private static string GenericParameters(Type[] parameters)
    {
        return string.Join(";", (parameters ?? new Type[0]).Select(parameter => parameter.Name + ":" + GenericConstraints(parameter)));
    }

    private static string GenericConstraints(Type parameter)
    {
        List<string> constraints = new List<string>();
        GenericParameterAttributes attributes = parameter.GenericParameterAttributes;
        if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0) constraints.Add("class");
        if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) constraints.Add("struct");
        if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0) constraints.Add("new()");
        constraints.AddRange(parameter.GetGenericParameterConstraints().Select(FormatType).OrderBy(value => value, StringComparer.Ordinal));
        return string.Join(",", constraints);
    }

    private static string FormatParameters(IEnumerable<ParameterInfo> parameters)
    {
        return string.Join(";", (parameters ?? Enumerable.Empty<ParameterInfo>()).Select(parameter =>
            (parameter.IsOut ? "out " : parameter.ParameterType.IsByRef ? (parameter.IsIn ? "in " : "ref ") : "") +
            FormatType(parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType() : parameter.ParameterType) + " " + parameter.Name +
            (parameter.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length != 0 ? " params" : "") +
            (parameter.IsOptional ? " optional=" + DefaultValue(parameter) : "")));
    }

    private static string DefaultValue(ParameterInfo parameter)
    {
        if (!parameter.HasDefaultValue) return "<missing>";
        object value = parameter.DefaultValue;
        if (value == null) return "null";
        if (value is string) return "\"" + Escape(value.ToString()) + "\"";
        if (value is Type) return "typeof(" + FormatType((Type)value) + ")";
        return Escape(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    private static string Visibility(Type type) => type.IsNested ?
        (type.IsNestedPublic ? "public" : type.IsNestedFamily ? "protected" : type.IsNestedFamORAssem ? "protected-internal" : type.IsNestedPrivate ? "private" : "internal") :
        (type.IsPublic ? "public" : "internal");

    private static string Visibility(MethodBase method) => method.IsPublic ? "public" : method.IsFamily ? "protected" : method.IsFamilyOrAssembly ? "protected-internal" : method.IsPrivate ? "private" : "internal";
    private static string Visibility(FieldInfo field) => field.IsPublic ? "public" : field.IsFamily ? "protected" : field.IsFamilyOrAssembly ? "protected-internal" : field.IsPrivate ? "private" : "internal";
    private static string AccessorVisibility(MethodInfo method) => method == null ? "none" : Visibility(method);

    private static string TypeModifiers(Type type)
    {
        List<string> modifiers = new List<string>();
        if (type.IsAbstract && type.IsSealed) modifiers.Add("static");
        else
        {
            if (type.IsAbstract) modifiers.Add("abstract");
            if (type.IsSealed) modifiers.Add("sealed");
        }
        return string.Join(",", modifiers);
    }

    private static string MethodModifiers(MethodBase method)
    {
        MethodInfo info = method as MethodInfo;
        List<string> modifiers = new List<string>();
        if (method.IsStatic) modifiers.Add("static");
        if (info != null && info.IsAbstract) modifiers.Add("abstract");
        if (info != null && info.GetBaseDefinition() != info) modifiers.Add("override");
        else if (info != null && info.IsVirtual) modifiers.Add("virtual");
        if (info != null && info.IsFinal && info.IsVirtual) modifiers.Add("sealed");
        return string.Join(",", modifiers);
    }

    private static string FieldModifiers(FieldInfo field)
    {
        List<string> modifiers = new List<string>();
        if (field.IsStatic) modifiers.Add("static");
        if (field.IsLiteral) modifiers.Add("const");
        else if (field.IsInitOnly) modifiers.Add("readonly");
        return string.Join(",", modifiers);
    }

    private static string FieldConstant(FieldInfo field)
    {
        if (!field.IsLiteral) return "<none>";
        object value = field.GetRawConstantValue();
        return value == null ? "null" : Escape(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    private static bool IsContractMember(MemberInfo member)
    {
        if (member is MethodBase) return IsContractVisibility(((MethodBase)member).Attributes);
        if (member is FieldInfo) return IsContractVisibility(((FieldInfo)member).Attributes);
        return false;
    }

    private static bool IsContractProperty(PropertyInfo property) => IsContractVisibility(property.GetMethod?.Attributes ?? property.SetMethod?.Attributes ?? 0);
    private static bool IsContractEvent(EventInfo @event) => IsContractVisibility(@event.AddMethod?.Attributes ?? @event.RemoveMethod?.Attributes ?? 0);
    private static bool IsContractVisibility(MethodAttributes attributes) => (attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public ||
        (attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Family || (attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.FamORAssem;
    private static bool IsContractVisibility(FieldAttributes attributes) => (attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public ||
        (attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Family || (attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.FamORAssem;

    private static string Signature(MemberInfo member) => member is MethodBase ? member.Name + "|" + FormatParameters(((MethodBase)member).GetParameters()) : member.Name;

    private static string ObsoleteSuffix(MemberInfo member)
    {
        ObsoleteAttribute obsolete = member.GetCustomAttributes(typeof(ObsoleteAttribute), false).OfType<ObsoleteAttribute>().FirstOrDefault();
        return obsolete == null ? string.Empty : "|obsolete=" + Escape(obsolete.Message ?? string.Empty) + ";error=" + obsolete.IsError;
    }

    private static string Escape(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("|", "\\|").Replace("\r", "\\r").Replace("\n", "\\n");

    private static string AssemblyReleaseVersion(Assembly assembly)
    {
        return assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
            .OfType<AssemblyInformationalVersionAttribute>().Select(attribute => attribute.InformationalVersion).FirstOrDefault();
    }
}
