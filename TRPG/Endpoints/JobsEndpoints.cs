using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TRPG.Contracts.Jobs.Responses;
using TRPG.Data;

namespace TRPG.Endpoints;

internal static class JobsEndpoints
{
    public static void MapJobsEndpoints(this WebApplication app)
    {
        app.MapGet("/jobs/{id:guid}", GetJob);
    }

    private static async Task<IResult> GetJob(
        Guid id,
        TrpgTickerQDbContext context,
        CancellationToken cancellationToken
    )
    {
        var ticker = await context.TimeTickers.FindAsync([id], cancellationToken);
        if (ticker is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(
            new JobStatusResponse(
                ticker.Id,
                Enum.Parse<JobStatus>(ticker.Status.ToString()),
                ticker.ResultJson,
                ticker.ExceptionMessage
            )
        );
    }
}
