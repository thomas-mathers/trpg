using Spectre.Console;

namespace TRPG.Client;

internal static class Theme
{
    public const string Negative = "#E05252";
    public const string Accent = "#E0AC52";
    public const string Caution = "#E0D552";
    public const string Positive = "#52E052";
    public const string PlayerAccent = "#52D5E0";
    public const string ApBar = "#5299E0";
    public const string CommandSyntax = "#5275E0";
    public const string MpBar = "#9952E0";
    public const string XpBar = "#E0D552";

    public const string Neutral = "#808080";
    public const string NeutralAccent = "#B3B3B3";
    public const string ChipForeground = "#000000";

    public static readonly Color AccentColor = Color.FromHex(Accent);

    public const string CreatureStateAccent = "#CD52E0";

    public const string CreatureAccent = "#E05E52";
    public const string BuildingAccent = "#E0D752";
    public const string DistrictAccent = "#6EE052";
    public const string WorldAccent = "#52E0AE";
    public const string CountryAccent = "#5297E0";
    public const string StateAccent = "#8652E0";
    public const string CityAccent = "#E052BF";
}
