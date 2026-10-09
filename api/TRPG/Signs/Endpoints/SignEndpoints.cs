using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Domain.Models;
using TRPG.Signs.Responses;

namespace TRPG.Signs.Endpoints;

internal static class SignEndpoints
{
    public static void MapSignEndpoints(this WebApplication app)
    {
        app.MapGet("/signs/{signId:guid}", GetSignText)
            .WithName("GetSignText")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<Ok<SignTextResponse>> GetSignText(
        Guid signId,
        [FromServices] IQueryHandler<GetPropByIdQuery, Prop?> getPropById,
        CancellationToken cancellationToken
    )
    {
        var prop = await getPropById.Handle(
            new GetPropByIdQuery { Id = signId },
            cancellationToken
        );
        if (prop is not Sign sign)
        {
            throw new EntityNotFoundException("Sign", signId);
        }

        return TypedResults.Ok(new SignTextResponse(sign.Description));
    }
}
