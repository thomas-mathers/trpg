using TRPG.Application.Scenes.Queries;

namespace TRPG.Application.GameSessions;

internal sealed class EntityNameAutomaton
{
    public Node Root { get; } = new(depth: 0);

    private EntityNameAutomaton() { }

    public static EntityNameAutomaton Build(IReadOnlyCollection<LoreAnchorSummary> entities)
    {
        var automaton = new EntityNameAutomaton();
        automaton.Root.Fail = automaton.Root;
        var ambiguousNames = entities
            .GroupBy(entity => entity.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            if (ambiguousNames.Contains(entity.Name))
            {
                continue;
            }

            var node = automaton.Root;
            foreach (var character in entity.Name)
            {
                node = node.GetOrAddChild(char.ToLowerInvariant(character));
            }
            node.Match ??= entity;
        }

        automaton.ComputeFailureLinks();
        return automaton;
    }

    public static EntityNameAutomaton Build(IReadOnlyCollection<NamedEntitySummary> entities) =>
        Build(
            entities
                .Select(entity => new LoreAnchorSummary(
                    entity.Id,
                    entity.Name,
                    (LoreAnchorType)entity.Type,
                    entity.Subtype,
                    entity.Description
                ))
                .ToArray()
        );

    private void ComputeFailureLinks()
    {
        var queue = new Queue<Node>();
        foreach (var child in Root.Children.Values)
        {
            child.Fail = Root;
            queue.Enqueue(child);
        }

        while (queue.TryDequeue(out var node))
        {
            foreach (var (key, child) in node.Children)
            {
                var fail = node.Fail;
                while (fail != Root && !fail.Children.ContainsKey(key))
                {
                    fail = fail.Fail;
                }

                child.Fail = fail.Children.GetValueOrDefault(key, Root);
                queue.Enqueue(child);
            }
        }
    }

    public sealed class Node(int depth)
    {
        public int Depth { get; } = depth;
        public LoreAnchorSummary? Match { get; internal set; }
        internal Dictionary<char, Node> Children { get; } = new();
        internal Node Fail { get; set; } = null!;

        internal Node GetOrAddChild(char key)
        {
            if (!Children.TryGetValue(key, out var child))
            {
                child = new Node(Depth + 1);
                Children[key] = child;
            }
            return child;
        }

        public Node Step(char character)
        {
            var key = char.ToLowerInvariant(character);
            var node = this;
            while (true)
            {
                if (node.Children.TryGetValue(key, out var next))
                {
                    return next;
                }

                if (node.Fail == node)
                {
                    return node;
                }

                node = node.Fail;
            }
        }
    }
}
