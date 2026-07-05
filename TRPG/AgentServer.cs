using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TRPG;

internal class AgentServer(GameTurnRunner turnRunner, ILogger<AgentServer> logger) {
    public async Task Run(GameSession session, CancellationToken cancellationToken) {
        turnRunner.StartSession(session);
        await turnRunner.SendOpening(cancellationToken: cancellationToken);

        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/");
        listener.Start();

        logger.LogInformation("Agent server listening on http://localhost:5000/");

        cancellationToken.Register(() => listener.Stop());

        while (!cancellationToken.IsCancellationRequested) {
            HttpListenerContext context;
            try {
                context = await listener.GetContextAsync();
            }
            catch (HttpListenerException) {
                break;
            }

            await HandleRequest(context, cancellationToken);
        }
    }

    private async Task HandleRequest(HttpListenerContext context, CancellationToken cancellationToken) {
        var request = context.Request;
        var response = context.Response;

        if (request.HttpMethod != "POST" || request.Url?.AbsolutePath != "/chat") {
            response.StatusCode = 404;
            response.Close();
            return;
        }

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        var body = await reader.ReadToEndAsync(cancellationToken);

        string message;
        try {
            var doc = JsonDocument.Parse(body);
            message = doc.RootElement.GetProperty("message").GetString()!;
        }
        catch (JsonException) {
            response.StatusCode = 400;
            response.Close();
            return;
        }
        catch (KeyNotFoundException) {
            response.StatusCode = 400;
            response.Close();
            return;
        }

        var metrics = await turnRunner.ProcessTurn(message, cancellationToken: cancellationToken);

        var json = JsonSerializer.Serialize(new {
            response = metrics.Response,
            firstTokenMs = metrics.FirstTokenMs,
            totalMs = metrics.TotalMs,
            tokenCount = metrics.TokenCount,
            tokensPerSecond = metrics.TokensPerSecond
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken);
        response.Close();
    }
}
