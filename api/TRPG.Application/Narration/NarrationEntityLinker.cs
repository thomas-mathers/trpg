using System.Runtime.CompilerServices;
using System.Text;
using TRPG.Application.Scenes.Queries;

namespace TRPG.Application.Narration;

internal static class NarrationEntityLinker
{
    public static async IAsyncEnumerable<string> Link(
        IAsyncEnumerable<string> tokens,
        EntityNameAutomaton automaton,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var state = new MatchState(automaton);

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

    private sealed record Checkpoint(int Length, LoreAnchorSummary Entity);

    private sealed class MatchState(EntityNameAutomaton automaton)
    {
        private readonly StringBuilder _pendingText = new();
        private readonly StringBuilder _activeSpan = new();
        private readonly Queue<char> _requeue = new();
        private EntityNameAutomaton.Node _node = automaton.Root;
        private Checkpoint? _checkpoint;

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

            if (_activeSpan.Length > 0)
            {
                _pendingText.Append(_activeSpan);
                _activeSpan.Clear();
            }

            foreach (var flushed in DrainPendingText())
            {
                yield return flushed;
            }
        }

        private IEnumerable<string> AdvanceOne(char c)
        {
            var next = _node.Step(c);

            if (next.Depth == _node.Depth + 1)
            {
                _activeSpan.Append(c);
                _node = next;
                if (next.Match is { } match)
                {
                    _checkpoint = new Checkpoint(_activeSpan.Length, match);
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

            var combined = _activeSpan.ToString() + c;
            var discardLength = combined.Length - next.Depth;

            _pendingText.Append(combined, 0, discardLength);
            if (char.IsWhiteSpace(combined[discardLength - 1]))
            {
                foreach (var flushed in DrainPendingText())
                {
                    yield return flushed;
                }
            }

            _activeSpan.Clear();
            _activeSpan.Append(combined, discardLength, next.Depth);
            _node = next;
            if (next.Match is { } recoveredMatch)
            {
                _checkpoint = new Checkpoint(_activeSpan.Length, recoveredMatch);
            }
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

            var leftoverLength = _activeSpan.Length - checkpoint.Length;
            var leftover = _activeSpan.ToString(checkpoint.Length, leftoverLength);
            _activeSpan.Clear();
            _node = automaton.Root;

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

    private static string ToMarkup(LoreAnchorSummary entity) =>
        $"[{entity.Name}](entity://{entity.Type}/{entity.Id})";
}
