using System.Text;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class PropModelNames
{
    internal static string DisplayName(PropModel model)
    {
        var name = model.ToString().Replace(nameof(Furniture), "", StringComparison.Ordinal);
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
