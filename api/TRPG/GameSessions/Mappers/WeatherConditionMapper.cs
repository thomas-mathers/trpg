using ContractWeatherCondition = TRPG.GameSessions.Responses.WeatherCondition;
using DataWeatherCondition = TRPG.Domain.Models.WeatherCondition;

namespace TRPG.GameSessions.Mappers;

internal static class WeatherConditionMapper
{
    public static ContractWeatherCondition ToResponse(this DataWeatherCondition weather) =>
        weather switch
        {
            DataWeatherCondition.Clear => ContractWeatherCondition.Clear,
            DataWeatherCondition.Cloudy => ContractWeatherCondition.Cloudy,
            DataWeatherCondition.Rain => ContractWeatherCondition.Rain,
            DataWeatherCondition.Storm => ContractWeatherCondition.Storm,
            DataWeatherCondition.Snow => ContractWeatherCondition.Snow,
            DataWeatherCondition.Fog => ContractWeatherCondition.Fog,
        };
}
