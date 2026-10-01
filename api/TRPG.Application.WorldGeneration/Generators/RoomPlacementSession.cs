using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record RoomPropInput(Guid Id, PropModel Model);

internal record PlacedProp(Guid Id, PropModel Model, Placement Placement, Footprint Footprint);

internal sealed class RoomPlacementSession
{
    private const double WallInset = 0.05;
    private const double InteriorMargin = 1;
    private const double AnchorGap = 0.05;
    private const double DoorKeepOutSize = 1.5;
    private const int SamplingTries = 50;
    private const double FreeAngleStep = Math.PI / 12;
    private const double RelaxedStep = 0.5;
    private const double Tolerance = 1e-6;

    private readonly Footprint _room;
    private readonly Random _random;
    private readonly List<Obstacle> _obstacles;
    private readonly List<PlacedProp> _placed = [];
    private readonly List<PlacedProp> _workstations = [];

    internal RoomPlacementSession(Footprint room, IReadOnlyList<ConnectorExit> exits, Random random)
    {
        _room = room;
        _random = random;
        _obstacles = exits.Select(DoorKeepOut).ToList();
    }

    internal IReadOnlyList<PlacedProp> Placed => _placed;

    internal bool TryPlace(RoomPropInput prop)
    {
        var spec = PropFootprintCatalog.Get(prop.Model);

        foreach (var rule in RuleChain(spec.Rule))
        {
            foreach (var candidate in Candidates(rule, spec))
            {
                if (Fits(candidate, spec))
                {
                    Commit(prop, spec, candidate.Pose);
                    return true;
                }
            }
        }

        return false;
    }

    internal void PlaceBestEffort(RoomPropInput prop)
    {
        if (TryPlace(prop))
        {
            return;
        }

        var spec = PropFootprintCatalog.Get(prop.Model);
        Commit(prop, spec, FirstUnobstructedPose(spec));
    }

    private static Obstacle DoorKeepOut(ConnectorExit exit)
    {
        var centerX = exit.Point.X + DoorKeepOutSize / 2 * Math.Sin(exit.FacingAngle);
        var centerY = exit.Point.Y - DoorKeepOutSize / 2 * Math.Cos(exit.FacingAngle);

        return new Obstacle(
            new OrientedBox(centerX, centerY, DoorKeepOutSize, DoorKeepOutSize, 0),
            Clearance: 0,
            IsPlacedProp: false
        );
    }

    private static PropPlacementRule[] RuleChain(PropPlacementRule rule) =>
        rule switch
        {
            PropPlacementRule.Corner =>
            [
                PropPlacementRule.Corner,
                PropPlacementRule.Wall,
                PropPlacementRule.Free,
            ],
            PropPlacementRule.Wall => [PropPlacementRule.Wall, PropPlacementRule.Free],
            PropPlacementRule.Anchor => [PropPlacementRule.Anchor, PropPlacementRule.Free],
            PropPlacementRule.Center => [PropPlacementRule.Center, PropPlacementRule.Free],
            _ => [PropPlacementRule.Free],
        };

    private IEnumerable<Candidate> Candidates(PropPlacementRule rule, PropFootprintSpec spec) =>
        rule switch
        {
            PropPlacementRule.Corner => CornerCandidates(spec),
            PropPlacementRule.Wall => WallCandidates(spec),
            PropPlacementRule.Anchor => AnchorCandidates(spec),
            PropPlacementRule.Center => CenterCandidates(spec),
            _ => FreeCandidates(spec),
        };

    private IEnumerable<Candidate> CornerCandidates(PropFootprintSpec spec)
    {
        var start = _random.Next(8);

        for (var offset = 0; offset < 8; offset++)
        {
            var index = (start + offset) % 8;
            var wall = (RoomWall)(index / 2);
            var length = WallLength(wall);
            var along =
                index % 2 == 0 ? WallInset + spec.Width / 2 : length - WallInset - spec.Width / 2;

            yield return new Candidate(PoseAgainstWall(wall, along, spec), null);
        }
    }

    private IEnumerable<Candidate> WallCandidates(PropFootprintSpec spec)
    {
        for (var attempt = 0; attempt < SamplingTries; attempt++)
        {
            var wall = (RoomWall)_random.Next(4);
            var lower = WallInset + spec.Width / 2;
            var upper = WallLength(wall) - WallInset - spec.Width / 2;
            var along = lower + _random.NextDouble() * Math.Max(0, upper - lower);

            yield return new Candidate(PoseAgainstWall(wall, along, spec), null);
        }
    }

    private IEnumerable<Candidate> AnchorCandidates(PropFootprintSpec spec)
    {
        if (_workstations.Count == 0)
        {
            yield break;
        }

        var preferred = _workstations.OrderBy(workstation =>
            workstation.Model == PropModel.WorkstationTrade ? 0 : 1
        );

        foreach (var target in preferred)
        {
            foreach (var pose in FrontRowPoses(target, spec))
            {
                yield return new Candidate(pose, target);
            }
        }
    }

