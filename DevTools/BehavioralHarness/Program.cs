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

            bool auditPassed = PublicV3Audit.Run(typeof(KnowledgeFrameworkVerification).Assembly, result);
            Console.WriteLine("layer=pure passed={0} failed={1} skipped={2} unavailable={3}",
                result.purePassed, result.pureFailed, result.skipped, result.unavailable);
            Console.WriteLine("layer=game-state passed=0 failed=0 skipped=0 unavailable=1 reason=active GameComponent and map required");
            Console.WriteLine("layer=manual-ui passed=0 failed=0 skipped=0 unavailable=1 reason=human interaction required");
            Console.WriteLine("behavioralSuite={0} pureProductionChecks={1} publicV3DeclarationAudit={2} gameSuite=UNAVAILABLE_NO_GAME_STATE",
                auditPassed ? "PASS" : "FAIL", result.purePassed, auditPassed ? "PASS" : "FAIL");
            return auditPassed ? 0 : 1;
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

    private static string GetOption(string[] args, string name)
    {
        string prefix = "--" + name + "=";
        return args == null ? null : args.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?.Substring(prefix.Length);
    }

    private static string Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException && exception.InnerException != null)
            exception = exception.InnerException;
        return exception.ToString();
    }
}
