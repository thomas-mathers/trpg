using TRPG.Application.CreatureJobs;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.CreatureJobs;

public sealed class WeatherShelterTests
{
    private readonly Guid _creatureId = Guid.NewGuid();
    private readonly Guid _outdoorId = Guid.NewGuid();
    private readonly Guid _shelterId = Guid.NewGuid();

    [Fact]
    public void Apply_MovesAnIdleJobToTheShelter_WhenItsLocationHasSevereWeather()
    {
        // Arrange
        var job = Builders.MakeCreatureJob(_creatureId, locationId: _outdoorId);

        // Act
        var sheltered = WeatherShelter.Apply(job, _shelterId, new HashSet<Guid> { _outdoorId });

        // Assert
        Assert.Equal((_shelterId, job.Id), (sheltered.LocationId, sheltered.Id));
    }

    [Fact]
    public void Apply_KeepsTheJob_WhenTheWeatherIsNotSevereThere()
    {
        // Arrange
        var job = Builders.MakeCreatureJob(_creatureId, locationId: _outdoorId);

        // Act
        var sheltered = WeatherShelter.Apply(job, _shelterId, new HashSet<Guid>());

        // Assert
        Assert.Same(job, sheltered);
    }

    [Fact]
    public void Apply_KeepsTheJob_WhenTheCreatureHasNoShelter()
    {
        // Arrange
        var job = Builders.MakeCreatureJob(_creatureId, locationId: _outdoorId);

        // Act
        var sheltered = WeatherShelter.Apply(job, null, new HashSet<Guid> { _outdoorId });

        // Assert
        Assert.Same(job, sheltered);
    }

    [Fact]
    public void Apply_KeepsTheJob_WhenItIsNotIdle()
    {
        // Arrange
        var job = Builders.MakeCreatureJob(
            _creatureId,
            action: CreatureJobAction.Work,
            locationId: _outdoorId
        );

        // Act
        var sheltered = WeatherShelter.Apply(job, _shelterId, new HashSet<Guid> { _outdoorId });

        // Assert
        Assert.Same(job, sheltered);
    }
}
