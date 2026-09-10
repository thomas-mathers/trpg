using System.Text;

namespace TRPG.Balance;

// Prototype only — curated scenarios (not randomized) to compare the short-route-obstacle /
// long-route-loot design against variants, before any of it lands in
// TRPG.Application.WorldGeneration.
internal static class DungeonScenarioExperiment
{
    private enum ShortRouteObstacle
    {
        KeyLock,
        Miniboss,
        TrapGauntlet,
    }

    private enum ThirdRouteObstacle
    {
        None,
        KeyLock,
        Social,
    }

    private record Scenario(
        string Name,
        ShortRouteObstacle ShortObstacle,
        int ShortRouteLength,
        int LongRouteLength,
        int DeadEndCount,
        bool BossOnUpperFloor,
        ThirdRouteObstacle ThirdRoute
    );

    private static readonly string[] TrapKinds = ["Mechanical", "Collapse", "Slope", "Water"];

    public static void Run(string outputPath)
    {
        var scenarios = new[]
        {
            new Scenario(
                "Baseline (key-lock)",
                ShortRouteObstacle.KeyLock,
                2,
                4,
                2,
                false,
                ThirdRouteObstacle.None
            ),
            new Scenario(
                "Miniboss shortcut",
                ShortRouteObstacle.Miniboss,
                2,
                3,
                1,
                false,
                ThirdRouteObstacle.None
            ),
            new Scenario(
                "Trap gauntlet",
                ShortRouteObstacle.TrapGauntlet,
                4,
                5,
                3,
                false,
                ThirdRouteObstacle.None
            ),
            new Scenario(
                "Boss on a floor above",
                ShortRouteObstacle.KeyLock,
                2,
                4,
                2,
                true,
                ThirdRouteObstacle.None
            ),
            new Scenario(
                "Large dungeon, third route",
                ShortRouteObstacle.TrapGauntlet,
                3,
                5,
                2,
                false,
                ThirdRouteObstacle.Social
            ),
        };

        var mixed = new (string Name, string Mermaid)[]
        {
            ("Mixed: layered single route", BuildLayeredRoute()),
            ("Mixed: three routes, no repeats", BuildThreeDistinctRoutes()),
            ("Mixed: ambient trap on loot", BuildKeyLockWithAmbientTrap()),
            ("Lever opens a portcullis", BuildLeverPortcullis()),
            ("Boss shortcut, revived as rare bonus", BuildBossShortcutLever()),
            ("Mandatory portcullis, single route", BuildMandatoryPortcullis()),
            ("Multi-lever portcullis", BuildMultiLeverPortcullis()),
            ("Mandatory portcullis, converging routes", BuildConvergingMandatoryPortcullis()),
        };

        using var writer = new StreamWriter(outputPath);

        foreach (var scenario in scenarios)
        {
            var mermaid = Build(scenario);
            Console.WriteLine(
                $"{scenario.Name}: short={scenario.ShortObstacle} shortLen={scenario.ShortRouteLength} "
                    + $"longLen={scenario.LongRouteLength} deadEnds={scenario.DeadEndCount} "
                    + $"upperFloor={scenario.BossOnUpperFloor} third={scenario.ThirdRoute}"
            );

            writer.WriteLine($"## {scenario.Name}");
            writer.WriteLine();
            writer.WriteLine("```mermaid");
            writer.Write(mermaid);
            writer.WriteLine("```");
            writer.WriteLine();
        }

        foreach (var (name, mermaid) in mixed)
        {
            Console.WriteLine(name);

            writer.WriteLine($"## {name}");
            writer.WriteLine();
            writer.WriteLine("```mermaid");
            writer.Write(mermaid);
            writer.WriteLine("```");
            writer.WriteLine();
        }

        Console.WriteLine($"Wrote {scenarios.Length + mixed.Length} scenario(s) to {outputPath}");
    }

