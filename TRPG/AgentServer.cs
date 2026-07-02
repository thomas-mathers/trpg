using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using TRPG.Services;
using TRPG.Tools;

namespace TRPG;

internal class AgentServer(
    OllamaApiClient ollamaClient,
    ToolFactory toolFactory,
    SceneService sceneService,
    WorldService worldService,
    ILogger<AgentServer> logger) {
    public async Task Run(GameSession session, CancellationToken cancellationToken) {
        var tools = toolFactory.Create(session);
        var chat = new Chat(ollamaClient, Game.SystemPrompt) { Think = true, Options = new RequestOptions { NumCtx = 8192 } };
        chat.OnThink += (_, token) => logger.LogDebug("[agent] think: {Token}", token);

        var openingPrompt = await Game.BuildOpeningPrompt(session, sceneService, worldService, cancellationToken);
        await foreach (var _ in chat.SendAsync(openingPrompt, tools, cancellationToken: cancellationToken)) {
        }

        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/");
        listener.Start();

        logger.LogInformation("Agent server listening on http://localhost:5000/");

        cancellationToken.Register(() => listener.Stop());

        while (!cancellationToken.IsCancellationRequested) {
            HttpListenerContext context;
            try {
                context = await listener.GetContextAsync();
            } catch (HttpListenerException) {
                break;
            }

            await HandleRequest(context, chat, tools, cancellationToken);
        }
    }

    private async Task HandleRequest(HttpListenerContext context, Chat chat, IReadOnlyList<Tool> tools, CancellationToken cancellationToken) {
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
        } catch (JsonException) {
            response.StatusCode = 400;
            response.Close();
            return;
        } catch (KeyNotFoundException) {
            response.StatusCode = 400;
            response.Close();
            return;
        }

        logger.LogInformation("[agent] >>> {Message}", message);

        var buffer = new StringBuilder();
        await foreach (var token in chat.SendAsync(message, tools, cancellationToken: cancellationToken)) {
            buffer.Append(token);
        }

        var responseText = buffer.ToString();
        logger.LogInformation("[agent] <<< {Response}", responseText);

        var json = JsonSerializer.Serialize(new { response = responseText });
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken);
        response.Close();
    }
}
