using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TRPG.Data;
using TRPG.Jobs.Responses;

namespace TRPG.Jobs.Endpoints;

internal static class JobEndpoints
{
    public static void MapJobEndpoints(this WebApplication app)
    {
        app.MapGet("/jobs/{id:guid}", GetJob).WithName("GetJob");
    }

    private static async Task<Results<NotFound, Ok<JobStatusResponse>>> GetJob(
        Guid id,
        TrpgTickerQDbContext context,
        CancellationToken cancellationToken
    )
    {
        var ticker = await context.TimeTickers.FindAsync([id], cancellationToken);
        if (ticker is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(
            new JobStatusResponse(
                ticker.Id,
                Enum.Parse<JobStatus>(ticker.Status.ToString()),
                ticker.ResultJson,
                ticker.ExceptionMessage
            )
        );
    }
}
