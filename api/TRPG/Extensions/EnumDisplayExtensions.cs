using System.ComponentModel;
using System.Reflection;

namespace TRPG.Extensions;

public static class EnumDisplayExtensions
{
    public static string ToDisplayName(this Enum value)
    {
        var name = value.ToString();
        var field = value.GetType().GetField(name);
        var description = field?.GetCustomAttribute<DescriptionAttribute>()?.Description;
        return description ?? name;
    }
}
