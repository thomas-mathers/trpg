using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using TRPG.Services;

namespace TRPG.Tools;

internal class ToolFactory(
    SceneService sceneService,
    PersonService personService,
    BuildingService buildingService,
    LocationService locationService,
    LockService lockService,
    WorldService worldService,
    InventoryService inventoryService,
    NpcConversationService npcConversationService,
    ILoggerFactory loggerFactory) {
    public IReadOnlyList<Tool> Create(GameSession session) {
        return [
            new WorldInfoTool(session, worldService),
            new LookTool(session, sceneService, loggerFactory.CreateLogger<LookTool>()),
            new MoveTool(session, sceneService, personService, buildingService, locationService, lockService,
                loggerFactory.CreateLogger<MoveTool>()),
            new InventoryTool(session, personService, inventoryService, loggerFactory.CreateLogger<InventoryTool>()),
            new CharacterTool(session, personService, loggerFactory.CreateLogger<CharacterTool>()),
            new StartConversationTool(session, personService, npcConversationService,
                loggerFactory.CreateLogger<StartConversationTool>()),
            new EndConversationTool(session, npcConversationService, loggerFactory.CreateLogger<EndConversationTool>())
        ];
    }
}