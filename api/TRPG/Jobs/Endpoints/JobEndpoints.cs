using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TRPG.Jobs.Responses;

namespace TRPG.Jobs.Endpoints;

internal static class JobEndpoints
{
    public static void MapJobEndpoints(this WebApplication app)
    {
        app.MapGet("/jobs/{id:guid}", GetJob).WithName("GetJob");
    }

    private static Results<NotFound, Ok<JobStatusResponse>> GetJob(Guid id, JobRunner jobRunner)
    {
        var snapshot = jobRunner.Get(id);
        if (snapshot is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(
            new JobStatusResponse(
                snapshot.JobId,
                snapshot.Status,
                snapshot.ResultJson,
                snapshot.ErrorMessage
            )
        );
    }
}
