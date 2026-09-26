using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TRPG.Domain;

namespace TRPG.Data;

public sealed class GameInstantValueConverter()
    : ValueConverter<GameInstant, DateTime>(
        instant => instant.Value,
        value => new GameInstant(value)
    );
