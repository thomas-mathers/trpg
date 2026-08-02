using System.Runtime.CompilerServices;
using System.Text;
using TRPG.Application.Scenes.Queries;

namespace TRPG.Application.GameSessions;

internal static class NarrationEntityLinker
{
    public static async IAsyncEnumerable<string> Link(
        IAsyncEnumerable<string> tokens,
        IReadOnlyCollection<NamedEntitySummary> namedEntities,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var state = new MatchState();
        var requeue = new Queue<char>();

        await foreach (var token in tokens.WithCancellation(cancellationToken))
        {
            foreach (var tokenChar in token)
            {
                requeue.Enqueue(tokenChar);
                while (requeue.TryDequeue(out var c))
                {
                    foreach (var flushed in state.Advance(c, namedEntities, requeue))
                    {
                        yield return flushed;
                    }
                }
            }
        }

        foreach (var flushed in state.Finish(namedEntities))
        {
            yield return flushed;
        }
    }

    // Tracks the in-progress prefix match plus the longest point during the current run where the
    // prefix exactly equaled some entity's name (a "checkpoint"). A completed match can still be
    // a valid prefix of a longer entity's name one character further (e.g. "Alden Ashford" vs
    // "Alden Ashford Junior"), so accumulation keeps extending past a checkpoint chasing a longer
    // candidate that may ultimately fail. Because the prefix only ever grows during that chase,
    // each new checkpoint is always longer than the last, so only the most recent one is ever
    // useful — a single slot, overwritten on every new exact match. When extension finally fails,
    // that checkpoint wins if one exists; everything accumulated after it is requeued and
    // reprocessed from scratch through the same Advance logic, since it was never actually
    // consumed by a match and may itself contain further entity names or just be plain text —
    // this is also what lets a dangling match at end-of-stream resolve correctly without a
    // separate code path.
    private sealed class MatchState
    {
        private readonly StringBuilder _pendingText = new();
        private (int Length, NamedEntitySummary Entity)? _checkpoint;
        private string _prefix = "";

        public IEnumerable<string> Advance(
            char c,
            IReadOnlyCollection<NamedEntitySummary> namedEntities,
            Queue<char> requeue
        )
        {
            var candidate = _prefix + c;
            if (IsPrefixOfAny(candidate, namedEntities))
            {
                _prefix = candidate;
                if (FindExactMatch(_prefix, namedEntities) is { } match)
                {
                    _checkpoint = (_prefix.Length, match);
                }
                yield break;
            }

            if (_checkpoint != null)
            {
                foreach (var flushed in ResolveCheckpoint(requeue))
                {
                    yield return flushed;
                }
                requeue.Enqueue(c);
                yield break;
            }

            if (_prefix.Length > 0)
            {
                _pendingText.Append(_prefix);
                if (char.IsWhiteSpace(_prefix[^1]))
                {
                    foreach (var flushed in DrainPendingText())
                    {
                        yield return flushed;
                    }
                }
            }

            _prefix = c.ToString();
        }

        public IEnumerable<string> Finish(IReadOnlyCollection<NamedEntitySummary> namedEntities)
        {
            var requeue = new Queue<char>();

            while (true)
            {
                if (_checkpoint != null)
                {
                    foreach (var flushed in ResolveCheckpoint(requeue))
                    {
                        yield return flushed;
                    }
                }

                if (!requeue.TryDequeue(out var c))
                {
                    break;
                }

                foreach (var flushed in Advance(c, namedEntities, requeue))
                {
                    yield return flushed;
                }
            }

            if (_prefix.Length > 0)
            {
                _pendingText.Append(_prefix);
                _prefix = "";
            }

            foreach (var flushed in DrainPendingText())
            {
                yield return flushed;
            }
        }

        private IEnumerable<string> ResolveCheckpoint(Queue<char> requeue)
        {
            var (length, matchedEntity) = _checkpoint!.Value;
            _checkpoint = null;

            foreach (var flushed in DrainPendingText())
            {
                yield return flushed;
            }
            yield return ToMarkup(matchedEntity);

            var leftover = _prefix[length..];
            _prefix = "";
            foreach (var leftoverChar in leftover)
            {
                requeue.Enqueue(leftoverChar);
            }
        }

        private IEnumerable<string> DrainPendingText()
        {
            if (_pendingText.Length == 0)
            {
                yield break;
            }

            yield return _pendingText.ToString();
            _pendingText.Clear();
        }
    }

    private static bool IsPrefixOfAny(
        string candidate,
        IReadOnlyCollection<NamedEntitySummary> namedEntities
    ) =>
        namedEntities.Any(entity =>
            entity.Name.StartsWith(candidate, StringComparison.OrdinalIgnoreCase)
        );

    private static NamedEntitySummary? FindExactMatch(
        string text,
        IReadOnlyCollection<NamedEntitySummary> namedEntities
    ) =>
        namedEntities.FirstOrDefault(entity =>
            entity.Name.Equals(text, StringComparison.OrdinalIgnoreCase)
        );

    private static string ToMarkup(NamedEntitySummary entity) =>
        $"[{entity.Name}](entity://{entity.Type}/{entity.Id})";
}
