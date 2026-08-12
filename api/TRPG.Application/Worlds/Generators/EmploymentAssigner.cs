using TRPG.Application.Common.Algorithms;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

internal record IdleCandidate(
    Guid LocationId,
    int Capacity,
    int Weight,
    BuildingType? BuildingType,
    HourWindow? OpenHours = null
);

internal class CityEmploymentContext
{
    public required List<ShopEmploymentSlot> OpenShopSlots { get; init; }
    public required List<Creature> EligibleForEmployment { get; init; }
    public required List<StaffDayOff> ShopOwnerAssignments { get; init; }
    public required Dictionary<Guid, List<Creature>> HouseholdByMemberId { get; init; }
    public required Dictionary<Guid, Guid> HomeLocationIdByMemberId { get; init; }
    public required HashSet<Guid> FatherIds { get; init; }
    public required List<IdleCandidate> CityIdleCandidates { get; init; }
    public required Guid WorldId { get; init; }
    public required List<CreatureJob> Jobs { get; init; }
}

internal static class EmploymentAssigner
{
    internal static readonly CreatureJobAction[] DayOffActivities =
    [
        CreatureJobAction.Idle,
        CreatureJobAction.Study,
        CreatureJobAction.Pray,
        CreatureJobAction.Train,
        CreatureJobAction.Sit,
    ];

    private static readonly DayOfWeek[] AllWeekdays = Enum.GetValues<DayOfWeek>();

    private record DayOffNeed(
        IReadOnlyList<Guid> ParticipantIds,
        Guid HomeLocationId,
        CreatureJobAction Action,
        HourWindow Hours,
        bool ExcludeTavern,
        bool IsUnemployedActivity
    );

    internal static void AssignEmployment(CityEmploymentContext context)
    {
        var needsByDay = AllWeekdays.ToDictionary(day => day, _ => new List<DayOffNeed>());

        var slotIndex = 0;
        foreach (var adult in context.EligibleForEmployment)
        {
            if (slotIndex >= context.OpenShopSlots.Count)
            {
                adult.Profession = Profession.Unemployed;
                RegisterUnemployedWeek(adult, needsByDay, context);
                continue;
            }

            var slot = context.OpenShopSlots[slotIndex++];
            adult.Profession = slot.EmployeeProfession;
            context.Jobs.Add(
                CreatureJobGenerator.GenerateWork(
                    adult.Id,
                    slot.LocationId,
                    context.WorldId,
                    slot.WorkHours
                )
            );
            CreatureJobGenerator.ApplySleepOverride(
                adult.Id,
                slot.SleepHours,
                context.WorldId,
                context.Jobs
            );
            RegisterDayOff(adult.Id, slot.DaysOff, slot.WorkHours, needsByDay, context);
        }

        foreach (var ownerAssignment in context.ShopOwnerAssignments)
        {
            RegisterDayOff(
                ownerAssignment.CreatureId,
                ownerAssignment.DaysOff,
                ownerAssignment.WorkHours,
                needsByDay,
                context
            );
        }

        GenerateDayOffJobs(needsByDay, context);
    }

    private static void RegisterDayOff(
        Guid adultId,
        IReadOnlyList<DayOfWeek> daysOff,
        HourWindow workHours,
        Dictionary<DayOfWeek, List<DayOffNeed>> needsByDay,
        CityEmploymentContext context
    )
    {
        var household = context.HouseholdByMemberId[adultId];
        var homemaker = context.FatherIds.Contains(adultId)
            ? household.FirstOrDefault(m => m.Profession == Profession.Homemaker)
            : null;

        if (homemaker != null)
        {
            var father = household.First(m => m.Id == adultId);
            var eligibleIds = context.EligibleForEmployment.Select(c => c.Id).ToHashSet();
            var kids = household
                .Where(m => m.Id != adultId && m.Id != homemaker.Id && !eligibleIds.Contains(m.Id))
                .ToList();
            RegisterFamilyDay(father, homemaker, kids, daysOff, workHours, needsByDay, context);
            return;
        }

        RegisterSoloDayOff(
            adultId,
            context.HomeLocationIdByMemberId[adultId],
            daysOff,
            workHours,
            needsByDay
        );
    }

    private static void RegisterFamilyDay(
        Creature father,
        Creature homemaker,
        IReadOnlyList<Creature> kids,
        IReadOnlyList<DayOfWeek> daysOff,
        HourWindow workHours,
        Dictionary<DayOfWeek, List<DayOffNeed>> needsByDay,
        CityEmploymentContext context
    )
    {
        var action = DayOffActivities[Random.Shared.Next(DayOffActivities.Length)];
        var participantIds = new List<Guid> { father.Id, homemaker.Id };
        participantIds.AddRange(kids.Select(k => k.Id));
        var excludeTavern = kids.Any(k => k.Profession == null);
        var homeLocationId = context.HomeLocationIdByMemberId[father.Id];
        var need = new DayOffNeed(
            participantIds,
            homeLocationId,
            action,
            workHours,
            excludeTavern,
            false
        );

        foreach (var day in daysOff)
        {
            needsByDay[day].Add(need);
        }
    }