    // Lock at the entry, a trap partway through, a miniboss at the end — one route stacking three
    // independent challenges instead of picking a single flavor for the whole thing.
    private static string BuildLayeredRoute()
    {
        const int entrance = 0;
        const int boss = 1;
        var nextIndex = 2;

        var mermaid = new StringBuilder("graph LR\n");
        mermaid.AppendLine($"    {entrance}[\"Entrance (Floor 1)\"]");
        mermaid.AppendLine($"    {boss}[\"Boss (Floor 1)\"]");

        var longRouteRooms = new List<int>();
        BuildLongRoute(mermaid, entrance, boss, 4, ref nextIndex, longRouteRooms);

        var chest = nextIndex++;
        mermaid.AppendLine($"    {chest}[\"Chest\"]");
        mermaid.AppendLine($"    {longRouteRooms[1]} --- {chest}");

        var sidePassage = nextIndex++;
        mermaid.AppendLine($"    {sidePassage}[\"Side Passage\"]");
        mermaid.AppendLine($"    {longRouteRooms[0]} --- {sidePassage}");
        var keyRoom = nextIndex++;
        mermaid.AppendLine($"    {keyRoom}[\"Key (guarded)\"]");
        mermaid.AppendLine($"    {sidePassage} --- {keyRoom}");

        var lockedEntry = nextIndex++;
        mermaid.AppendLine($"    {lockedEntry}[\"Short\"]");
        mermaid.AppendLine($"    {entrance} -->|Locked, needs key| {lockedEntry}");

        var trapRoom = nextIndex++;
        mermaid.AppendLine($"    {trapRoom}[\"Collapse Trap\"]");
        mermaid.AppendLine($"    {lockedEntry} --- {trapRoom}");
        var rubbleLanding = CreateNamedRoom(mermaid, ref nextIndex, "Rubble Landing (Floor 0)");
        mermaid.AppendLine($"    {trapRoom} -.->|fails, falls through| {rubbleLanding}");
        mermaid.AppendLine($"    {rubbleLanding} ==>|climb back up| {lockedEntry}");

        var minibossRoom = nextIndex++;
        mermaid.AppendLine($"    {minibossRoom}[\"Miniboss Chamber\"]");
        mermaid.AppendLine($"    {trapRoom} --- {minibossRoom}");
        mermaid.AppendLine($"    {minibossRoom} --- {boss}");

        return mermaid.ToString();
    }

    // Two gated routes, deliberately different obstacle types, plus the long safe route — no
    // dungeon should ever repeat the same obstacle across its routes.
    private static string BuildThreeDistinctRoutes()
    {
        const int entrance = 0;
        const int boss = 1;
        var nextIndex = 2;

        var mermaid = new StringBuilder("graph LR\n");
        mermaid.AppendLine($"    {entrance}[\"Entrance (Floor 1)\"]");
        mermaid.AppendLine($"    {boss}[\"Boss (Floor 1)\"]");

        var longRouteRooms = new List<int>();
        BuildLongRoute(mermaid, entrance, boss, 4, ref nextIndex, longRouteRooms);

        for (var deadEndIndex = 0; deadEndIndex < 2; deadEndIndex++)
        {
            var parent = longRouteRooms[deadEndIndex % longRouteRooms.Count];
            var leaf = nextIndex++;
            mermaid.AppendLine($"    {leaf}[\"Chest\"]");
            mermaid.AppendLine($"    {parent} --- {leaf}");
        }

        BuildShortRoute(mermaid, entrance, boss, ShortRouteObstacle.Miniboss, 2, ref nextIndex);
        BuildShortRoute(mermaid, entrance, boss, ShortRouteObstacle.TrapGauntlet, 3, ref nextIndex);

        return mermaid.ToString();
    }

