using OllamaSharp;
using TRPG.Data;

namespace TRPG.Commands;

internal class BootstrapWorldCommand { }

internal class BootstrapWorldCommandHandler(OllamaApiClient client, TrpgDbContext context) {
    public async Task Handle(BootstrapWorldCommand command, CancellationToken cancellationToken) {
        _ = client;
        _ = context;
        throw new NotImplementedException();
    }
}