using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class ExteriorLayoutPass
{
    private record ExteriorLayout(
        Dictionary<Guid, DistrictBuildingLayout> Buildings,
        IReadOnlyList<DistrictDecor> Decor,
        double? SideAxis = null,
        DistrictPlan? Plan = null,
        IReadOnlyList<OrientedBox>? Occupied = null
    );

    internal static LocationLayoutResult Run(
        LocationLayoutContext context,
        Dictionary<Guid, ConnectorExit> exitByConnectorId
    )
    {
        var furniture = new List<Prop>();
        var roadNodes = new List<RoadNode>();
        var roadEdges = new List<RoadEdge>();

        foreach (var location in context.Locations.Where(l => l.Kind != LocationKind.Room))
        {
            var exterior = LayOut(context, location);
            var exits = ResolveExits(context, location, exterior);
            foreach (var exit in exits)
            {
                exitByConnectorId[exit.ConnectorId] = exit;
            }

            if (location.Kind == LocationKind.District)
            {
                var roads = PlanRoads(context, location, exterior, exits);
                roadNodes.AddRange(roads.Nodes);
                roadEdges.AddRange(roads.Edges);
                var amenities = OutdoorAmenityPlanner.Place(
                    context.DistrictByLocationId[location.Id].DistrictType,
                    new OutdoorAmenityInput(
                        exterior.Plan!,
                        context
                            .BuildingsByExterior[location.Id]
                            .ToDictionary(
                                building => building.Id,
                                building => building.BuildingType
                            ),
                        exterior.Decor,
                        exterior.Occupied!,
                        roads
                    )
                );
                furniture.AddRange(amenities.Select(item => CreateFurniture(location, item)));
            }

            furniture.AddRange(exterior.Decor.Select(item => CreateFurniture(location, item)));
        }

        return new LocationLayoutResult(furniture, roadNodes, roadEdges);
    }

    private static RoadNetwork PlanRoads(
        LocationLayoutContext context,
        Location district,
        ExteriorLayout exterior,
        IReadOnlyCollection<ConnectorExit> exits
    ) =>
        RoadNetworkPlanner.Plan(
            district,
            DistrictRoadInputs.Obstacles(context, district, exterior.Decor),
            DistrictRoadInputs.Terminals(context, district, exits)
        );

    private static IReadOnlyList<ConnectorExit> ResolveExits(
        LocationLayoutContext context,
        Location location,
        ExteriorLayout exterior
    )
    {
        var classifier = new ConnectorExitClassifier(context, exterior.Buildings);
        var requests = context
            .ConnectorsByOrigin[location.Id]
            .Select(classifier.Classify)
            .ToArray();
        var frame = new Footprint(Width: location.Width, Depth: location.Depth);

        var exits = ConnectorPointResolver.ResolveExits(frame, requests, exterior.SideAxis);

        return location.Kind == LocationKind.District
            ? DistrictExitSnapper.Snap(frame, exits)
            : exits;
    }

    private static Prop CreateFurniture(Location location, DistrictDecor item)
    {
        if (PropModelNames.IsSeat(item.Model))
        {
            return new Seat
            {
                LocationId = location.Id,
                WorldId = location.WorldId,
                Name = PropModelNames.DisplayName(item.Model),
                X = item.Placement.X,
                Y = item.Placement.Y,
                Angle = item.Placement.Angle,
                Width = item.Footprint.Width,
                Depth = item.Footprint.Depth,
            };
        }

        return new Furniture
        {
            LocationId = location.Id,
            WorldId = location.WorldId,
            Name = PropModelNames.DisplayName(item.Model),
            Model = item.Model,
            X = item.Placement.X,
            Y = item.Placement.Y,
            Angle = item.Placement.Angle,
            Width = item.Footprint.Width,
            Depth = item.Footprint.Depth,
        };
    }

    private static ExteriorLayout LayOut(LocationLayoutContext context, Location location)
    {
        var buildings = context.BuildingsByExterior[location.Id].ToArray();
        var footprints = buildings.ToDictionary(
            building => building.Id,
            building => SizeBuilding(context, building)
        );
        var inputs = buildings
            .Select(building => new DistrictBuildingInput(
                building.Id,
                building.BuildingType,
                footprints[building.Id]
            ))
            .ToArray();
        var exterior =
            location.Kind == LocationKind.District
                ? LayOutDistrict(context, location, inputs)
                : LayOutWilderness(location, inputs);

        foreach (var building in buildings)
        {
            var layout = exterior.Buildings[building.Id];
            building.X = layout.Placement.X;
            building.Y = layout.Placement.Y;
            building.Angle = layout.Placement.Angle;
            building.Width = footprints[building.Id].Width;
            building.Depth = footprints[building.Id].Depth;
        }

        return exterior;
    }

    private static ExteriorLayout LayOutDistrict(
        LocationLayoutContext context,
        Location location,
        DistrictBuildingInput[] buildings
    )
    {
        var props = context.PropsByLocationId[location.Id].ToArray();
        var seats = props
            .Select(prop => new DistrictSeatInput(
                prop.Id,
                PropFootprintCatalog.Get(PropModelResolver.Resolve(prop)).Footprint
            ))
            .ToArray();

        var districtType = context.DistrictByLocationId[location.Id].DistrictType;
        var layout = DistrictLayoutGenerator.Generate(
            districtType,
            buildings,
            seats,
            LayoutSeed.From(location.Id)
        );

        location.Width = layout.District.Width;
        location.Depth = layout.District.Depth;
        ApplySeats(props, seats, layout.Seats);
        var occupied = OutdoorFurnisher.Blocked(layout.Plan, buildings);
        occupied.AddRange(
            layout.Seats.Select(seat =>
                CityGrid.CellBox(
                    seat.Placement,
                    seats.Single(input => input.Id == seat.Id).Footprint
                )
            )
        );

        return new ExteriorLayout(
            layout.Buildings.ToDictionary(building => building.Id),
            layout.Decor,
            layout.Plan.SideAxis,
            layout.Plan,
            occupied
        );
    }

    private static ExteriorLayout LayOutWilderness(
        Location location,
        DistrictBuildingInput[] buildings
    )
    {
        var size = LocationSizer.SizeWilderness();
        location.Width = size.Width;
        location.Depth = size.Depth;

        var placed = WildernessBuildingPlacer
            .Place(size, buildings, new Random(LayoutSeed.From(location.Id)))
            .ToDictionary(building => building.Id);

        return new ExteriorLayout(placed, []);
    }

    private static void ApplySeats(
        Prop[] props,
        DistrictSeatInput[] inputs,
        IReadOnlyList<DistrictSeatLayout> layouts
    )
    {
        var layoutById = layouts.ToDictionary(layout => layout.Id);
        var footprintById = inputs.ToDictionary(input => input.Id, input => input.Footprint);

        foreach (var prop in props)
        {
            var placement = layoutById[prop.Id].Placement;
            prop.X = placement.X;
            prop.Y = placement.Y;
            prop.Angle = placement.Angle;
            prop.Width = footprintById[prop.Id].Width;
            prop.Depth = footprintById[prop.Id].Depth;
        }
    }

    private static Footprint SizeBuilding(LocationLayoutContext context, Building building)
    {
        var rooms = context.RoomsByBuilding[building.Id].ToArray();

        var footprint = BuildingTypes.Dungeon.Contains(building.BuildingType)
            ? SizeDungeon(context, building, rooms)
            : BuildingTemplateCatalog.Resolve(building.BuildingType, rooms).Footprint;

        return CityGrid.SnapBuilding(footprint);
    }

    private static Footprint SizeDungeon(
        LocationLayoutContext context,
        Building building,
        Room[] rooms
    )
    {
        var groundFloor = rooms.Where(room => room.FloorNumber == 0).ToArray();
        var groundFloorArea = (groundFloor.Length > 0 ? groundFloor : rooms).Sum(room =>
        {
            var location = context.LocationById[room.LocationId];
            return location.Width * location.Depth;
        });

        return LocationSizer.SizeBuilding(
            building.BuildingType,
            groundFloorArea,
            new Random(LayoutSeed.From(building.Id))
        );
    }
}
