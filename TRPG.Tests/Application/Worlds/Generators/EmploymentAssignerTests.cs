using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Generators;

public class EmploymentAssignerTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private static readonly HourWindow WorkHours = new(8, 18);

    [Fact]
    public void AssignEmployment_AssignsMatchingProfessionAndWorkJob_WhenSlotAvailable()
    {
        // Arrange
        var shopRoomId = Guid.NewGuid();
        var adult = Builders.MakeCreature(_worldId, profession: Profession.Unemployed);
        var context = MakeContext(
            eligible: [adult],
            slots:
            [
                new ShopEmploymentSlot(
                    shopRoomId,
                    Profession.Baker,
                    ShopStaffingPolicy.StaffDayOffPatterns[1],
                    WorkHours
                ),
            ],
            homeRooms: new Dictionary<Guid, Guid> { [adult.Id] = Guid.NewGuid() }
        );

        // Act
        EmploymentAssigner.AssignEmployment(context);

        // Assert
        Assert.Equal(Profession.Baker, adult.Profession);
        Assert.Contains(
            context.Jobs,
            j => j.Action == JobAction.Work && j.CreatureId == adult.Id && j.RoomId == shopRoomId
        );
    }

    [Fact]
    public void AssignEmployment_AssignsUnemployedWithSevenIndependentDays_WhenNoSlotsAvailable()
    {
        // Arrange
        var adult = Builders.MakeCreature(_worldId, profession: Profession.Unemployed);
        var context = MakeContext(
            eligible: [adult],
            slots: [],
            homeRooms: new Dictionary<Guid, Guid> { [adult.Id] = Guid.NewGuid() }
        );

        // Act
        EmploymentAssigner.AssignEmployment(context);

        // Assert
        Assert.Equal(Profession.Unemployed, adult.Profession);
        var adultJobs = context.Jobs.Where(j => j.CreatureId == adult.Id).ToArray();
        Assert.Equal(7, adultJobs.Length);
        Assert.Equal(7, adultJobs.Select(j => j.SpecificDay).Distinct().Count());
        Assert.All(adultJobs, j => Assert.NotNull(j.SpecificDay));
    }

    [Fact]
    public void AssignEmployment_SharesFamilyDayOff_WithHomemakerAndKids()
    {
        // Arrange
        var shopRoomId = Guid.NewGuid();
        var homeRoomId = Guid.NewGuid();
        var father = Builders.MakeCreature(_worldId, profession: Profession.Unemployed);
        var homemaker = Builders.MakeCreature(_worldId, profession: Profession.Homemaker);
        var kid = Builders.MakeCreature(_worldId, profession: Profession.Unemployed);
        var household = new List<Creature> { father, homemaker, kid };
        var daysOff = ShopStaffingPolicy.StaffDayOffPatterns[0];

        var context = MakeContext(
            eligible: [father],
            slots: [new ShopEmploymentSlot(shopRoomId, Profession.Baker, daysOff, WorkHours)],
            homeRooms: household.ToDictionary(c => c.Id, _ => homeRoomId),
            households: household.ToDictionary(c => c.Id, _ => household),
            fatherIds: [father.Id]
        );

        // Act
        EmploymentAssigner.AssignEmployment(context);

        // Assert — with no other candidates, the family's own home is the only possible destination
        foreach (var day in daysOff)
        {
            var fatherJob = Assert.Single(
                context.Jobs,
                j => j.CreatureId == father.Id && j.SpecificDay == day
            );
            var homemakerJob = Assert.Single(
                context.Jobs,
                j => j.CreatureId == homemaker.Id && j.SpecificDay == day
            );
            var kidJob = Assert.Single(
                context.Jobs,
                j => j.CreatureId == kid.Id && j.SpecificDay == day
            );

            Assert.Equal(homeRoomId, fatherJob.RoomId);
            Assert.Equal(fatherJob.RoomId, homemakerJob.RoomId);
            Assert.Equal(fatherJob.Action, homemakerJob.Action);
            Assert.Equal(fatherJob.RoomId, kidJob.RoomId);
            Assert.Equal(fatherJob.Action, kidJob.Action);
        }
    }

    [Fact]
    public void AssignEmployment_GivesSoloDayOff_WhenHouseholdHasNoHomemaker()
    {
        // Arrange
        var shopRoomId = Guid.NewGuid();
        var adult = Builders.MakeCreature(_worldId, profession: Profession.Unemployed);
        var daysOff = ShopStaffingPolicy.StaffDayOffPatterns[1];

        // Flagged as a father, but no Homemaker exists in the household — must fall back to a solo day off.
        var context = MakeContext(
            eligible: [adult],
            slots: [new ShopEmploymentSlot(shopRoomId, Profession.Tailor, daysOff, WorkHours)],
            homeRooms: new Dictionary<Guid, Guid> { [adult.Id] = Guid.NewGuid() },
            fatherIds: [adult.Id]
        );

        // Act
        EmploymentAssigner.AssignEmployment(context);

        // Assert
        Assert.Equal(
            daysOff.Count,
            context.Jobs.Count(j => j.CreatureId == adult.Id && j.SpecificDay != null)
        );
        Assert.All(context.Jobs, j => Assert.Equal(adult.Id, j.CreatureId));
    }

    [Fact]
    public void AssignEmployment_ExcludesTavernFromFamilyDay_WhenAMinorIsInTheGroup()
    {
        // Arrange
        var shopRoomId = Guid.NewGuid();
        var homeRoomId = Guid.NewGuid();
        var tavernRoomId = Guid.NewGuid();
        var father = Builders.MakeCreature(_worldId, profession: Profession.Unemployed);
        var homemaker = Builders.MakeCreature(_worldId, profession: Profession.Homemaker);
        var minorKid = Builders.MakeCreature(_worldId);
        minorKid.Profession = null;
        var household = new List<Creature> { father, homemaker, minorKid };
        var daysOff = ShopStaffingPolicy.StaffDayOffPatterns[0];

        var context = MakeContext(
            eligible: [father],
            slots: [new ShopEmploymentSlot(shopRoomId, Profession.Baker, daysOff, WorkHours)],
            homeRooms: household.ToDictionary(c => c.Id, _ => homeRoomId),
            households: household.ToDictionary(c => c.Id, _ => household),
            fatherIds: [father.Id],
            cityIdleCandidates:
            [
                new IdleCandidate(tavernRoomId, Guid.NewGuid(), 10, 1000, BuildingType.Tavern),
            ]
        );

        // Act
        EmploymentAssigner.AssignEmployment(context);

        // Assert — Tavern is the only public candidate and has an overwhelming weight, but it must never be
        // picked for a family day-off with a minor present, so this always falls back to home instead
        var fatherJobs = context
            .Jobs.Where(j => j.CreatureId == father.Id && j.SpecificDay != null)
            .ToArray();
        Assert.NotEmpty(fatherJobs);
        Assert.All(fatherJobs, j => Assert.Equal(homeRoomId, j.RoomId));
    }

    [Fact]
    public void AssignEmployment_SkipsCandidate_WhenCapacityIsBelowGroupHeadcount()
    {
        // Arrange
        var shopRoomId = Guid.NewGuid();
        var homeRoomId = Guid.NewGuid();
        var marketRoomId = Guid.NewGuid();
        var father = Builders.MakeCreature(_worldId, profession: Profession.Unemployed);
        var homemaker = Builders.MakeCreature(_worldId, profession: Profession.Homemaker);
        var kid = Builders.MakeCreature(_worldId, profession: Profession.Unemployed);
        var household = new List<Creature> { father, homemaker, kid };
        var daysOff = ShopStaffingPolicy.StaffDayOffPatterns[0];

        var context = MakeContext(
            eligible: [father],
            slots: [new ShopEmploymentSlot(shopRoomId, Profession.Baker, daysOff, WorkHours)],
            homeRooms: household.ToDictionary(c => c.Id, _ => homeRoomId),
            households: household.ToDictionary(c => c.Id, _ => household),
            fatherIds: [father.Id],
            cityIdleCandidates:
            [
                new IdleCandidate(marketRoomId, Guid.NewGuid(), 2, 1000, BuildingType.GeneralGoods),
            ]
        );

        // Act
        EmploymentAssigner.AssignEmployment(context);

        // Assert — capacity 2 can't fit this group of 3, even with an overwhelming weight, so this falls back home
        var fatherJobs = context
            .Jobs.Where(j => j.CreatureId == father.Id && j.SpecificDay != null)
            .ToArray();
        Assert.NotEmpty(fatherJobs);
        Assert.All(fatherJobs, j => Assert.Equal(homeRoomId, j.RoomId));
    }

    [Fact]
    public void AssignEmployment_SkipsCandidate_WhenBarelyOpenDuringTheDayOffHours()
    {
        // Arrange — the shop is only open 6-10, overlapping the 8-18 day off by a mere 2 hours,
        // below the minimum worthwhile stay
        var shopRoomId = Guid.NewGuid();
        var homeRoomId = Guid.NewGuid();
        var earlyShopRoomId = Guid.NewGuid();
        var adult = Builders.MakeCreature(_worldId, profession: Profession.Unemployed);
        var daysOff = ShopStaffingPolicy.StaffDayOffPatterns[1];

        var context = MakeContext(
            eligible: [adult],
            slots:
            [
                new ShopEmploymentSlot(shopRoomId, Profession.Tailor, daysOff, WorkHours),
            ],
            homeRooms: new Dictionary<Guid, Guid> { [adult.Id] = homeRoomId },
            cityIdleCandidates:
            [
                new IdleCandidate(
                    earlyShopRoomId,
                    Guid.NewGuid(),
                    10,
                    1000,
                    BuildingType.Bakery,
                    new HourWindow(6, 10)
                ),
            ]
        );

        // Act
        EmploymentAssigner.AssignEmployment(context);

        // Assert — the shop has overwhelming weight but too little open-hours overlap, so this
        // always falls back home
        var dayOffJobs = context
            .Jobs.Where(j => j.CreatureId == adult.Id && j.SpecificDay != null)
            .ToArray();
        Assert.NotEmpty(dayOffJobs);
        Assert.All(dayOffJobs, j => Assert.Equal(homeRoomId, j.RoomId));
    }

    [Fact]
    public void AssignEmployment_ClampsStayToOpenHours_WhenDestinationOpensLate()
    {
        // Arrange — an unemployed adult's 6-22 day, with an evening-only venue (16-4) as the only
        // public candidate. The venue pick is weighted, so retry until it comes up, then verify the
        // visit is clamped to the open window rather than spanning the whole day.
        var tavernRoomId = Guid.NewGuid();

        for (var attempt = 0; attempt < 30; attempt++)
        {
            var adult = Builders.MakeCreature(_worldId, profession: Profession.Unemployed);
            var context = MakeContext(
                eligible: [adult],
                slots: [],
                homeRooms: new Dictionary<Guid, Guid> { [adult.Id] = Guid.NewGuid() },
                cityIdleCandidates:
                [
                    new IdleCandidate(
                        tavernRoomId,
                        Guid.NewGuid(),
                        100,
                        1000,
                        BuildingType.Tavern,
                        new HourWindow(16, 4)
                    ),
                ]
            );

            EmploymentAssigner.AssignEmployment(context);

            var tavernJob = context.Jobs.FirstOrDefault(j =>
                j.CreatureId == adult.Id && j.RoomId == tavernRoomId
            );
            if (tavernJob == null)
            {
                continue;
            }

            // Assert
            Assert.Equal(16, tavernJob.StartHour);
            Assert.Equal(22, tavernJob.EndHour);
            return;
        }

        Assert.Fail(
            "The overwhelmingly-weighted venue was never picked in 30 attempts — the weighting or eligibility filter may have regressed."
        );
    }

    [Fact]
    public void AssignEmployment_GivesShopOwnerTheirDayOffPattern_WithoutAnyEligibleAdults()
    {
        // Arrange
        var owner = Builders.MakeCreature(_worldId, profession: Profession.Baker);
        var ownerId = owner.Id;
        var ownerHomeRoomId = Guid.NewGuid();
        var daysOff = ShopStaffingPolicy.StaffDayOffPatterns[0];
        var context = MakeContext(
            eligible: [],
            slots: [],
            homeRooms: new Dictionary<Guid, Guid> { [ownerId] = ownerHomeRoomId },
            households: new Dictionary<Guid, List<Creature>> { [ownerId] = [owner] },
            ownerAssignments: [new StaffDayOff(ownerId, daysOff, WorkHours)]
        );

        // Act
        EmploymentAssigner.AssignEmployment(context);

        // Assert
        var ownerJobs = context.Jobs.Where(j => j.CreatureId == ownerId).ToArray();
        Assert.Equal(daysOff.Count, ownerJobs.Length);
        Assert.Equal(daysOff.ToHashSet(), ownerJobs.Select(j => j.SpecificDay!.Value).ToHashSet());
    }

    private CityEmploymentContext MakeContext(
        IReadOnlyList<Creature> eligible,
        IReadOnlyList<ShopEmploymentSlot> slots,
        IReadOnlyDictionary<Guid, Guid>? homeRooms = null,
        IReadOnlyDictionary<Guid, List<Creature>>? households = null,
        IReadOnlyList<Guid>? fatherIds = null,
        IReadOnlyList<StaffDayOff>? ownerAssignments = null,
        IReadOnlyList<IdleCandidate>? cityIdleCandidates = null
    )
    {
        return new CityEmploymentContext
        {
            OpenShopSlots = slots.ToList(),
            EligibleForEmployment = eligible.ToList(),
            ShopOwnerAssignments = ownerAssignments?.ToList() ?? [],
            HouseholdByMemberId =
                households?.ToDictionary(kv => kv.Key, kv => kv.Value)
                ?? eligible.ToDictionary(c => c.Id, c => new List<Creature> { c }),
            HomeRoomIdByMemberId =
                homeRooms?.ToDictionary(kv => kv.Key, kv => kv.Value)
                ?? new Dictionary<Guid, Guid>(),
            FatherIds = fatherIds?.ToHashSet() ?? [],
            CityIdleCandidates = cityIdleCandidates?.ToList() ?? [],
            StateId = Guid.NewGuid(),
            WorldId = _worldId,
            Jobs = [],
        };
    }
}