    // The gauntlet obstacle and the existing rare-ambient-trap system are unrelated code paths
    // that should be able to land on the same dungeon without either knowing about the other.
    private static string BuildKeyLockWithAmbientTrap()
    {
        const int entrance = 0;
        const int boss = 1;
        var nextIndex = 2;

        var mermaid = new StringBuilder("graph LR\n");
        mermaid.AppendLine($"    {entrance}[\"Entrance (Floor 1)\"]");
        mermaid.AppendLine($"    {boss}[\"Boss (Floor 1)\"]");

        var longRouteRooms = new List<int>();
        BuildLongRoute(mermaid, entrance, boss, 4, ref nextIndex, longRouteRooms);

        var plainChest = nextIndex++;
        mermaid.AppendLine($"    {plainChest}[\"Chest\"]");
        mermaid.AppendLine($"    {longRouteRooms[2]} --- {plainChest}");

        // An ordinary DungeonContentPolicy.TreasureRoom pick, same as any other dead end — it just
        // happens to be one of the dungeon's 0-2 rare ambient traps too, entirely independent of
        // whichever obstacle the short route rolled.
        var trappedChest = nextIndex++;
        mermaid.AppendLine($"    {trappedChest}[\"Trapped Chest (Collapse)\"]");
        mermaid.AppendLine($"    {longRouteRooms[3]} --- {trappedChest}");
        var rubbleLanding = CreateNamedRoom(mermaid, ref nextIndex, "Rubble Landing (Floor 0)");
        mermaid.AppendLine($"    {trappedChest} -.->|fails, falls through| {rubbleLanding}");
        mermaid.AppendLine($"    {rubbleLanding} ==>|climb back up| {trappedChest}");

        var sidePassage = nextIndex++;
        mermaid.AppendLine($"    {sidePassage}[\"Side Passage\"]");
        mermaid.AppendLine($"    {longRouteRooms[0]} --- {sidePassage}");
        var keyRoom = nextIndex++;
        mermaid.AppendLine($"    {keyRoom}[\"Key (guarded)\"]");
        mermaid.AppendLine($"    {sidePassage} --- {keyRoom}");

        BuildShortRoute(mermaid, entrance, boss, ShortRouteObstacle.KeyLock, 2, ref nextIndex);

        return mermaid.ToString();
    }

    // A lever is a remote state change, not a portable item — nothing to carry or lose. First
    // pass gated a whole separate Short route starting back at the Entrance, but that made the
    // "shortcut" cost more than it saved: reaching the lever meant walking partway down the long
    // route, then backtracking all the way to the Entrance, then walking the short route — far
    // more hops than just finishing the long route already underway. Fixed by having the
    // portcullis open a direct skip-ahead from the lever's own room straight to the Boss, so
    // finding it pays off immediately (skips the remaining long-route rooms) instead of requiring
    // a round trip back to the start.
    private static string BuildLeverPortcullis()
    {
        const int entrance = 0;
        const int boss = 1;
        var nextIndex = 2;

        var mermaid = new StringBuilder("graph LR\n");
        mermaid.AppendLine($"    {entrance}[\"Entrance (Floor 1)\"]");
        mermaid.AppendLine($"    {boss}[\"Boss (Floor 1)\"]");

        var longRouteRooms = new List<int>();
        BuildLongRoute(mermaid, entrance, boss, 4, ref nextIndex, longRouteRooms);

        var chest = nextIndex++;
        mermaid.AppendLine($"    {chest}[\"Chest\"]");
        mermaid.AppendLine($"    {longRouteRooms[3]} --- {chest}");

        var lever = nextIndex++;
        mermaid.AppendLine($"    {lever}[\"Lever\"]");
        mermaid.AppendLine($"    {longRouteRooms[1]} --- {lever}");
        mermaid.AppendLine($"    {lever} -->|Portcullis, needs lever| {boss}");

        return mermaid.ToString();
    }