    private static void RegisterSoloDayOff(
        Guid creatureId,
        Guid homeLocationId,
        IReadOnlyList<DayOfWeek> daysOff,
        HourWindow workHours,
        Dictionary<DayOfWeek, List<DayOffNeed>> needsByDay
    )
    {
        var action = DayOffActivities[Random.Shared.Next(DayOffActivities.Length)];
        var need = new DayOffNeed([creatureId], homeLocationId, action, workHours, false, false);

        foreach (var day in daysOff)
        {
            needsByDay[day].Add(need);
        }
    }

    private static void RegisterUnemployedWeek(
        Creature adult,
        Dictionary<DayOfWeek, List<DayOffNeed>> needsByDay,
        CityEmploymentContext context
    )
    {
        var homeLocationId = context.HomeLocationIdByMemberId[adult.Id];

        foreach (var day in AllWeekdays)
        {
            var action = DayOffActivities[Random.Shared.Next(DayOffActivities.Length)];
            needsByDay[day]
                .Add(
                    new DayOffNeed(
                        [adult.Id],
                        homeLocationId,
                        action,
                        new HourWindow(6, 22),
                        false,
                        true
                    )
                );
        }
    }

    private static void GenerateDayOffJobs(
        Dictionary<DayOfWeek, List<DayOffNeed>> needsByDay,
        CityEmploymentContext context
    )
    {
        foreach (var (day, needs) in needsByDay)
        {
            var remainingCapacity = context.CityIdleCandidates.Select(c => c.Capacity).ToList();

            foreach (var need in needs)
            {
                var destination = PickDayOffDestination(
                    need,
                    context.CityIdleCandidates,
                    remainingCapacity
                );

                foreach (var participantId in need.ParticipantIds)
                {
                    context.Jobs.Add(
                        need.IsUnemployedActivity
                            ? CreatureJobGenerator.GenerateUnemployedDayActivity(
                                participantId,
                                need.Action,
                                destination.LocationId,
                                day,
                                context.WorldId,
                                destination.Hours
                            )
                            : CreatureJobGenerator.GenerateDayOff(
                                participantId,
                                need.Action,
                                destination.LocationId,
                                day,
                                context.WorldId,
                                destination.Hours
                            )
                    );
                }
            }
        }
    }

    private sealed record DayOffDestination(Guid LocationId, HourWindow Hours);

    private static DayOffDestination PickDayOffDestination(
        DayOffNeed need,
        IReadOnlyList<IdleCandidate> cityIdleCandidates,
        List<int> remainingCapacity
    )
    {
        var clampedByIndex = cityIdleCandidates
            .Select(candidate => ClampToOpenHours(need.Hours, candidate.OpenHours))
            .ToArray();
        var eligibleIndices = Enumerable
            .Range(0, cityIdleCandidates.Count)
            .Where(i => remainingCapacity[i] >= need.ParticipantIds.Count)
            .Where(i =>
                !need.ExcludeTavern || cityIdleCandidates[i].BuildingType != BuildingType.Tavern
            )
            .Where(i =>
                clampedByIndex[i] is { } clamped && WindowLength(clamped) >= MinimumStayHours
            )
            .ToArray();

        var weights = eligibleIndices.Select(i => cityIdleCandidates[i].Weight).ToList();
        weights.Add(BuildingGenerator.Popularity[BuildingType.House]);

        var pickedIndex = WeightedSampler.SampleIndex(weights);
        if (pickedIndex == eligibleIndices.Length)
        {
            return new DayOffDestination(need.HomeLocationId, need.Hours);
        }

        var candidateIndex = eligibleIndices[pickedIndex];
        remainingCapacity[candidateIndex] -= need.ParticipantIds.Count;
        return new DayOffDestination(
            cityIdleCandidates[candidateIndex].LocationId,
            clampedByIndex[candidateIndex]!
        );
    }

    private const int MinimumStayHours = 4;

    private static int WindowLength(HourWindow window) => (window.End - window.Start + 24) % 24;

    private static HourWindow? ClampToOpenHours(HourWindow stay, HourWindow? openHours)
    {
        if (openHours == null)
        {
            return stay;
        }

        var stayLength = WindowLength(stay);
        var openLength = WindowLength(openHours);

        var stayOffset = (stay.Start - openHours.Start + 24) % 24;
        if (stayOffset < openLength)
        {
            var length = Math.Min(stayLength, openLength - stayOffset);
            return new HourWindow(stay.Start, (stay.Start + length) % 24);
        }

        var openOffset = (openHours.Start - stay.Start + 24) % 24;
        if (openOffset < stayLength)
        {
            var length = Math.Min(openLength, stayLength - openOffset);
            return new HourWindow(openHours.Start, (openHours.Start + length) % 24);
        }

        return null;
    }
}
