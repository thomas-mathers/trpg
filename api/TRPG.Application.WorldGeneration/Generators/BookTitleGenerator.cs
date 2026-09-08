using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public record BookSubject(BookSubjectType Type, string Name);

public record GeneratedBookTitle(string Title, BookSubject Subject, int PageCount);

// Titles are drawn from the world's own countries, factions and trades, so a library reads as
// belonging to this world rather than to fantasy in general.
public static class BookTitleGenerator
{
    private const int MinimumPageCount = 3;
    private const int MaximumPageCount = 9;

    private static readonly string[] GeneralForms =
    [
        "A History of {0}",
        "On the Nature of {0}",
        "Concerning {0}",
        "Meditations upon {0}",
        "The Chronicle of {0}",
        "Observations on {0}",
        "An Account of {0}",
        "The Lesser Annals of {0}",
    ];

    private static readonly string[] PlaceForms =
    [
        "A History of {0}",
        "The Founding of {0}",
        "Roads and Rivers of {0}",
        "Concerning the Customs of {0}",
        "An Account of {0}",
    ];

    private static readonly string[] FactionForms =
    [
        "The Ledger of {0}",
        "Concerning {0}",
        "The Rise of {0}",
        "Articles of {0}",
    ];

    private static readonly string[] TradeForms =
    [
        "The Craft of the {0}",
        "A Treatise on the {0}",
        "Instructions for the Apprentice {0}",
        "On the Labours of the {0}",
    ];

    public static GeneratedBookTitle Generate(BookSubject subject, Random random)
    {
        var forms = FormsFor(subject.Type);
        var form = forms[random.Next(forms.Length)];

        return new GeneratedBookTitle(
            string.Format(form, subject.Name),
            subject,
            random.Next(MinimumPageCount, MaximumPageCount + 1)
        );
    }

    private static string[] FormsFor(BookSubjectType subjectType) =>
        subjectType switch
        {
            BookSubjectType.Country or BookSubjectType.State or BookSubjectType.City => PlaceForms,
            BookSubjectType.Faction => FactionForms,
            BookSubjectType.Profession => TradeForms,
            BookSubjectType.CreatureType or BookSubjectType.Building => GeneralForms,
        };
}