    // The original design's "boss shortcut back out" idea, demoted from a mandatory universal
    // mechanic (which is why it was dropped) to a rare bonus, same tier as the third route — the
    // reward for reaching the boss at all, not a required structural piece.
    private static string BuildBossShortcutLever()
    {
        const int entrance = 0;
        const int boss = 1;
        var nextIndex = 2;

        var mermaid = new StringBuilder("graph LR\n");
        mermaid.AppendLine($"    {entrance}[\"Entrance (Floor 1)\"]");
        mermaid.AppendLine($"    {boss}[\"Boss (Floor 1)\"]");

        var longRouteRooms = new List<int>();
        BuildLongRoute(mermaid, entrance, boss, 4, ref nextIndex, longRouteRooms);

        for (var deadEndIndex = 0; deadEndIndex < 2; deadEndIndex++)
        {
            var parent = longRouteRooms[deadEndIndex % longRouteRooms.Count];
            var leaf = nextIndex++;
            mermaid.AppendLine($"    {leaf}[\"Chest\"]");
            mermaid.AppendLine($"    {parent} --- {leaf}");
        }

        BuildShortRoute(mermaid, entrance, boss, ShortRouteObstacle.Miniboss, 2, ref nextIndex);

        var filler = nextIndex++;
        mermaid.AppendLine($"    {filler}[\"Shortcut filler\"]");
        mermaid.AppendLine($"    {boss} --- {filler}");
        var backDoor = nextIndex++;
        mermaid.AppendLine($"    {backDoor}[\"Back door\"]");
        mermaid.AppendLine($"    {filler} --- {backDoor}");
        mermaid.AppendLine($"    {backDoor} -.->|lever, locked from here| {entrance}");

        return mermaid.ToString();
    }

    // No long/short fork at all — this is what makes a lever mandatory rather than optional.
    // Small dungeons can have exactly one route to the boss, and when that route carries a
    // portcullis, there's no alternate path to bypass it with: the forced detour to the lever
    // alcove is the only way forward, not a reward for exploring.
    private static string BuildMandatoryPortcullis()
    {
        const int entrance = 0;
        const int boss = 1;
        var nextIndex = 2;

        var mermaid = new StringBuilder("graph LR\n");
        mermaid.AppendLine($"    {entrance}[\"Entrance (Floor 1)\"]");
        mermaid.AppendLine($"    {boss}[\"Boss (Floor 1)\"]");

        var first = nextIndex++;
        mermaid.AppendLine($"    {first}[\"Room #0\"]");
        mermaid.AppendLine($"    {entrance} -->|Only route| {first}");

        var branchPoint = nextIndex++;
        mermaid.AppendLine($"    {branchPoint}[\"Room #1\"]");
        mermaid.AppendLine($"    {first} --- {branchPoint}");

        var leverAlcove = nextIndex++;
        mermaid.AppendLine($"    {leverAlcove}[\"Lever Alcove\"]");
        mermaid.AppendLine($"    {branchPoint} --- {leverAlcove}");

        var gate = nextIndex++;
        mermaid.AppendLine($"    {gate}[\"Room #2\"]");
        mermaid.AppendLine($"    {branchPoint} -->|Portcullis, needs lever| {gate}");
        mermaid.AppendLine($"    {leverAlcove} -.->|opens| {gate}");
        mermaid.AppendLine($"    {gate} --- {boss}");

        return mermaid.ToString();
    }

    // Skyrim's other lever trope: several switches, all required, not just one. Scattering them
    // across different routes means opening the gate demands having explored more than one route,
    // not just the one that happens to hold the "right" lever.
    private static string BuildMultiLeverPortcullis()
    {
        const int entrance = 0;
        const int boss = 1;
        var nextIndex = 2;

        var mermaid = new StringBuilder("graph LR\n");
        mermaid.AppendLine($"    {entrance}[\"Entrance (Floor 1)\"]");
        mermaid.AppendLine($"    {boss}[\"Boss (Floor 1)\"]");

        var longRouteRooms = new List<int>();
        BuildLongRoute(mermaid, entrance, boss, 4, ref nextIndex, longRouteRooms);

        var chest = nextIndex++;
        mermaid.AppendLine($"    {chest}[\"Chest\"]");
        mermaid.AppendLine($"    {longRouteRooms[3]} --- {chest}");

        var leverA = nextIndex++;
        mermaid.AppendLine($"    {leverA}[\"Lever A\"]");
        mermaid.AppendLine($"    {longRouteRooms[1]} --- {leverA}");

        var routeCFirst = nextIndex++;
        mermaid.AppendLine($"    {routeCFirst}[\"Route C #0\"]");
        mermaid.AppendLine($"    {entrance} -->|Route C| {routeCFirst}");
        var leverB = nextIndex++;
        mermaid.AppendLine($"    {leverB}[\"Lever B\"]");
        mermaid.AppendLine($"    {routeCFirst} --- {leverB}");
        var routeCSecond = nextIndex++;
        mermaid.AppendLine($"    {routeCSecond}[\"Route C #1\"]");
        mermaid.AppendLine($"    {routeCFirst} --- {routeCSecond}");
        mermaid.AppendLine($"    {routeCSecond} --- {boss}");

        var gate = nextIndex++;
        mermaid.AppendLine($"    {gate}[\"Short\"]");
        mermaid.AppendLine($"    {entrance} -->|Portcullis, needs both levers| {gate}");
        mermaid.AppendLine($"    {leverA} -.->|opens, with Lever B| {gate}");
        mermaid.AppendLine($"    {leverB} -.->|opens, with Lever A| {gate}");
        var afterGate = nextIndex++;
        mermaid.AppendLine($"    {afterGate}[\"Short\"]");
        mermaid.AppendLine($"    {gate} --- {afterGate}");
        mermaid.AppendLine($"    {afterGate} --- {boss}");

        return mermaid.ToString();
    }

