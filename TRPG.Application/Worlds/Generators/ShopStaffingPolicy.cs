using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

internal record ShopEmploymentSlot(
    Guid RoomId,
    Profession EmployeeProfession,
    IReadOnlyList<DayOfWeek> DaysOff,
    HourWindow WorkHours,
    HourWindow? SleepHours = null
);

internal record StaffDayOff(
    Guid CreatureId,
    IReadOnlyList<DayOfWeek> DaysOff,
    HourWindow WorkHours
);

internal static class ShopStaffingPolicy
{
    internal const int MaxShopStaff = 3;

    internal static readonly BuildingType[] StandardBuildingTypes =
    [
        BuildingType.ArcaneShop,
        BuildingType.Apothecary,
        BuildingType.Bakery,
        BuildingType.Barracks,
        BuildingType.Blacksmith,
        BuildingType.Carpenter,
        BuildingType.Castle,
        BuildingType.GeneralGoods,
        BuildingType.GuildHall,
        BuildingType.Inn,
        BuildingType.Jail,
        BuildingType.Jeweler,
        BuildingType.Library,
        BuildingType.Stable,
        BuildingType.Tailor,
        BuildingType.Tavern,
        BuildingType.Temple,
    ];

    internal static readonly IReadOnlyList<DayOfWeek>[] StaffDayOffPatterns =
    [
        [DayOfWeek.Saturday, DayOfWeek.Sunday],
        [DayOfWeek.Monday, DayOfWeek.Tuesday],
        [DayOfWeek.Wednesday, DayOfWeek.Thursday],
    ];

    internal static readonly IReadOnlyList<DayOfWeek>[] NonOverlappingDayOffPatterns =
    [
        [DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday],
        [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday],
    ];

    internal static Profession GetProfessionForBuilding(BuildingType type)
    {
        return type switch
        {
            BuildingType.Tavern => Profession.Bartender,
            BuildingType.Blacksmith => Profession.Blacksmith,
            BuildingType.Temple => Profession.Cleric,
            BuildingType.Library => Profession.Scholar,
            BuildingType.GeneralGoods => Profession.Merchant,
            BuildingType.Apothecary => Profession.Alchemist,
            BuildingType.Bakery => Profession.Baker,
            BuildingType.Stable => Profession.StableMaster,
            BuildingType.ArcaneShop => Profession.Mage,
            BuildingType.GuildHall => Profession.Politician,
            BuildingType.Castle => Profession.Politician,
            BuildingType.Jail => Profession.Guard,
            BuildingType.Inn => Profession.Innkeeper,
            BuildingType.Barracks => Profession.Guard,
            BuildingType.Tailor => Profession.Tailor,
            BuildingType.Carpenter => Profession.Carpenter,
            BuildingType.Jeweler => Profession.Jeweler,
            _ => throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "No profession mapped for this building type."
            ),
        };
    }

    internal static Profession GetEmployeeProfessionForBuilding(BuildingType type)
    {
        return type == BuildingType.Castle ? Profession.Knight : GetProfessionForBuilding(type);
    }

    // Inn runs its own day/night shift model entirely separately from this — it never calls this.
    internal static HourWindow GetWorkHoursForBuilding(BuildingType type)
    {
        return type switch
        {
            BuildingType.Bakery => new HourWindow(6, 14),
            BuildingType.Tavern => new HourWindow(16, 4),
            BuildingType.Blacksmith or BuildingType.Carpenter or BuildingType.Stable =>
                new HourWindow(7, 17),
            BuildingType.ArcaneShop or BuildingType.Library => new HourWindow(9, 19),
            BuildingType.Castle or BuildingType.Jail or BuildingType.Barracks => new HourWindow(
                8,
                20
            ),
            _ => new HourWindow(8, 18),
        };
    }

    internal static HourWindow? GetSleepHoursForBuilding(BuildingType type)
    {
        return type == BuildingType.Tavern ? new HourWindow(4, 12) : null;
    }

    internal static void GenerateInnStaffing(
        Guid stateId,
        Guid worldId,
        Guid ownerId,
        Guid groundFloorRoomId,
        List<Job> jobs,
        List<StaffDayOff> shopOwnerAssignments,
        List<ShopEmploymentSlot> openShopSlots
    )
    {
        var dayShiftHours = new HourWindow(6, 18);
        var nightShiftHours = new HourWindow(18, 6);
        var nightShiftSleepHours = new HourWindow(6, 14);

        jobs.Add(
            JobGenerator.GenerateWork(stateId, ownerId, groundFloorRoomId, worldId, dayShiftHours)
        );

        shopOwnerAssignments.Add(
            new StaffDayOff(ownerId, NonOverlappingDayOffPatterns[0], dayShiftHours)
        );

        openShopSlots.Add(
            new ShopEmploymentSlot(
                groundFloorRoomId,
                Profession.Innkeeper,
                NonOverlappingDayOffPatterns[1],
                dayShiftHours
            )
        );
        openShopSlots.Add(
            new ShopEmploymentSlot(
                groundFloorRoomId,
                Profession.Innkeeper,
                NonOverlappingDayOffPatterns[0],
                nightShiftHours,
                nightShiftSleepHours
            )
        );
        openShopSlots.Add(
            new ShopEmploymentSlot(
                groundFloorRoomId,
                Profession.Innkeeper,
                NonOverlappingDayOffPatterns[1],
                nightShiftHours,
                nightShiftSleepHours
            )
        );
    }
}
