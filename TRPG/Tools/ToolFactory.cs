using Microsoft.Extensions.Logging;
using OllamaSharp.Models.Chat;
using TRPG.Services;

namespace TRPG.Tools;

internal class ToolFactory(
    SceneService sceneService,
    PersonService personService,
    BuildingService buildingService,
    WorldService worldService,
    InventoryService inventoryService,
    ILoggerFactory loggerFactory) {
    public IReadOnlyList<Tool> Create(GameSession session) => [
        new WorldInfoTool(session, worldService),
        new LookTool(session, sceneService, loggerFactory.CreateLogger<LookTool>()),
        new MoveTool(session, sceneService, personService, buildingService, loggerFactory.CreateLogger<MoveTool>()),
        new InventoryTool(session, personService, inventoryService, loggerFactory.CreateLogger<InventoryTool>()),
        new CharacterTool(session, personService, loggerFactory.CreateLogger<CharacterTool>())
    ];
}
