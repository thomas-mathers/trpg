using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using TRPG.Generators;

namespace TRPG.Tests.Helpers;

public sealed class FakeOllamaApiClient : IOllamaApiClient {
    private static int _nameCounter;

    public Uri Uri { get; } = new("http://fake-ollama.test");
    public string SelectedModel { get; set; } = "fake-model";
    public string ChatResponseText { get; set; } = "You look around. What do you want to do next?";

    public async IAsyncEnumerable<ChatResponseStream> ChatAsync(ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        await Task.Yield();
        yield return new ChatResponseStream {
            Model = SelectedModel,
            Message = new Message { Role = ChatRole.Assistant, Content = ChatResponseText },
            Done = true
        };
    }

    public async IAsyncEnumerable<GenerateResponseStream> GenerateAsync(GenerateRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        await Task.Yield();
        yield return new GenerateResponseStream {
            Model = SelectedModel,
            Response = BuildJson($"{request.System} {request.Prompt}"),
            Done = true
        };
    }

    private static string BuildJson(string text) {
        if (text.Contains("unique races", StringComparison.OrdinalIgnoreCase)) {
            var count = ExtractCount(text, @"Generate (\d+) unique races");
            var schema = new RaceListSchema {
                Races = Enumerable.Range(0, count)
                    .Select(_ => new RaceItemSchema
                        { Name = NextName("Race"), Description = "A fake race.", CultureStyle = "Nordic/Viking" })
                    .ToList()
            };
            return JsonSerializer.Serialize(schema);
        }

        if (text.Contains("unique factions", StringComparison.OrdinalIgnoreCase)) {
            var count = ExtractCount(text, @"Generate (\d+) unique factions");
            var schema = new FactionListSchema {
                Factions = Enumerable.Range(0, count)
                    .Select(_ => new FactionItemSchema { Name = NextName("Faction"), Description = "A fake faction." })
                    .ToList()
            };
            return JsonSerializer.Serialize(schema);
        }

        if (text.Contains("Cities array containing exactly", StringComparison.OrdinalIgnoreCase)) {
            var count = ExtractCount(text, @"Cities array containing exactly (\d+)");
            var schema = new CityListSchema {
                Cities = Enumerable.Range(0, count)
                    .Select(_ => new CityItemSchema { Name = NextName("City"), Description = "A fake city." })
                    .ToList()
            };
            return JsonSerializer.Serialize(schema);
        }

        if (text.Contains("Roads array containing exactly", StringComparison.OrdinalIgnoreCase)) {
            var count = ExtractCount(text, @"Roads array containing exactly (\d+)");
            var schema = new RoadNamesSchema {
                Roads = Enumerable.Range(1, count)
                    .Select(i => new RoadNameItemSchema { Index = i, Name = NextName("Road") })
                    .ToList()
            };
            return JsonSerializer.Serialize(schema);
        }

        var entity = new GeographyEntitySchema { Name = NextName("Entity"), Description = "A fake entity." };
        return JsonSerializer.Serialize(entity);
    }

    private static int ExtractCount(string text, string pattern) {
        var match = Regex.Match(text, pattern);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
    }

    private static string NextName(string prefix) => $"{prefix}{Interlocked.Increment(ref _nameCounter)}";

    public Task CopyModelAsync(CopyModelRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<CreateModelResponse> CreateModelAsync(CreateModelRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task DeleteModelAsync(DeleteModelRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<EmbedResponse> EmbedAsync(EmbedRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IEnumerable<Model>> ListLocalModelsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IEnumerable<RunningModel>> ListRunningModelsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<PullModelResponse> PullModelAsync(PullModelRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public IAsyncEnumerable<PushModelResponse> PushModelAsync(PushModelRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ShowModelResponse> ShowModelAsync(ShowModelRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<bool> IsRunningAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<string> GetVersionAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task PushBlobAsync(string digest, byte[] bytes, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> IsBlobExistsAsync(string digest, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
