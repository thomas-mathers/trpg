using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IWeatherDbContext : ITrpgDbContext
{
    DbSet<WeatherState> WeatherStates { get; }
}
