using System.Globalization;
using System.Text.Json;
using Spectre.Console;
using TRPG.Client.Extensions;
using TRPG.Contracts;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.Jobs.Responses;
using TRPG.Contracts.Worlds.Requests;
using TRPG.Contracts.Worlds.Responses;

namespace TRPG.Client;

internal sealed class NewGameFlow(TrpgHttpClient client)
{
    public async Task<Guid?> Run(CancellationToken cancellationToken)
    {
        var request = await PromptForGameOptions(cancellationToken);
        if (request == null)
        {
            return null;
        }

        var jobId = await client.CreateWorld(request, cancellationToken);

        var jobStatus = await AnsiConsole
            .Status()
            .StartAsync(
                "Generating world...",
                _ => WaitForJobWithProgress(jobId, cancellationToken)
            );

        if (jobStatus.Status != JobStatus.Done)
        {
            AnsiConsole.AnnounceError(
                $"World generation failed: {jobStatus.ErrorMessage?.EscapeMarkup()}"
            );
            return null;
        }

        var world = JsonSerializer.Deserialize<CreateWorldResponse>(jobStatus.ResultJson!)!;

        AnsiConsole.AnnounceSuccess($"World \"{world.WorldName.EscapeMarkup()}\" generated.");
        AnsiConsole.WriteLine(
            $"Entering \"{world.WorldName}\" as {request.PlayerName} the {request.PlayerClass.ToDisplayName()}..."
        );

        return world.WorldId;
    }

