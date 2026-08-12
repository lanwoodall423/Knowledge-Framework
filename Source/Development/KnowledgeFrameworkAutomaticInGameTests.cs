using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Verse;

namespace KnowledgeFramework
{
    public sealed partial class GameComponent_KnowledgeFramework
    {
        internal void RunAutomaticTestsIfRequested()
        {
            if (KnowledgeFrameworkAutomaticInGameTests.TryRunIfRequested()) return;
            LongEventHandler.ExecuteWhenFinished(RetryAutomaticTests);
        }

        private void RetryAutomaticTests()
        {
            if (!KnowledgeFrameworkAutomaticInGameTests.TryRunIfRequested())
                LongEventHandler.ExecuteWhenFinished(RetryAutomaticTests);
        }
    }

    internal static class KnowledgeFrameworkAutomaticInGameTests
    {
        internal const string RequestFileName = "KnowledgeFramework_AutomaticTest.request";
        internal const string ReportFileName = "KnowledgeFramework_AutomaticTest.txt";
        private static bool requestClaimed;

        private static string RequestPath => Path.Combine(GenFilePaths.ConfigFolderPath, RequestFileName);
        private static string ReportPath => Path.Combine(GenFilePaths.ConfigFolderPath, ReportFileName);

        // Returns false only while a request exists but the playable map is not ready.
        internal static bool TryRunIfRequested()
        {
            if (requestClaimed) return true;

            string requestPath;
            try { requestPath = RequestPath; }
            catch (Exception exception)
            {
                Log.Warning("[Knowledge Framework] Automatic test request path is unavailable: " + exception);
                return true;
            }

            if (!File.Exists(requestPath)) return true;
            if (!GenScene.InPlayScene || Verse.Current.Game == null || Find.CurrentMap == null || Find.TickManager == null)
                return false;
            if (!KnowledgeConsumerApi.Readiness.IsReady)
                return false;

            requestClaimed = true;
            string request = string.Empty;
            try
            {
                request = File.ReadAllText(requestPath);
                File.Delete(requestPath);
            }
            catch (Exception exception)
            {
                WriteFailureReport(request, "Could not claim the automatic test request: " + exception);
                Log.Error("[Knowledge Framework] Automatic in-game test request could not be claimed: " + exception);
                return true;
            }

            Run(request);
            return true;
        }

        private static void Run(string request)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Pawn pawn = Find.CurrentMap?.mapPawns?.FreeColonists?.FirstOrDefault();
            KnowledgeVerificationResult result = null;
            Exception failure = null;
            try
            {
                result = KnowledgeFrameworkVerification.RunGameTests(pawn);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            stopwatch.Stop();
            WriteReport(request, result, pawn, failure, stopwatch.ElapsedMilliseconds);

            bool passed = failure == null && result != null && result.Success;
            string detail = result == null ? "no result" : result.ToString();
            Log.Message("[Knowledge Framework] Automatic in-game verification " + (passed ? "PASS" : "FAIL") +
                ": " + detail + ", pawn=" + (pawn?.thingIDNumber.ToString() ?? "none") +
                ", elapsedMs=" + stopwatch.ElapsedMilliseconds + ", report=" + ReportPath);
            if (!passed && failure != null)
                Log.Error("[Knowledge Framework] Automatic in-game verification exception: " + failure);
            if (!passed && result != null && result.failures.Count > 0)
                Log.Warning("[Knowledge Framework] Automatic in-game verification failures: " + string.Join("; ", result.failures));
        }

        private static void WriteReport(string request, KnowledgeVerificationResult result, Pawn pawn,
            Exception failure, long elapsedMilliseconds)
        {
            if (result == null)
            {
                WriteFailureReport(request, failure?.ToString() ?? "Automatic verification returned no result.", elapsedMilliseconds,
                    pawn);
                return;
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine("Knowledge Framework automatic in-game behavioral verification");
            report.AppendLine("completed=true");
            report.AppendLine("status=" + (failure == null && result.Success ? "PASS" : "FAIL"));
            report.AppendLine("run-id=" + Clean(RequestValue(request, "run-id")));
            report.AppendLine("launch-id=" + Clean(Environment.GetEnvironmentVariable("DEVBRIDGE_LAUNCH_ID")));
            report.AppendLine("map-pawns=" + (Find.CurrentMap?.mapPawns?.AllPawns?.Count ?? 0));
            report.AppendLine("free-colonists=" + (Find.CurrentMap?.mapPawns?.FreeColonists?.Count ?? 0));
            report.AppendLine("pawn=" + (pawn?.thingIDNumber.ToString() ?? "none"));
            report.AppendLine("elapsed-ms=" + elapsedMilliseconds);
            report.AppendLine("pure-passed=" + result.purePassed);
            report.AppendLine("pure-failed=" + result.pureFailed);
            report.AppendLine("game-passed=" + result.gamePassed);
            report.AppendLine("game-failed=" + result.gameFailed);
            report.AppendLine("unavailable=" + result.unavailable);
            report.AppendLine("manual-ui=SKIPPED (automated runner does not claim UI coverage)");
            report.AppendLine("passed-tests=" + string.Join(";", result.passedTests));
            report.AppendLine("failures=" + string.Join(";", result.failures));
            if (failure != null) report.AppendLine("exception=" + Clean(failure.ToString()));
            WriteReportFile(report.ToString());
        }

        private static void WriteFailureReport(string request, string failure, long elapsedMilliseconds = 0, Pawn pawn = null)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("Knowledge Framework automatic in-game behavioral verification");
            report.AppendLine("completed=true");
            report.AppendLine("status=FAIL");
            report.AppendLine("run-id=" + Clean(RequestValue(request, "run-id")));
            report.AppendLine("launch-id=" + Clean(Environment.GetEnvironmentVariable("DEVBRIDGE_LAUNCH_ID")));
            report.AppendLine("pawn=" + (pawn?.thingIDNumber.ToString() ?? "none"));
            report.AppendLine("elapsed-ms=" + elapsedMilliseconds);
            report.AppendLine("failure=" + Clean(failure));
            WriteReportFile(report.ToString());
        }

        private static void WriteReportFile(string contents)
        {
            try
            {
                string reportPath = ReportPath;
                string temporaryPath = reportPath + ".tmp";
                File.WriteAllText(temporaryPath, contents);
                if (File.Exists(reportPath)) File.Delete(reportPath);
                File.Move(temporaryPath, reportPath);
            }
            catch (Exception exception)
            {
                Log.Error("[Knowledge Framework] Automatic in-game verification report could not be written: " + exception);
            }
        }

        private static string RequestValue(string request, string key)
        {
            if (request.NullOrEmpty() || key.NullOrEmpty()) return "unknown";
            string prefix = key + "=";
            return request.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?.Substring(prefix.Length) ?? "unknown";
        }

        private static string Clean(string value) => (value ?? "unknown").Replace("\r", " ").Replace("\n", " ");
    }
}
