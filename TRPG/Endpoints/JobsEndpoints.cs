using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TRPG.Contracts.Jobs.Responses;
using TRPG.Data;

namespace TRPG.Endpoints;

internal static class JobsEndpoints
{
    public static void MapJobsEndpoints(this WebApplication app)
    {
        app.MapGet("/jobs/{id:guid}", GetJob);
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