    private async Task<CreateWorldRequest?> PromptForGameOptions(
        CancellationToken cancellationToken
    )
    {
        AnsiConsole.Write(new Rule("Character").RuleStyle(Theme.Neutral).LeftJustified());

        var name = await AnsiConsole.AskAsync<string>("Name", cancellationToken);

        var gender = await AnsiConsole.PromptAsync(
            new SelectionPrompt<Gender>()
                .Title("Gender")
                .AddChoices(Enum.GetValues<Gender>())
                .UseConverter(value => value.ToDisplayName()),
            cancellationToken
        );

        AnsiConsole.MarkupLine($"[{Theme.Positive}]✓[/] Gender: {gender.ToDisplayName()}");

        var age = await AnsiConsole.PromptAsync(
            new SelectionPrompt<Age>()
                .Title("Age")
                .AddChoices(Enum.GetValues<Age>())
                .UseConverter(value => value.ToDisplayName()),
            cancellationToken
        );

        AnsiConsole.MarkupLine($"[{Theme.Positive}]✓[/] Age: {age.ToDisplayName()}");

        var race = await AnsiConsole.PromptAsync(
            new SelectionPrompt<Race>()
                .Title("Race")
                .AddChoices(Enum.GetValues<Race>())
                .UseConverter(value => value.ToDisplayName()),
            cancellationToken
        );

        AnsiConsole.MarkupLine($"[{Theme.Positive}]✓[/] Race: {race.ToDisplayName()}");

        var playerClass = await AnsiConsole.PromptAsync(
            new SelectionPrompt<PlayerClass>()
                .Title("Class")
                .AddChoices(Enum.GetValues<PlayerClass>())
                .UseConverter(value => value.ToDisplayName()),
            cancellationToken
        );

        AnsiConsole.MarkupLine($"[{Theme.Positive}]✓[/] Class: {playerClass.ToDisplayName()}");

        AnsiConsole.Write(new Rule("Starting Attributes").RuleStyle(Theme.Neutral).LeftJustified());

        var generationOptions = await client.GetCreatureGenerationOptions(cancellationToken);
        var startingAttributeAllocation =
            await AttributeAllocationPrompt.Run(
                generationOptions.PointsPerLevel,
                generationOptions.BaseAttributes,
                cancellationToken
            ) ?? new Dictionary<AttributeName, int>();

        AnsiConsole.Write(new Rule("World").RuleStyle(Theme.Neutral).LeftJustified());

        var description = await AnsiConsole.AskAsync(
            "Description",
            WorldGenerationDefaults.Description,
            cancellationToken
        );

        AnsiConsole.Write(new Rule("Geography").RuleStyle(Theme.Neutral).LeftJustified());

        var minCityStates = await AnsiConsole.AskAsync(
            "Min city states",
            WorldGenerationDefaults.MinCityStates,
            cancellationToken
        );

        var maxCityStates = await AnsiConsole.AskAsync(
            "Max city states",
            WorldGenerationDefaults.MaxCityStates,
            cancellationToken
        );

        var minRuralStates = await AnsiConsole.AskAsync(
            "Min rural states",
            WorldGenerationDefaults.MinRuralStates,
            cancellationToken
        );

        var maxRuralStates = await AnsiConsole.AskAsync(
            "Max rural states",
            WorldGenerationDefaults.MaxRuralStates,
            cancellationToken
        );

        AnsiConsole.Write(new Rule("Dungeons").RuleStyle(Theme.Neutral).LeftJustified());

        var minDungeonsPerState = await AnsiConsole.AskAsync(
            "Min dungeons per state",
            WorldGenerationDefaults.MinBuildingsPerState,
            cancellationToken
        );

        var maxDungeonsPerState = await AnsiConsole.AskAsync(
            "Max dungeons per state",
            WorldGenerationDefaults.MaxBuildingsPerState,
            cancellationToken
        );

        AnsiConsole.Write(new Rule("Factions").RuleStyle(Theme.Neutral).LeftJustified());

        var minFactionMembers = await AnsiConsole.AskAsync(
            "Min faction members",
            WorldGenerationDefaults.MinFactionMembers,
            cancellationToken
        );

        var maxFactionMembers = await AnsiConsole.AskAsync(
            "Max faction members",
            WorldGenerationDefaults.MaxFactionMembers,
            cancellationToken
        );

        var numFactions = await AnsiConsole.AskAsync(
            "Num factions",
            WorldGenerationDefaults.FactionCount,
            cancellationToken
        );

        AnsiConsole.Write(new Rule("Households").RuleStyle(Theme.Neutral).LeftJustified());

        var housesPerCity = await AnsiConsole.AskAsync(
            "Houses/city",
            WorldGenerationDefaults.HousesPerCity,
            cancellationToken
        );

        var minHouseholdSize = await AnsiConsole.AskAsync(
            "Min household size",
            WorldGenerationDefaults.MinHouseholdSize,
            cancellationToken
        );

        var maxHouseholdSize = await AnsiConsole.AskAsync(
            "Max household size",
            WorldGenerationDefaults.MaxHouseholdSize,
            cancellationToken
        );

        AnsiConsole.Write(new Rule("Review").RuleStyle(Theme.Neutral).LeftJustified());

        AnsiConsole.PrintTable(
            ["Setting", "Value"],
            [
                ["Name", name.EscapeMarkup()],
                ["Gender", AnsiConsole.FormatNeutralChip(gender)],
                ["Age", AnsiConsole.FormatNeutralChip(age)],
                ["Race", AnsiConsole.FormatNeutralChip(race)],
                ["Class", AnsiConsole.FormatNeutralChip(playerClass)],
                ["Attributes", FormatStartingAttributeAllocation(startingAttributeAllocation)],
                ["Description", description.EscapeMarkup()],
                ["City states", $"{minCityStates}–{maxCityStates}"],
                ["Rural states", $"{minRuralStates}–{maxRuralStates}"],
                ["Dungeons/state", $"{minDungeonsPerState}–{maxDungeonsPerState}"],
                ["Faction members", $"{minFactionMembers}–{maxFactionMembers}"],
                ["Factions", numFactions.ToString(CultureInfo.InvariantCulture)],
                ["Houses/city", housesPerCity.ToString(CultureInfo.InvariantCulture)],
                ["Household size", $"{minHouseholdSize}–{maxHouseholdSize}"],
            ]
        );

        var createWorld = await AnsiConsole.ConfirmAsync(
            "Create this world?",
            cancellationToken: cancellationToken
        );
        if (!createWorld)
        {
            return null;
        }

        return new CreateWorldRequest
        {
            PlayerName = name,
            Race = race,
            Gender = gender,
            Age = age,
            PlayerClass = playerClass,
            StartingAttributeAllocation = startingAttributeAllocation,
            Description = description,
            MinCityStates = minCityStates,
            MaxCityStates = maxCityStates,
            MinRuralStates = minRuralStates,
            MaxRuralStates = maxRuralStates,
            MinBuildingsPerState = minDungeonsPerState,
            MaxBuildingsPerState = maxDungeonsPerState,
            MinFactionMembers = minFactionMembers,
            MaxFactionMembers = maxFactionMembers,
            HousesPerCity = housesPerCity,
            MinHouseholdSize = minHouseholdSize,
            MaxHouseholdSize = maxHouseholdSize,
            FactionCount = numFactions,
        };
    }

    private static string FormatStartingAttributeAllocation(
        IReadOnlyDictionary<AttributeName, int> allocation
    ) =>
        allocation.Count == 0
            ? "None allocated"
            : string.Join(
                ", ",
                allocation.Select(kv => $"{kv.Key.ToDisplayName()} +{kv.Value}")
            );

    private async Task<JobStatusResponse> WaitForJobWithProgress(
        Guid jobId,
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            var status = await client.GetJobStatus(jobId, cancellationToken);
            if (status.Status is JobStatus.Done or JobStatus.Failed or JobStatus.Cancelled)
            {
                return status;
            }

            await Task.Delay(2000, cancellationToken);
        }
    }
}
