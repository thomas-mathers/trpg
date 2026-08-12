using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

internal static class LocationNameGenerator
{
    public static IReadOnlyList<Location> Generate(
        IReadOnlyCollection<Location> locations,
        IReadOnlyCollection<State> states,
        IReadOnlyCollection<City> cities,
        IReadOnlyCollection<District> districts,
        IReadOnlyCollection<Building> buildings,
        IReadOnlyCollection<Room> rooms
    )
    {
        var statesById = states.ToDictionary(state => state.Id);
        var citiesById = cities.ToDictionary(city => city.Id);
        var districtsById = districts.ToDictionary(district => district.Id);
        var roomsById = rooms.ToDictionary(room => room.Id);
        var buildingsById = buildings.ToDictionary(building => building.Id);

        return locations
            .Select(location => new Location
            {
                Id = location.Id,
                Name = FormatName(
                    location,
                    statesById,
                    citiesById,
                    districtsById,
                    roomsById,
                    buildingsById
                ),
                WorldId = location.WorldId,
                StateId = location.StateId,
                CityId = location.CityId,
                DistrictId = location.DistrictId,
                RoomId = location.RoomId,
            })
            .ToArray();
    }

    private static string FormatName(
        Location location,
        IReadOnlyDictionary<Guid, State> states,
        IReadOnlyDictionary<Guid, City> cities,
        IReadOnlyDictionary<Guid, District> districts,
        IReadOnlyDictionary<Guid, Room> rooms,
        IReadOnlyDictionary<Guid, Building> buildings
    )
    {
        var room = location.RoomId is Guid roomId ? rooms.GetValueOrDefault(roomId) : null;
        var building = room is null ? null : buildings[room.BuildingId];

        return string.Join(
            ", ",
            new[]
            {
                room?.Name,
                building?.Name,
                location.DistrictId is Guid districtId ? districts[districtId].Name : null,
                location.CityId is Guid cityId ? cities[cityId].Name : null,
                states[location.StateId].Name,
            }.Where(name => name is not null)
        );
    }
}
