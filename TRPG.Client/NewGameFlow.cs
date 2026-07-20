using System.Text.Json;
using Spectre.Console;
using TRPG.Client.Extensions;
using TRPG.Contracts;
using TRPG.Contracts.Jobs.Responses;
using TRPG.Contracts.Worlds.Requests;
using TRPG.Contracts.Worlds.Responses;

namespace TRPG.Client;

internal sealed class NewGameFlow(GameServerClient client)
{
    public async Task<Guid?> Run(CancellationToken cancellationToken)
    {
        var request = PromptForGameOptions();
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

    private static CreateWorldRequest? PromptForGameOptions()
    {
        AnsiConsole.Write(new Rule("Character").RuleStyle(Theme.Neutral).LeftJustified());

        var name = AnsiConsole.Ask<string>("Name");

        var gender = AnsiConsole.Prompt(
            new SelectionPrompt<Gender>()
                .Title("Gender")
                .AddChoices(Enum.GetValues<Gender>())
                .UseConverter(value => value.ToDisplayName())
        );

        AnsiConsole.MarkupLine($"[{Theme.Positive}]✓[/] Gender: {gender.ToDisplayName()}");

        var age = AnsiConsole.Prompt(
            new SelectionPrompt<Age>()
                .Title("Age")
                .AddChoices(Enum.GetValues<Age>())
                .UseConverter(value => value.ToDisplayName())
        );

        AnsiConsole.MarkupLine($"[{Theme.Positive}]✓[/] Age: {age.ToDisplayName()}");

        var race = AnsiConsole.Prompt(
            new SelectionPrompt<Race>()
                .Title("Race")
                .AddChoices(Enum.GetValues<Race>())
                .UseConverter(value => value.ToDisplayName())
        );

        AnsiConsole.MarkupLine($"[{Theme.Positive}]✓[/] Race: {race.ToDisplayName()}");

        var playerClass = AnsiConsole.Prompt(
            new SelectionPrompt<PlayerClass>()
                .Title("Class")
                .AddChoices(Enum.GetValues<PlayerClass>())
                .UseConverter(value => value.ToDisplayName())
        );

        AnsiConsole.MarkupLine($"[{Theme.Positive}]✓[/] Class: {playerClass.ToDisplayName()}");

        AnsiConsole.Write(new Rule("World").RuleStyle(Theme.Neutral).LeftJustified());

        var description = AnsiConsole.Ask("Description", WorldGenerationDefaults.Description);

        AnsiConsole.Write(new Rule("Geography").RuleStyle(Theme.Neutral).LeftJustified());

        var minCityStates = AnsiConsole.Ask(
            "Min city states",
            WorldGenerationDefaults.MinCityStates
        );

        var maxCityStates = AnsiConsole.Ask(
            "Max city states",
            WorldGenerationDefaults.MaxCityStates
        );

        var minRuralStates = AnsiConsole.Ask(
            "Min rural states",
            WorldGenerationDefaults.MinRuralStates
        );

        var maxRuralStates = AnsiConsole.Ask(
            "Max rural states",
            WorldGenerationDefaults.MaxRuralStates
        );

        AnsiConsole.Write(new Rule("Dungeons").RuleStyle(Theme.Neutral).LeftJustified());

        var minDungeonsPerState = AnsiConsole.Ask(
            "Min dungeons per state",
            WorldGenerationDefaults.MinBuildingsPerState
        );

        var maxDungeonsPerState = AnsiConsole.Ask(
            "Max dungeons per state",
            WorldGenerationDefaults.MaxBuildingsPerState
        );

        AnsiConsole.Write(new Rule("Factions").RuleStyle(Theme.Neutral).LeftJustified());

        var minFactionMembers = AnsiConsole.Ask(
            "Min faction members",
            WorldGenerationDefaults.MinFactionMembers
        );

        var maxFactionMembers = AnsiConsole.Ask(
            "Max faction members",
            WorldGenerationDefaults.MaxFactionMembers
        );

        var numFactions = AnsiConsole.Ask("Num factions", WorldGenerationDefaults.FactionCount);

        AnsiConsole.Write(new Rule("Households").RuleStyle(Theme.Neutral).LeftJustified());

        var housesPerCity = AnsiConsole.Ask("Houses/city", WorldGenerationDefaults.HousesPerCity);

        var minHouseholdSize = AnsiConsole.Ask(
            "Min household size",
            WorldGenerationDefaults.MinHouseholdSize
        );

        var maxHouseholdSize = AnsiConsole.Ask(
            "Max household size",
            WorldGenerationDefaults.MaxHouseholdSize
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
                ["Description", description.EscapeMarkup()],
                ["City states", $"{minCityStates}–{maxCityStates}"],
                ["Rural states", $"{minRuralStates}–{maxRuralStates}"],
                ["Dungeons/state", $"{minDungeonsPerState}–{maxDungeonsPerState}"],
                ["Faction members", $"{minFactionMembers}–{maxFactionMembers}"],
                ["Factions", numFactions.ToString()],
                ["Houses/city", housesPerCity.ToString()],
                ["Household size", $"{minHouseholdSize}–{maxHouseholdSize}"],
            ]
        );

        if (!AnsiConsole.Confirm("Create this world?"))
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