    private IEnumerable<Candidate> CenterCandidates(PropFootprintSpec spec)
    {
        yield return new Candidate(new Placement(_room.Width / 2, _room.Depth / 2, 0), null);

        for (var attempt = 1; attempt < SamplingTries; attempt++)
        {
            var spread = attempt < SamplingTries / 2 ? 0.5 : 1;
            var x =
                _room.Width / 2
                + (_random.NextDouble() - 0.5) * (_room.Width - 2 * InteriorMargin) * spread;
            var y =
                _room.Depth / 2
                + (_random.NextDouble() - 0.5) * (_room.Depth - 2 * InteriorMargin) * spread;

            yield return new Candidate(new Placement(x, y, _random.Next(4) * Math.PI / 2), null);
        }
    }

    private IEnumerable<Candidate> FreeCandidates(PropFootprintSpec spec)
    {
        for (var attempt = 0; attempt < SamplingTries; attempt++)
        {
            var x =
                InteriorMargin
                + _random.NextDouble() * Math.Max(0, _room.Width - 2 * InteriorMargin);
            var y =
                InteriorMargin
                + _random.NextDouble() * Math.Max(0, _room.Depth - 2 * InteriorMargin);
            var angle = _random.Next(24) * FreeAngleStep;

            yield return new Candidate(new Placement(x, y, angle), null);
        }
    }

    private double WallLength(RoomWall wall) =>
        wall is RoomWall.North or RoomWall.South ? _room.Width : _room.Depth;

    private Placement PoseAgainstWall(RoomWall wall, double along, PropFootprintSpec spec) =>
        wall switch
        {
            RoomWall.North => new Placement(along, WallInset + spec.Depth / 2, Math.PI),
            RoomWall.East => new Placement(
                _room.Width - WallInset - spec.Depth / 2,
                along,
                3 * Math.PI / 2
            ),
            RoomWall.South => new Placement(along, _room.Depth - WallInset - spec.Depth / 2, 0),
            _ => new Placement(WallInset + spec.Depth / 2, along, Math.PI / 2),
        };

    private static IEnumerable<Placement> FrontRowPoses(PlacedProp target, PropFootprintSpec spec)
    {
        var angle = target.Placement.Angle;
        var forward = new PlanarPoint(Math.Sin(angle), -Math.Cos(angle));
        var right = new PlanarPoint(Math.Cos(angle), Math.Sin(angle));
        var distance = target.Footprint.Depth / 2 + spec.Depth / 2 + AnchorGap;
        var seatAngle = Math.Atan2(-forward.X, forward.Y);

        foreach (var offset in FrontRowOffsets(target.Footprint.Width / 2, spec))
        {
            yield return new Placement(
                target.Placement.X + forward.X * distance + right.X * offset,
                target.Placement.Y + forward.Y * distance + right.Y * offset,
                seatAngle
            );
        }
    }

    private static IEnumerable<double> FrontRowOffsets(double reach, PropFootprintSpec spec)
    {
        var spacing = spec.Width + spec.FrontClearance + 2 * AnchorGap;

        yield return 0;

        for (var slot = 1; slot * spacing <= reach + Tolerance; slot++)
        {
            yield return -slot * spacing;
            yield return slot * spacing;
        }
    }

    private bool Fits(Candidate candidate, PropFootprintSpec spec)
    {
        var box = OrientedBox.From(candidate.Pose, spec.Footprint);

        return box.IsInside(_room.Width, _room.Depth)
            && _obstacles.All(obstacle =>
                IsClearOf(obstacle, box, spec.FrontClearance, candidate.Target)
            );
    }

    private bool IsClearOf(Obstacle obstacle, OrientedBox box, double clearance, PlacedProp? target)
    {
        if (target is not null && obstacle.Source == target)
        {
            return !box.Overlaps(obstacle.Raw);
        }

        return !box.Overlaps(obstacle.Raw.Inflated(obstacle.Clearance))
            && (!obstacle.IsPlacedProp || !box.Inflated(clearance).Overlaps(obstacle.Raw));
    }

    private void Commit(RoomPropInput prop, PropFootprintSpec spec, Placement pose)
    {
        var placed = new PlacedProp(prop.Id, prop.Model, pose, spec.Footprint);
        _placed.Add(placed);
        _obstacles.Add(
            new Obstacle(
                OrientedBox.From(pose, spec.Footprint),
                spec.FrontClearance,
                IsPlacedProp: true
            )
            {
                Source = placed,
            }
        );

        if (IsWorkstation(prop.Model))
        {
            _workstations.Add(placed);
        }
    }

    private static bool IsWorkstation(PropModel model) =>
        model.ToString().StartsWith(nameof(Workstation), StringComparison.Ordinal);

    private Placement FirstUnobstructedPose(PropFootprintSpec spec)
    {
        for (var y = spec.Depth / 2; y <= _room.Depth - spec.Depth / 2; y += RelaxedStep)
        {
            for (var x = spec.Width / 2; x <= _room.Width - spec.Width / 2; x += RelaxedStep)
            {
                var box = new OrientedBox(x, y, spec.Width, spec.Depth, 0);

                if (_obstacles.Where(o => o.IsPlacedProp).All(o => !box.Overlaps(o.Raw)))
                {
                    return new Placement(x, y, 0);
                }
            }
        }

        return new Placement(_room.Width / 2, _room.Depth / 2, 0);
    }

    private enum RoomWall
    {
        North,
        East,
        South,
        West,
    }

    private record Candidate(Placement Pose, PlacedProp? Target);

    private record Obstacle(OrientedBox Raw, double Clearance, bool IsPlacedProp)
    {
        internal PlacedProp? Source { get; init; }
    }
}
