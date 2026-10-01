using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class AssetKeyResolver
{
    internal static string Resolve(Prop prop) =>
        prop switch
        {
            Workstation workstation =>
                $"prop.workstation.{Slug(workstation.WorkstationType.ToString())}",
            Seat seat => ResolveByName("prop.seat", seat.Name),
            Container container => ResolveByName("prop.container", container.Name),
            Trigger trigger => ResolveByName("prop.trigger", trigger.Name),
            Trap trap => $"prop.trap.{Slug(trap.TrapKind.ToString())}",
            Bed => "prop.bed.basic",
            Cell => "prop.cell.basic",
            Sign => "prop.sign.basic",
            _ => throw new InvalidOperationException($"{prop.GetType().Name} has no asset key."),
        };

    private static string ResolveByName(string prefix, string name)
    {
        var key = $"{prefix}.{Slug(name)}";
        return PropFootprintCatalog.Contains(key) ? key : $"{prefix}.basic";
    }

    private static string Slug(string value) => value.Trim().ToLowerInvariant().Replace(' ', '_');
}
