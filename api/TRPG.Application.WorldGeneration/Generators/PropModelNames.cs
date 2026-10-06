using System.Text;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class PropModelNames
{
    private const string SeatPrefix = "Seat";

    internal static bool IsSeat(PropModel model) =>
        model.ToString().StartsWith(SeatPrefix, StringComparison.Ordinal);

    internal static string DisplayName(PropModel model)
    {
        var name = model.ToString().Replace(nameof(Furniture), "", StringComparison.Ordinal);
        if (IsSeat(model))
        {
            name = name[SeatPrefix.Length..];
        }

        var words = new StringBuilder();

        foreach (var letter in name)
        {
            if (char.IsUpper(letter) && words.Length > 0)
            {
                words.Append(' ');
            }

            words.Append(letter);
        }

        return words.ToString();
    }
}
