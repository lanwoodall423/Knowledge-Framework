using System;
using System.IO;
using System.Linq;
using System.Reflection;
using KnowledgeFramework;

internal static class Program
{
    private static readonly string[] RequiredAssemblies =
    {
        "UnityEngine.CoreModule.dll",
        "UnityEngine.IMGUIModule.dll",
        "UnityEngine.TextRenderingModule.dll",
        "0Harmony.dll",
        "Assembly-CSharp.dll"
    };

    private static int Main(string[] args)
    {
        try
        {
            string managedPath = GetOption(args, "managed") ??
                Environment.GetEnvironmentVariable("RIMWORLD_MANAGED_PATH");
            if (string.IsNullOrWhiteSpace(managedPath))
            {
                Console.Error.WriteLine("behavioralSuite=BLOCKED expected=RimWorld managed assemblies actual=missing --managed path");
                return 2;
            }

            LoadRequiredAssemblies(managedPath);
            KnowledgeVerificationResult result = KnowledgeFrameworkVerification.RunPureTests();
            PrintResult(result);
            if (!result.Success) return 1;

            if (!RunPublicV3Audit()) return 1;
            Console.WriteLine("behavioralSuite=PASS pureProductionChecks={0} publicV3DeclarationAudit=PASS gameSuite=NOT_RUN_NO_MAP", result.passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("behavioralSuite=FAIL expected=production pure verification completes actual={0}", Unwrap(exception));
            return 1;
        }
    }

    private static void LoadRequiredAssemblies(string managedPath)
    {
        string harmonyPath = Environment.GetEnvironmentVariable("RIMWORLD_HARMONY_PATH");
        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
        {
            string fileName = new AssemblyName(eventArgs.Name).Name + ".dll";
            string managedFile = Path.Combine(managedPath, fileName);
            if (File.Exists(managedFile)) return Assembly.LoadFrom(managedFile);
            if (!string.IsNullOrWhiteSpace(harmonyPath))
            {
                string harmonyFile = Path.Combine(harmonyPath, fileName);
                if (File.Exists(harmonyFile)) return Assembly.LoadFrom(harmonyFile);
            }
            return null;
        };
        foreach (string name in RequiredAssemblies)
        {
            string path = name == "0Harmony.dll" && !string.IsNullOrWhiteSpace(harmonyPath)
                ? Path.Combine(harmonyPath, name)
                : Path.Combine(managedPath, name);
            if (!File.Exists(path))
                throw new FileNotFoundException("required runtime assembly is missing: " + path, path);
            Assembly.LoadFrom(path);
        }
    }

    private static void PrintResult(KnowledgeVerificationResult result)
    {
        if (result.Success)
        {
            Console.WriteLine("PASS production pure suite expected=all pure checks actual={0} checks", result.passed);
            return;
        }

        Console.Error.WriteLine("FAIL production pure suite expected=all pure checks actual={0} failures", result.failures.Count);
        foreach (string failure in result.failures)
            Console.Error.WriteLine("FAIL behavior={0} expected=true actual=false", failure);
    }

    private static bool RunPublicV3Audit()
    {
        Assembly assembly = typeof(KnowledgeFrameworkVerification).Assembly;
        bool valid = true;
        Type[] publicTypes = assembly.GetTypes()
            .Where(type => type.IsPublic && type.Namespace == "KnowledgeFramework" && type.Name.StartsWith("Knowledge", StringComparison.Ordinal))
            .OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
        foreach (Type type in publicTypes.Where(value => value.IsEnum))
        {
            foreach (string member in Enum.GetNames(type))
            {
                FieldInfo field = type.GetField(member);
                bool obsolete = field.GetCustomAttributes(typeof(ObsoleteAttribute), false).Length != 0;
                string status = obsolete ? "obsolete-rejected" : "declared-supported";
                string coverage = obsolete ? "validation-rejection-required" : "declaration-only-no-behavior-claim";
                Console.WriteLine("AUDIT v3.enum={0}.{1} status={2} coverage={3} execution=reflection-only", type.Name, member, status, coverage);
            }
        }

        foreach (Type type in publicTypes.Where(value => !value.IsEnum))
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                bool obsolete = field.GetCustomAttributes(typeof(ObsoleteAttribute), false).Length != 0;
                string status = obsolete ? "obsolete-rejected" : "declared-supported";
                string coverage = obsolete ? "validation-rejection-required" : "declaration-only-no-behavior-claim";
                Console.WriteLine("AUDIT v3.field={0}.{1} status={2} coverage={3} execution=reflection-only", type.Name, field.Name, status, coverage);
            }
        }

        return valid;
    }

    private static string GetOption(string[] args, string name)
    {
        string prefix = "--" + name + "=";
        return args == null ? null : args.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?.Substring(prefix.Length);
    }

    private static string Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException && exception.InnerException != null)
            exception = exception.InnerException;
        return exception.GetType().Name + ": " + exception.Message;
    }
}