    // The better version of "mandatory" — reuses the same convergence-point pattern already built
    // for the floor-split staircase, but puts a portcullis there instead. Every route stays a
    // genuinely different way to travel, and now BOTH routes are fully mandatory, not just "one
    // required path plus an optional detour": Lever B sits past the Miniboss Chamber, not before
    // it, so it's the reward for beating the miniboss, not a way to skip that fight. Reaching the
    // boss demands walking the long route far enough to find Lever A AND clearing the short
    // route's own obstacle to find Lever B.
    private static string BuildConvergingMandatoryPortcullis()
    {
        const int entrance = 0;
        const int boss = 1;
        var nextIndex = 2;

        var mermaid = new StringBuilder("graph LR\n");
        mermaid.AppendLine($"    {entrance}[\"Entrance (Floor 1)\"]");
        mermaid.AppendLine($"    {boss}[\"Boss (Floor 1)\"]");

        var approach = nextIndex++;
        mermaid.AppendLine($"    {approach}[\"Approach\"]");
        mermaid.AppendLine($"    {approach} -->|Portcullis, needs both levers| {boss}");

        var longRouteRooms = new List<int>();
        BuildLongRoute(mermaid, entrance, approach, 4, ref nextIndex, longRouteRooms);

        var chest = nextIndex++;
        mermaid.AppendLine($"    {chest}[\"Chest\"]");
        mermaid.AppendLine($"    {longRouteRooms[3]} --- {chest}");

        var leverA = nextIndex++;
        mermaid.AppendLine($"    {leverA}[\"Lever A\"]");
        mermaid.AppendLine($"    {longRouteRooms[1]} --- {leverA}");
        mermaid.AppendLine($"    {leverA} -.->|opens, with Lever B| {approach}");

        var shortEntry = nextIndex++;
        mermaid.AppendLine($"    {shortEntry}[\"Short\"]");
        mermaid.AppendLine($"    {entrance} -->|Short route| {shortEntry}");
        var minibossChamber = nextIndex++;
        mermaid.AppendLine($"    {minibossChamber}[\"Miniboss Chamber\"]");
        mermaid.AppendLine($"    {shortEntry} --- {minibossChamber}");
        mermaid.AppendLine($"    {minibossChamber} --- {approach}");

        var leverB = nextIndex++;
        mermaid.AppendLine($"    {leverB}[\"Lever B\"]");
        mermaid.AppendLine($"    {minibossChamber} --- {leverB}");
        mermaid.AppendLine($"    {leverB} -.->|opens, with Lever A| {approach}");

        return mermaid.ToString();
    }

