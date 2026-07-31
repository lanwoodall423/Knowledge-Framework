using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    internal static class KnowledgeGraphValidation
    {
        internal static bool HasCycle(IEnumerable<KeyValuePair<string, string>> edges)
        {
            Dictionary<string, List<string>> graph = (edges ?? Enumerable.Empty<KeyValuePair<string, string>>())
                .Where(item => !item.Key.NullOrEmpty() && !item.Value.NullOrEmpty()).GroupBy(item => item.Key)
                .ToDictionary(group => group.Key, group => group.Select(item => item.Value).Distinct().ToList());
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            Func<string, bool> visit = null;
            visit = node =>
            {
                if (visiting.Contains(node)) return true;
                if (!visited.Add(node)) return false;
                visiting.Add(node);
                if (graph.TryGetValue(node, out List<string> next))
                    for (int i = 0; i < next.Count; i++) if (visit(next[i])) return true;
                visiting.Remove(node);
                return false;
            };
            return graph.Keys.Any(visit);
        }
    }
}
