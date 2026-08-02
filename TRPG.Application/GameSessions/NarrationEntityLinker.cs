using System.Runtime.CompilerServices;
using System.Text;
using TRPG.Application.Scenes.Queries;

namespace TRPG.Application.GameSessions;

internal interface IEntityNameMatcher
{
    bool IsPrefixOfAny(string candidate);
    NamedEntitySummary? FindExactMatch(string text);
}

internal sealed class LinearEntityNameMatcher(IReadOnlyCollection<NamedEntitySummary> namedEntities)
    : IEntityNameMatcher
{
    public bool IsPrefixOfAny(string candidate) =>
        namedEntities.Any(entity =>
            entity.Name.StartsWith(candidate, StringComparison.OrdinalIgnoreCase)
        );

    public NamedEntitySummary? FindExactMatch(string text) =>
        namedEntities.FirstOrDefault(entity =>
            entity.Name.Equals(text, StringComparison.OrdinalIgnoreCase)
        );
}

internal static class NarrationEntityLinker
{
    public static async IAsyncEnumerable<string> Link(
        IAsyncEnumerable<string> tokens,
        IReadOnlyCollection<NamedEntitySummary> namedEntities,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var state = new MatchState(new LinearEntityNameMatcher(namedEntities));

        await foreach (var token in tokens.WithCancellation(cancellationToken))
        {
            foreach (var tokenChar in token)
            {
                foreach (var flushed in state.Advance(tokenChar))
                {
                    yield return flushed;
                }
            }
        }

        foreach (var flushed in state.Finish())
        {
            yield return flushed;
        }
    }

    private sealed record Checkpoint(int Length, NamedEntitySummary Entity);

    private sealed class MatchState(IEntityNameMatcher matcher)
    {
        private readonly StringBuilder _pendingText = new();
        private readonly Queue<char> _requeue = new();
        private Checkpoint? _checkpoint;
        private string _prefix = "";

        public IEnumerable<string> Advance(char c)
        {
            _requeue.Enqueue(c);
            while (_requeue.TryDequeue(out var next))
            {
                foreach (var flushed in AdvanceOne(next))
                {
                    yield return flushed;
                }
            }
        }

        public IEnumerable<string> Finish()
        {
            while (true)
            {
                if (_checkpoint != null)
                {
                    foreach (var flushed in ResolveCheckpoint())
                    {
                        yield return flushed;
                    }
                }

                if (!_requeue.TryDequeue(out var c))
                {
                    break;
                }

                foreach (var flushed in AdvanceOne(c))
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

        private IEnumerable<string> AdvanceOne(char c)
        {
            var candidate = _prefix + c;
            if (matcher.IsPrefixOfAny(candidate))
            {
                _prefix = candidate;
                if (matcher.FindExactMatch(_prefix) is { } match)
                {
                    _checkpoint = new Checkpoint(_prefix.Length, match);
                }
                yield break;
            }

            if (_checkpoint != null)
            {
                foreach (var flushed in ResolveCheckpoint())
                {
                    yield return flushed;
                }
                _requeue.Enqueue(c);
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

        private IEnumerable<string> ResolveCheckpoint()
        {
            var checkpoint = _checkpoint!;
            _checkpoint = null;

            foreach (var flushed in DrainPendingText())
            {
                yield return flushed;
            }
            yield return ToMarkup(checkpoint.Entity);

            var leftover = _prefix[checkpoint.Length..];
            _prefix = "";

            foreach (var leftoverChar in leftover)
            {
                _requeue.Enqueue(leftoverChar);
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

    private static string ToMarkup(NamedEntitySummary entity) =>
        $"[{entity.Name}](entity://{entity.Type}/{entity.Id})";
}