    private static string Build(Scenario scenario)
    {
        const int entrance = 0;
        const int boss = 1;
        var nextIndex = 2;

        var mermaid = new StringBuilder("graph LR\n");
        mermaid.AppendLine($"    {entrance}[\"Entrance (Floor 1)\"]");

        // A shared landing room is where every route converges before the final stretch. On a
        // flat dungeon that final stretch is just the boss room itself; on a vertical one it's a
        // staircase, so every route pays the same "getting there" cost regardless of which one
        // was taken.
        int convergencePoint;
        if (scenario.BossOnUpperFloor)
        {
            var landing = nextIndex++;
            mermaid.AppendLine($"    {landing}[\"Landing (Floor 1)\"]");
            mermaid.AppendLine($"    {boss}[\"Boss (Floor 2)\"]");
            mermaid.AppendLine($"    {landing} ==>|Staircase up| {boss}");
            convergencePoint = landing;
        }
        else
        {
            mermaid.AppendLine($"    {boss}[\"Boss (Floor 1)\"]");
            convergencePoint = boss;
        }

        var keyRoomNeeded = scenario.ShortObstacle == ShortRouteObstacle.KeyLock;
        var longRouteRooms = new List<int>();
        BuildLongRoute(
            mermaid,
            entrance,
            convergencePoint,
            scenario.LongRouteLength,
            ref nextIndex,
            longRouteRooms
        );

        for (var deadEndIndex = 0; deadEndIndex < scenario.DeadEndCount; deadEndIndex++)
        {
            var parent = longRouteRooms[deadEndIndex % longRouteRooms.Count];
            var leaf = nextIndex++;
            mermaid.AppendLine($"    {leaf}[\"Chest\"]");
            mermaid.AppendLine($"    {parent} --- {leaf}");
        }

        if (keyRoomNeeded)
        {
            // Branches off near the entrance end, not the boss end — the key only makes the short
            // route a real choice if finding it costs less than the shortcut it unlocks saves. A
            // key found next to the boss is pointless: by then the long route already got you here
            // for free. But it's a genuine side spur, not a single adjacent closet — a one-hop
            // branch is barely a detour at all, and the guard at the end (a forced occupant, not
            // the usual random roll dead ends get) shouldn't be the only cost to reach.
            var spurRoot = longRouteRooms[0];
            var sidePassage = nextIndex++;
            mermaid.AppendLine($"    {sidePassage}[\"Side Passage\"]");
            mermaid.AppendLine($"    {spurRoot} --- {sidePassage}");

            var keyRoom = nextIndex++;
            mermaid.AppendLine($"    {keyRoom}[\"Key (guarded)\"]");
            mermaid.AppendLine($"    {sidePassage} --- {keyRoom}");
        }

        BuildShortRoute(
            mermaid,
            entrance,
            convergencePoint,
            scenario.ShortObstacle,
            scenario.ShortRouteLength,
            ref nextIndex
        );

        if (scenario.ThirdRoute != ThirdRouteObstacle.None)
        {
            BuildThirdRoute(
                mermaid,
                entrance,
                convergencePoint,
                scenario.ThirdRoute,
                ref nextIndex
            );
        }

        return mermaid.ToString();
    }

    private static void BuildLongRoute(
        StringBuilder mermaid,
        int entrance,
        int convergencePoint,
        int length,
        ref int nextIndex,
        List<int> longRouteRooms
    )
    {
        var previous = entrance;
        for (var step = 0; step < length; step++)
        {
            var room = nextIndex++;
            longRouteRooms.Add(room);
            mermaid.AppendLine($"    {room}[\"Long #{step}\"]");

            if (step == 0)
            {
                mermaid.AppendLine($"    {previous} -->|Long route| {room}");
            }
            else
            {
                mermaid.AppendLine($"    {previous} --- {room}");
            }

            previous = room;
        }

        mermaid.AppendLine($"    {previous} --- {convergencePoint}");
    }

