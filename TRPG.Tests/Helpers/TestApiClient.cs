using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using TRPG.Contracts;

namespace TRPG.Tests.Helpers;

public sealed class TestApiClient(
    HttpClient client,
    EndpointDataSource endpointDataSource,
    LinkGenerator linkGenerator
)
{
    public Task<HttpResponseMessage> GetAsync(
        string endpointName,
        object? routeValues = null,
        IReadOnlyDictionary<string, object?>? query = null,
        CancellationToken cancellationToken = default
    ) => SendAsync(endpointName, routeValues, query, cancellationToken: cancellationToken);

    public Task<HttpResponseMessage> PostAsync(
        string endpointName,
        object? routeValues = null,
        IReadOnlyDictionary<string, object?>? query = null,
        CancellationToken cancellationToken = default
    ) => SendAsync(endpointName, routeValues, query, cancellationToken: cancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(
        string endpointName,
        object? routeValues = null,
        IReadOnlyDictionary<string, object?>? query = null,
        CancellationToken cancellationToken = default
    ) => SendAsync(endpointName, routeValues, query, cancellationToken: cancellationToken);

    public Task<HttpResponseMessage> PostAsJsonAsync(
        string endpointName,
        object? body = null,
        object? routeValues = null,
        IReadOnlyDictionary<string, object?>? query = null,
        CancellationToken cancellationToken = default
    ) => SendAsync(endpointName, routeValues, query, body, cancellationToken);

    public async Task<HttpResponseMessage> SendAsync(
        string endpointName,
        object? routeValues = null,
        IReadOnlyDictionary<string, object?>? query = null,
        object? body = null,
        CancellationToken cancellationToken = default
    )
    {
        Endpoint? endpoint = null;
        foreach (var candidate in endpointDataSource.Endpoints)
        {
            if (
                candidate.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
                == endpointName
            )
            {
                endpoint = candidate;
                break;
            }
        }

        if (endpoint is null)
        {
            throw new InvalidOperationException($"No endpoint named '{endpointName}' was found.");
        }

        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
        var method = methods is { Count: > 0 } ? methods[0] : null;
        if (method is null)
        {
            throw new InvalidOperationException(
                $"Endpoint '{endpointName}' does not declare an HTTP method."
            );
        }

        var path = linkGenerator.GetPathByName(endpointName, routeValues);
        if (path is null)
        {
            throw new InvalidOperationException(
                $"Route values did not produce a path for endpoint '{endpointName}'."
            );
        }

        if (query is not null)
        {
            path = QueryHelpers.AddQueryString(
                path,
                query.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString())
            );
        }

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: TrpgJsonOptions.Default);
        }

        return await client.SendAsync(request, cancellationToken);
    }

    public async Task<T?> ReadFromJsonAsync<T>(
        string endpointName,
        object? routeValues = null,
        IReadOnlyDictionary<string, object?>? query = null,
        object? body = null,
        CancellationToken cancellationToken = default
    )
    {
        using var response = await SendAsync(
            endpointName,
            routeValues,
            query,
            body,
            cancellationToken
        );
        return await response.Content.ReadFromJsonAsync<T>(
            TrpgJsonOptions.Default,
            cancellationToken
        );
    }
}
