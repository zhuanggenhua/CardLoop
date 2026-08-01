using System;
using System.Collections.Generic;

namespace FantasyWord.GameCore
{
    public readonly struct ElementReactionCandidate
    {
        public ElementReactionCandidate(string stableId, ElementReactionDefinition definition)
        {
            StableId = stableId;
            Definition = definition;
        }

        public string StableId { get; }
        public ElementReactionDefinition Definition { get; }
    }

    /// <summary>
    /// 纯规则筛选器。运行时系统负责缓存候选和执行结果，本类型只保证匹配与排序确定性。
    /// </summary>
    public static class ElementReactionResolver
    {
        private sealed class CandidateComparer : IComparer<ElementReactionCandidate>
        {
            public static readonly CandidateComparer Instance = new();

            public int Compare(ElementReactionCandidate first, ElementReactionCandidate second)
            {
                int priorityComparison = second.Definition.Priority.CompareTo(first.Definition.Priority);
                return priorityComparison != 0
                    ? priorityComparison
                    : string.Compare(first.StableId, second.StableId, StringComparison.Ordinal);
            }
        }

        public static void CollectMatches(
            IReadOnlyList<ElementReactionCandidate> candidates,
            in ElementReactionContext context,
            List<ElementReactionCandidate> matches)
        {
            if (matches == null)
            {
                throw new ArgumentNullException(nameof(matches));
            }

            matches.Clear();
            if (candidates == null)
            {
                return;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                ElementReactionCandidate candidate = candidates[i];
                if (!string.IsNullOrEmpty(candidate.StableId) &&
                    candidate.Definition != null &&
                    candidate.Definition.Matches(context))
                {
                    matches.Add(candidate);
                }
            }

            matches.Sort(CandidateComparer.Instance);
        }
    }
}