    private static void BuildShortRoute(
        StringBuilder mermaid,
        int entrance,
        int convergencePoint,
        ShortRouteObstacle obstacle,
        int length,
        ref int nextIndex
    )
    {
        // Per the original trap design table: Collapse and Slope are climbable back (over rubble,
        // or hard but possible), Mechanical is not — a trapdoor with a catch gives you nowhere to
        // grab. So they can't share one interchangeable landing: Collapse/Slope get a room with a
        // way back up; Mechanical's cellar has none of its own and only connects onward into the
        // climbable one, so falling through it costs a real detour, not a straight climb back, and
        // never dead-ends outright. Water never touches either — it sweeps you backward along the
        // same level instead of down, so there's no floor change to climb back from at all.
        int? rubbleLanding = null;
        int? trapCellar = null;
        var firstRoom = -1;

        var previous = entrance;
        for (var step = 0; step < length; step++)
        {
            var room = nextIndex++;
            if (step == 0)
            {
                firstRoom = room;
            }

            var isFinalRoom = step == length - 1;
            var trapKind = TrapKinds[step % TrapKinds.Length];
            var label = obstacle switch
            {
                ShortRouteObstacle.KeyLock => "Short",
                ShortRouteObstacle.Miniboss => isFinalRoom ? "Miniboss Chamber" : "Short",
                ShortRouteObstacle.TrapGauntlet => trapKind + " Trap",
                _ => throw new ArgumentOutOfRangeException(nameof(obstacle)),
            };
            mermaid.AppendLine($"    {room}[\"{label}\"]");

            if (step == 0)
            {
                var edgeLabel = obstacle switch
                {
                    ShortRouteObstacle.KeyLock => "Locked, needs key",
                    ShortRouteObstacle.Miniboss => "Short route",
                    ShortRouteObstacle.TrapGauntlet => "Short route",
                    _ => throw new ArgumentOutOfRangeException(nameof(obstacle)),
                };
                mermaid.AppendLine($"    {previous} -->|{edgeLabel}| {room}");
            }
            else
            {
                mermaid.AppendLine($"    {previous} --- {room}");
            }

            if (obstacle == ShortRouteObstacle.TrapGauntlet)
            {
                switch (trapKind)
                {
                    case "Water":
                        mermaid.AppendLine($"    {room} -.->|fails, swept downstream| {entrance}");
                        break;
                    case "Mechanical":
                        trapCellar ??= CreateNamedRoom(
                            mermaid,
                            ref nextIndex,
                            "Trap Cellar (Floor 0)"
                        );
                        mermaid.AppendLine(
                            $"    {room} -.->|fails, falls through, no way back| {trapCellar}"
                        );
                        break;
                    default:
                        rubbleLanding ??= CreateNamedRoom(
                            mermaid,
                            ref nextIndex,
                            "Rubble Landing (Floor 0)"
                        );
                        mermaid.AppendLine(
                            $"    {room} -.->|fails, falls through| {rubbleLanding}"
                        );
                        break;
                }
            }

            previous = room;
        }

        // A Mechanical fall alone would soft-lock without this — the cellar has to connect onward
        // to wherever the way back up actually is, since it has no such way out of its own.
        if (trapCellar != null && rubbleLanding != null)
        {
            mermaid.AppendLine($"    {trapCellar} --- {rubbleLanding}");
        }

        if (rubbleLanding != null)
        {
            mermaid.AppendLine($"    {rubbleLanding} ==>|climb back up| {firstRoom}");
        }

        mermaid.AppendLine($"    {previous} --- {convergencePoint}");
    }

    private static int CreateNamedRoom(StringBuilder mermaid, ref int nextIndex, string label)
    {
        var room = nextIndex++;
        mermaid.AppendLine($"    {room}[\"{label}\"]");
        return room;
    }

    private static void BuildThirdRoute(
        StringBuilder mermaid,
        int entrance,
        int convergencePoint,
        ThirdRouteObstacle obstacle,
        ref int nextIndex
    )
    {
        var label =
            obstacle == ThirdRouteObstacle.KeyLock ? "Locked, second key" : "Reputation gate";
        var first = nextIndex++;
        mermaid.AppendLine($"    {first}[\"Third #0\"]");
        mermaid.AppendLine($"    {entrance} -->|{label}| {first}");

        var second = nextIndex++;
        mermaid.AppendLine($"    {second}[\"Third #1\"]");
        mermaid.AppendLine($"    {first} --- {second}");
        mermaid.AppendLine($"    {second} --- {convergencePoint}");
    }
}
