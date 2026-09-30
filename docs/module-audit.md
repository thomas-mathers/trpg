# Module audit — 2026-09-29

This is an architecture audit and refactoring proposal, not an implementation. It covers all 33 backend projects (28 application modules), all 10 SPA feature folders, and shared infrastructure. The inventory is based on the current checkout's project references, source imports, module database interfaces, event registrations, and selected workflow implementations. Generated/build output is excluded. This is not a claim that every method has been behaviorally tested.

The backend project graph is acyclic, with 135 direct Application-to-Application references. GameTurns has 23, Encounters 15, and LocationSimulation 14. Those counts include Common, Configuration, and formula libraries. A large orchestrator naturally has many dependencies; reducing the count is worthwhile only when it also improves ownership or isolates change.

The strongest findings are misplaced fact ownership, direct access to other modules' persistence interfaces, world-time APIs inside GameSessions, and several unrelated responsibilities accumulated in LocationSimulation. Events already provide useful separation for quest rewards, learned facts, session lifecycle, and prop cleanup. Extend that pattern selectively; do not turn validation, required results, or ordered gameplay into implicit event chains.

## Backend module inventory

Names below omit `TRPG.Application.`. Dependencies are **direct declared project references**, not the transitive closure. `D` = TRPG.Domain; `DB` = TRPG.Data; `C` = Application.Common; `O` = Application.Configuration. Package dependencies and hidden dependencies are listed separately below. “Keep” means no persuasive placement problem was identified in this audit, not a formal proof that every file is ideally placed.

| Module | Direct dependencies | Current responsibility | Placement verdict |
|---|---|---|---|
| Abilities | C, CreatureFormulas, D | Ability catalog, attack/support definitions, attribute effects, ability timing | Keep the rules/catalog together. `CombatTiming` is shared ability timing, so its name alone is not a reason to move it. |
| Books | C, O, Knowledge, Worlds, D, DB | Book works/pages, LLM page composition, reading and teaching facts; currently also stores general facts | Move `AddFactsCommand` and `GetFactByIdQuery`, plus Fact persistence ownership, to Knowledge. Keep page composition and book reading here. |
| Caravans | C, O, Creatures, Inventory, LocationSimulation, Routing, D, DB | Fares, tickets, purchase/boarding validation, caravan interactions | Keep commercial operations. Replace direct `IRoutingDbContext` reads with Routing queries. Weather dependency should target a smaller owner, not all of LocationSimulation. |
| Chat | C, D, DB | Session chat history, message ordinals, truncation and session lifecycle cleanup | Keep. Its Microsoft.Extensions.AI dependency is consistent with storing/reconstructing model messages. |
| Combat | Abilities, C, O, CreatureFormulas, D | In-memory combat simulation, action resolution, effects, damage/hit/evade calculations and result types | Keep deterministic rules separate from persisted fight lifecycle. Move `CombatNarration.cs` to the host's Combat presentation area: its current consumers are host result mappers. |
| Common | D | Command/query contracts, validation contracts, events, clock/gate interfaces, exceptions, serialization and reusable helpers | Mostly appropriate. `EnumerableExtensions.Shuffled` has only a Combat production caller; move it into Combat or inline it. Shared graph algorithms and LLM cache hints have multiple consumers and can stay. |
| Configuration | None | Shared options and LLM role identifiers | Keep genuinely shared options. Host-only `GameClientEventAckOptions` and session settings are candidates for host Configuration after verifying all consumers. Do not create feature dependencies merely to relocate option types. |
| CreatureFormulas | O, D | Stat, skill, progression and skill-check formulas | Keep. Useful persistence-free lower layer. |
| CreatureJobs | C, Creatures, GameSessions, Props, D, DB | Job records, schedule selection, executing current job activity and prop occupancy | Keep scheduling and job execution. GameSessions reference appears unused. Seat/bed operations require ordered outcomes; do not replace all calls with events. |
| Creatures | Abilities, C, O, CreatureFormulas, Factions, GameSessions, Inventory, Worlds, D, DB | Creature attributes, skills, condition/posture/movement/engagement, regeneration, creature projections and respawn preparation | Mostly coherent. Pass captured GameInstant to skill advancement to remove the GameSessions dependency. Respawn preparation is a candidate for GameTurns because it composes corpse creation, inventory transfer and temple selection. `PersistCombatantsCommand` correctly owns creature state writes despite its combat-related name. |
| Crimes | C, O, Creatures, Factions, Reputations, D, DB | Crime records/witnesses, hearsay, outcomes, consequence calculations and reputation penalties | Keep. Factions reference appears unused. Direct reputation adjustment remains reasonable unless consequences gain multiple independent consumers. |
| Encounters | Abilities, Combat, C, O, Creatures, Crimes, Factions, GameSessions, Inventory, Knowledge, Worlds, Props, Reputations, RoomBookings, WeaponProficiency, D, DB | Encounter evaluation/resolution, theft/lockpick/jailbreak interactions, persisted fight lifecycle and combat integration | Too broad at the edges. Split ordinary room-key return from overdue-key confrontation. Extract non-fight effect advancement if effect rules need independent reuse. Replace the general workstation lookup here with composition using existing Worlds/Props queries. Keep fight lifecycle here. |
| Factions | C, D, DB | Factions, memberships, standings and leadership queries | Keep and strengthen ownership: faction changes currently implemented inside Quests belong here. |
| GameSessions | C, D, DB | Session creation/deletion/lookups; currently also world-time query/advance wrappers | Keep session lifecycle and session-to-world resolution. Move `AdvanceTimeCommand` and `GetGameTimeByWorldIdQuery` to Worlds, or call the existing IWorldClock at workflow boundaries. |
| GameTurns | Books, Caravans, Chat, Combat, C, O, CreatureFormulas, Creatures, Crimes, Encounters, Factions, GameSessions, Inventory, LocationSimulation, Reputations, RoomBookings, Routing, Worlds, Narration, NpcConversations, Props, Quests, Knowledge, D | Player-turn orchestration, scene projections/publication, LLM execution, conversation briefings and turn-specific narration | Broad by design. Extract a Scenes read-model module only as a coherent group, not one mapper at a time. Keep orchestration here and move deterministic move-tool workflow out of the host into it. Do not move LLM orchestration wholesale into the currently small Narration module. |
| Inventory | CreatureFormulas, C, Props, Reputations, D, DB | Item ownership/quantities, equipment, gold, transfer and trade | Keep. Trade necessarily uses workstation/reputation information. A Trading module is optional for growth, not needed just to reduce reference counts. |
| Knowledge | C, Creatures, Factions, Worlds, D, DB | Learned facts/entities, visited rooms, trap knowledge, family relationships and knowledge projections | Adopt general Fact definitions from Books. Keep Relationship here under the repository's explicit graph-edge ownership convention. |
| LocationSimulation | C, Books, CreatureJobs, Creatures, Encounters, Factions, Inventory, Knowledge, Props, Quests, RoomBookings, Routing, WorldGeneration, Worlds, D, DB | Location catch-up, routines/travelers/effects/regeneration, weather, spawning, restocking, corpse cleanup and quest seeding/generation | Highest-value responsibility split. Move quest seeding/request lifecycle into QuestGeneration; move weather into Weather; retain catch-up orchestration, spawners and restock policies. |
| Narration | C, Creatures, Worlds, D | Lore anchor lookup/cache, name matching and linking streamed text to entities | Keep. Its actual scope is lore linking, not all narration. Renaming to LoreLinks is an optional clarity improvement. Display-name mappings used for lore anchors belong here. |
| NpcConversations | C, D, DB | NPC conversation records, memories/history and per-session open state | Keep persistence here. `CloseLingeringNpcConversationsCommand` in GameTurns is correctly a workflow because it coordinates LLM calls, chat history and engagement. |
| Props | C, D, DB | Props/triggers/cells/traps, bed/seat/workstation occupancy and cleanup reactions | Keep. Do not add a Worlds dependency solely to house a building-level convenience lookup. |
| Quests | Books, C, O, Creatures, Crimes, Inventory, Knowledge, Props, Reputations, Worlds, D, DB | Quest acceptance/completion, objectives, journal/markers, exclusive groups and quest-bound fact disclosure | Move faction persistence into Factions; switch fact queries from Books to Knowledge. Keep quest-specific disclosure rules here for now. Generation belongs alongside this module, not mixed into completion logic. |
| Reputations | C, O, Factions, D, DB | Reputation values/logs, faction-adjusted effective reputation and quest reward reactions | Keep. Configuration reference appears unused. |
| RoomBookings | C, O, Creatures, GameSessions, Inventory, Props, Worlds, D, DB | Booking/key issuance/replacement, room entitlement and sleep benefits | Bring ordinary key return here; keep confrontation in Encounters. Sleep entitlement belongs here, but advancing world time and reconciling effects belongs in a turn workflow. |
| Routing | C, O, Creatures, CreatureJobs, WorldGeneration, Worlds, D, DB | Routes, recurring route schedules, traveler membership/positions, pausing/resuming engaged groups | Keep persisted travel ownership. Read world topology through a Worlds query, not duplicated writable DbSets. Consider a pure TravelPlanning library for logic shared with generation. |
| WeaponProficiency | C, D, DB | Weapon proficiency persistence and adjustment | Keep; small, clear ownership is acceptable. Combatant loading needs its query, so an event for XP alone will not eliminate Encounters' reference. |
| WorldGeneration | Abilities, O, CreatureFormulas, C, D | Procedural geography, creatures/items/quests, social structures and route/content generation; no persistence | Keep stateless generation. Guide's “zero cross-module Application dependencies” is inaccurate: four foundational Application references exist. Clarify “no stateful feature-module dependencies.” Do not move shared route generation into stateful Routing and introduce a cycle. |
| Worlds | C, WorldGeneration, D, DB | Geography/structure/ownership, clock and world mutation gate, dungeon premises/expeditions, world creation/bootstrap/deletion | Keep geography and runtime clock. Bootstrap/purge are explicitly cross-domain workflows; isolate their exceptional persistence access or later extract WorldLifecycle. Weather can be separate without making Worlds larger. |
| TRPG.Domain | None | Shared entities, value objects, enums and domain calculations (including time/travel primitives) | Keep dependency-free. Flat model placement follows the guide. Shared entities are still a real coupling point; avoid exporting tracked mutable objects across modules. |
| TRPG.Data | Domain | EF contexts, module context interfaces, mappings, migrations and TickerQ persistence | Keep physical persistence infrastructure. Module interfaces are currently access conventions, not enforced boundaries: any DB consumer can import another interface, and Database/Entry expose escape hatches. |
| TRPG (host) | All Application modules **except Routing**, plus Domain and Data; Routing is used transitively | ASP.NET endpoints/SignalR, transport mapping, DI, command decorators, background services/jobs, provider adapters and tools | Keep adapters/wire types here. Add explicit Routing reference if retaining current direct source use. Move substantial gameplay orchestration from `MoveTool` to GameTurns; retain prompt/tool descriptions and serialization in the adapter. |
| TRPG.Balance | Abilities, Combat, O, CreatureFormulas, Creatures, WorldGeneration, Domain, Data | Balance experiments, generated/simulated combatants and fight measurements | Keep as an executable composition root; its broad dependencies are expected. |
| TRPG.Tests | TRPG | Unit/integration/HTTP/hub tests, PostgreSQL fixtures and test builders | Keep infrastructure/tests separate from production. Add architecture tests; current tests have broad transitive production access. No architecture-test file was identified by name during this audit. |

Folders named Application.Buildings, GuardPatrols, Locations, RoadTravelers and Scenes contain no source files and have no current project. They are not additional active modules. Do not use their presence as evidence that those boundaries already exist.

## Dependencies the project graph does not reveal

1. **Quests → Factions persistence.** `AcceptQuestCommand`, `CompleteQuestCommand`, `GetQuestInteractionsForGiverQuery`, and `GetQuestMarkersForCreaturesQuery` inject `IFactionsDbContext`. Completion both inserts memberships and changes faction standings. Quests has no direct Factions project reference, but plainly depends on its schema and rules.
2. **Caravans → Routing persistence.** Begin/end interaction, ticket purchase and boarding inject `IRoutingDbContext`. They read traveler/step/topology data while other routing operations already use public queries. A single public journey/stop query would consolidate this knowledge.
3. **Routing → Worlds persistence.** `IRoutingDbContext` exposes LocationConnectors and TravelConnectors, also exposed by `IWorldsDbContext`. Ownership is ambiguous even though the inspected Routing uses are reads. Provide a batched travel-network projection from Worlds.
4. **Books → Inventory persistence.** `IBooksDbContext` exposes Items; `GetBookByItemIdQuery` resolves a physical book item to its work. An Inventory query returning book identity would clarify this boundary, although a deliberately documented read-only join is also defensible.
5. **LocationSimulation → Factions persistence.** `SeedLlmQuestChainCommand` imports `IFactionsDbContext`. Its future QuestGeneration owner should query candidate faction information through Factions.
6. **Global lifecycle exceptions.** `BootstrapWorldCommand`, `DropWorldCommand`, and `DeleteCreaturesCommand` use the concrete `TrpgDbContext`. The last is explicitly permitted by the guide for corpse cleanup. Do not distribute deletion across unordered events merely to hide these dependencies.

Evidence: [module database interfaces](../api/TRPG.Data/ModuleContexts), [quest completion](../api/TRPG.Application.Quests/Commands/CompleteQuestCommand.cs), [boarding](../api/TRPG.Application.Caravans/Commands/BoardCaravanCommand.cs), [route schedule materialization](../api/TRPG.Application.Routing/Commands/EnsureCreatureRouteSchedulesCommand.cs).

Several projects also use transitive Application dependencies without declaring them directly: Encounters imports CreatureFormulas; LocationSimulation imports Configuration and GameSessions; Worlds imports Configuration; host imports Routing. Make direct references honest after choosing the target ownership. Adding an honest edge is preferable to hiding a real dependency.

Six declared references are removal candidates because no corresponding source namespace usage was found: Caravans → Configuration, CreatureJobs → GameSessions, Creatures → Factions, Crimes → Factions, Reputations → Configuration, Routing → Configuration. This is static evidence, not a compiler-proven removal. Delete one at a time and build the full solution; account for package propagation and DI discovery. If all six are unnecessary, the current graph would go from 135 to 129 Application edges without changing module design.

## Prioritized placement and coupling proposals

### 1. Restore faction ownership first

Move membership creation and standing changes out of `CompleteQuestCommand` into Factions handlers. Use Factions queries for acceptance and marker eligibility. Batch those queries so restoring ownership does not introduce per-NPC database calls.

For rewards, extend the existing pattern used by `QuestGoldRewardedEvent` and `QuestReputationRewardedEvent`: introduce a typed quest faction-reward event carrying world/player/faction IDs and the resolved reward facts. A Factions consumer owns idempotent membership insertion and standing changes. Alternatively, explicit Factions commands are simpler if these effects must return a result. Both are improvements over accessing IFactionsDbContext.

Keep the quest-completion transaction around the required reward effects. Faction membership must exist before the existing `QuestCompletedFactionChainSeedEventHandler` checks eligibility and schedules follow-up content. Publish the reward reaction before completion, or use an explicit command followed by the completion event; do not rely on registration order between sibling consumers.

**Benefit:** eliminates foreign writes and centralizes faction rules. Query callers will legitimately reference Factions, so the declared reference count may increase while architecture improves.

### 2. Move general facts from Books to Knowledge

Move these files and their context ownership:

- `Books/Commands/AddFactsCommand.cs` → Knowledge/Commands.
- `Books/Queries/GetFactByIdQuery.cs` → Knowledge/Queries.
- `Fact` DbSet access from IBooksDbContext → IKnowledgeDbContext; keep the entity in Domain.

Update Books page composition to query Knowledge for facts. Books already references Knowledge, so this adds no new feature edge. Update quest disclosure, quest generation and NPC briefing callers to use Knowledge. All currently also use Knowledge; this can remove their Books reference where no other Books usage remains. Books' Work/Page records and LLM page composition stay in Books.

Keep `ReadBookPageCommand → LearnFactCommand` synchronous: it returns whether the reader newly learned something. A fire-and-forget `PageRead` event would obscure that result and change the API semantics. The existing `FactLearnedEvent` is the right downstream notification.

**Benefit:** Quests → Books and GameTurns → Books appear removable after migrating all fact callers. LocationSimulation's Books reference can also disappear if fact creation is switched before or during quest-generation extraction. These are removal targets, not verified builds.

### 3. Remove world time from session-independent mechanics

`AdvanceTimeCommand` simply delegates to IWorldClock using WorldId. `GetGameTimeByWorldIdQuery` also has no session semantics. Move them to Worlds or use IWorldClock directly at the workflow boundary. Keep `GetGameTimeQuery(SessionId)` as a session adapter, since it resolves a session to its world.

More importantly, add an explicit GameInstant input to `AdjustCreatureSkillsCommand`; its caller already resolves combat at a captured instant. This avoids reading a second clock value during a deterministic operation and removes Creatures' only GameSessions use. Include the captured instant in `CreatureFreedEvent` so its schedule consumer does not need to query GameSessions again.

Keep clock advancement, owed effects, regeneration and location reconciliation explicitly sequenced. Do not replace them with a generic `WorldTimeAdvanced` broadcast: active fights and watched locations need different processing, and a time skip is not a replay of every world tick.

**Benefit:** removes the Creatures → GameSessions edge; RoomBookings → GameSessions can also disappear by moving sleep orchestration to GameTurns or switching the time command to Worlds, which RoomBookings already uses. Session-specific encounter/turn flows may still need GameSessions.

### 4. Extract quest generation from LocationSimulation

Create `Application.QuestGeneration` for the following existing LocationSimulation code:

- `SeedAssassinateQuestCommand`, `SeedCaptiveRescueQuestCommand`, `SeedClearDungeonQuestCommand`, `SeedCourierQuestCommand`, `SeedFetchQuestCommand`, `SeedStealQuestCommand`.
- `SeedLlmQuestChainCommand`, `GenerateQuestChainCommand`, and `IQuestChainGenerationScheduler`.
- `SyncQuestSeedScheduleCommand`, `QuestCompletedFactionChainSeedEventHandler`.
- Persistence ownership of QuestSeedSchedule and QuestChainGenerationRequest.

Keep the pure quest graph/casting/content generators in WorldGeneration initially. QuestGeneration composes those generators and the owning modules' commands/queries. LocationSimulation invokes one “seed if due” operation at the captured location/time. Move TickerQ job references/startup recovery to the new contracts, retaining TickerQ itself in the host.

Do not move the slow LLM call into an event handler under the world gate or quest-completion transaction. Eligibility/enqueue remains fast, generation runs in the existing background job, and generated facts/quests/items/triggers remain an atomic persistence operation. Preserve terminal failure state and duplicate-request checks.

**Benefit:** removes an entire responsibility from LocationSimulation and concentrates quest-generation dependencies in a module whose purpose explains them. It redistributes rather than magically eliminates most edges; do not claim all LocationSimulation → Quests/Knowledge/Factions edges disappear, because cleanup/spawning still use those modules.

### 5. Give weather a smaller owner

Create `Application.Weather` containing `SyncWeatherCommand`, `GetWeatherByStateIdQuery`, `GetWeatherByLocationIdQuery`, `WeatherRoll`, and WeatherState persistence ownership. WeatherState and WeatherConditions can remain Domain types. Weather queries may use Worlds to resolve a location's state; Worlds must not depend on Weather.

LocationSimulation drives synchronization. Caravans and Scenes/GameTurns query weather directly. Purchase and boarding currently import LocationSimulation only for weather queries, so this replaces Caravans' dependency on a large orchestrator with a dependency on a focused capability.

An alternative is to put weather in Worlds, which already owns state geography. That uses fewer projects but further broadens Worlds. Prefer the small Weather module if this code will continue growing.

**Benefit:** isolates change and removes the Caravans → LocationSimulation path. Total edge count can rise because the new module needs its own foundation references.

### 6. Keep travel calculations and data access behind Routing

Replace Caravans' direct routing reads with queries for traveler identity/world, valid stops, and a journey quote at the ticket-purchase instant. Return a value containing route identity, origin/destination and timing rather than exposing DbSets. Preserve the existing rule that a valid purchased ticket fixes the trip's schedule; narration latency must not make it invalid.

Replace Routing's connector DbSets with a batched Worlds topology query. This does not add a new edge: Routing already depends on Worlds.

`CreatureRouteScheduleGenerator` and WorldGeneration's `TravelGraph` contain pure travel planning reused by generation and runtime. If separating runtime from the large generation assembly is worthwhile, extract their reusable core with Routing's pathfinding into a persistence-free `TravelPlanning` library (Domain/Common dependencies only). Keep the adapter accepting WorldGeneratorResult in WorldGeneration. Do not move the generator directly into Routing: WorldGeneration → Routing → Worlds → WorldGeneration would create a cycle.

**Benefit:** clearer route ownership and, optionally, no Routing → WorldGeneration dependency. Expect replacement references to TravelPlanning rather than a lower raw total.

### 7. Separate scene projection from turn execution when useful

A prospective `Application.Scenes` should own `GetSceneQuery`, `SceneResult`, scene mappers, `LlmScene`/its mapper, semantic comparison, movement detection and scene publication state. Move the corresponding scene/movement application events with their publishers so the extracted module does not import GameTurns events. Audit references as a group before moving them. GameTurns should continue to orchestrate mutation/catch-up before requesting a scene; Scenes should not call GameTurns.

`RefreshSceneCommand` currently spans catch-up and projection, so keep its orchestration in GameTurns. Keep HTTP/SignalR SceneSnapshot, client payloads and wire mappers in the host. Move only application scene types, never transport types, into Scenes.

**Benefit:** host look/scene consumers can depend on Scenes without pulling in turn execution. This is a boundary improvement, not a promise of fewer total edges: scene projection necessarily queries many features.

### 8. Split mixed workflows without creating reverse references

- **Room keys:** `Encounters/Commands/ReturnRoomKeyCommand.cs` includes normal return, booking deletion and an overdue encounter. Move normal return/entitlement into RoomBookings; keep overdue confrontation in Encounters; orchestrate both in GameTurns. Moving the entire file into RoomBookings would introduce RoomBookings → Encounters → RoomBookings. `GetTradeWorkstationByBuildingIdQuery` also has no encounter semantics; replace it with caller composition of Worlds rooms and Props workstations, or a focused hospitality query if it becomes broadly reused.
- **Non-fight effects:** `Encounters/Commands/AdvanceCreatureEffectsCommand.cs` advances ordinary creatures using CombatantFactory and EffectAdvancer. It is a candidate for a CreatureEffects application module, with a persistence-free effect core shared by Combat. Do not copy combat mapping into Creatures just to move one command. LocationSimulation still needs Encounters for active fights and ambient encounters, so this extraction will not remove that edge by itself.
- **Respawn:** `Creatures/Commands/ResolvePlayerRespawnCommand.cs` prepares a corpse, transfers inventory and locates a temple; `GameTurns/StreamRespawnTurnHandler` completes the turn. Move the preparation workflow to GameTurns if narrowing Creatures is a priority. Creatures still uses Worlds/Inventory for other projections and equipment, so no whole-edge savings are promised.
- **Host move tool:** move destination/interception/time/effects/movement/catch-up orchestration into a GameTurns command returning application facts. Keep MoveTool as the LLM adapter. This makes the same deterministic movement workflow reusable and testable without exercising an LLM tool.
- **Combat prose:** move `CombatNarration.cs` into host Combat presentation because its present usage is exclusively `CombatActionResultMapper`. Moving it into Application.Narration would add a dependency to a module currently concerned with lore anchors, without a demonstrated reuse benefit.

One ordering discrepancy needs a focused test before refactoring sleep: `SleepInRoomCommand` applies passive regeneration, then its caller `StreamSleepTurnHandler` advances lingering effects. The backend guide specifies effects before regeneration. Treat this as a behavior question to reproduce with a deterministic lethal-effect case, not an ordering detail to silently preserve or change during a file move.

## Event opportunities and limits

Current `DomainEventPublisher<T>` awaits consumers sequentially in DI enumeration order. It has no durable queue/outbox, delivery deduplication or retry protocol. It participates in an ambient transaction only when the caller creates one. “Domain event” does not mean “after commit” in this codebase.

| Opportunity | Suggested mechanism | What it improves | Required constraint |
|---|---|---|---|
| Quest faction reward | Typed reward event handled by Factions, or explicit Factions commands | Removes Quests' foreign writes | Finish reward before QuestCompleted reactions; preserve atomicity and avoid duplicate standing penalties. |
| Follow-up content after joining a faction | Factions emits `FactionMembershipGranted` with world/creature/faction and captured time; QuestGeneration consumes | Removes quest-generation knowledge of which quest grants membership; supports non-quest admission later | Emit only on a new membership; enqueue generation after durable eligibility, with request deduplication. |
| Room-key replacement after inn restock | `WorkstationRestocked` with world/building/workstation/time; RoomBookings owns replacement reaction | Can remove LocationSimulation → RoomBookings: the observed usage is in SyncRestockPolicyCommand | Publish once for a completed due restock; keep key issuance and LastSync persistence in the transaction. The new Common contract must not carry LocationSimulation types. |
| Ambient spawn encounter | `EncounterGroupsSpawned` with explicit group IDs/location/player/time, consumed by Encounters | Removes encounter evaluation from routine orchestration | Only newly spawned groups qualify; preserve dead-player/active-encounter checks. Still not enough to remove all LocationSimulation → Encounters dependencies. |
| Arrival after catch-up | Nested `PlayerArrivalReconciled` notification, emitted only after destination catch-up, or explicit arrival coordinator | Separates catch-up from encounter evaluation while making order visible | Never make encounter evaluation a sibling PlayerMoved subscriber that might run before catch-up. Preserve departure interception vs forced relocation semantics. |
| Combat skill/proficiency gains | Resolved usage event with world/creature/time and counts, handled by progression owners | Removes direct adjustment calls from the round resolver | Persist gains exactly once in the same transaction. Encounters still reads Creatures and WeaponProficiency, so module references remain. Low priority unless more consumers emerge. |

Already-good event uses should remain: GameSessionCreated/Deleted → Chat/NpcConversations; creature lifecycle → Props cleanup; FactLearned → quest journal/objectives; item acquisition/gifting and creature kills → quest objectives; quest gold/reputation rewards → their owners; engagement → Routing pauses. Moving contracts out of Common into a dedicated contracts assembly is optional packaging, not a semantic reduction in coupling. Keep contracts small and independent of implementation result types.

Keep direct calls where the caller needs a result or an invariant: gold/item validation, transfer/equip, room availability, boarding eligibility, learning a fact with a boolean result, movement interception, seat occupancy, combat resolution and time-skip effect ordering. Do not use events as disguised request/response messages.

## World lifecycle follow-up: concrete deletion coverage gap

[DropWorldCommand](../api/TRPG.Application.Worlds/Commands/DropWorldCommand.cs) explicitly deletes most tables but currently omits Routes, RouteSteps, RouteTravelers, CaravanFares, CaravanTickets, WeatherStates, QuestSeedSchedules and QuestChainGenerationRequests. Their [EF mappings](../api/TRPG.Data/TrpgDbContext.cs) have indexed WorldId values, without a World foreign key/cascade for these types. Source inspection therefore indicates those rows survive a world purge. This was not reproduced against PostgreSQL in this audit.

This is a concrete maintenance cost of central cross-module cleanup. Address it as a separate bugfix: seed at least one row of every world-owned type, delete the world, and verify using a second context that none survive; first establish a failing test per the repository's bugfix rule.

Longer term, use an explicit WorldLifecycle coordinator with module-owned purge participants, a deliberate dependency order and one transaction. Resolve Chat's session-based cleanup before deleting sessions. Keep bootstrap bulk insertion centralized if it is materially simpler/faster, but label its exceptional ownership and test completeness. An unordered `WorldDeleted` broadcast is a poor substitute for an atomic purge.

## SPA module inventory

These are source-folder boundaries, not separately compiled packages. The sibling dependencies below come from production feature imports; common dependencies such as generated API types, React, shared UI and lib are listed separately. Feature-level cycles do not by themselves prove a problematic runtime JavaScript initialization cycle; some links are type-only.

| Feature | Other feature dependencies | Responsibility | Placement verdict |
|---|---|---|---|
| books | None | Bookshelf and book reader dialogs | Keep. Generated API + shared UI are appropriate dependencies. |
| character | game | Character details and attribute allocation | Keep UI; depend on a session/scene context boundary instead of the game composition feature. |
| combat | game, inventory | Combat state, action/item picker, combatant/effect display, death/respawn UI | Keep combat UI. Shared session/hub/time primitives should leave game; shared attribute labels should not be owned by inventory. |
| encounters | game | Typed encounter states and resolution dialogs | Keep. Extract shared session/turn hooks from game to remove reverse dependency on the composing screen. |
| game | books, character, combat, encounters, inventory, quests | Main composition, chat, scene/context/clock, hub connection, notifications, nearby UI, caravan/sleep/wait UI | Split composition from reusable session/scene/transport state. Caravan dialog is an optional new caravans feature; do not split every small dialog just for symmetry with backend modules. |
| inventory | books, game | Item/equipment/trade/transfer UI and item presentation | Keep item presentation. Character stats panel is a candidate for character presentation/shared stats, not inventory ownership. Keep BookReader integration explicit. |
| quests | game, inventory | Quest dialog, journal/tracker and item delivery | Keep. Item visuals are legitimate reuse; shared narration rendering and session hooks should not require game-screen ownership. |
| skills | None | Ability visuals and skill tree UI | Keep. Do not merge into Combat merely because combat uses abilities. |
| world-generation | None | World creation form and defaults | Keep. Uses API/forms and shared UI. |
| worlds | game | World/local maps, geometry, markers and viewport | Keep maps; move reusable room-role icons out of game so Worlds does not depend on the screen feature for display metadata. |

The current folder graph contains game ↔ combat, game ↔ encounters, game ↔ inventory, game ↔ quests and game ↔ character relationships. The central cause is that game both composes feature UI and owns reusable state/transport primitives.

Recommended shared boundaries:

- `session`: player/session identity, hub connection and typed transport-event handling; no imports from feature components.
- `scene`: scene context/provider, version filtering, game clock and shared scene formatting; depends on generated contracts and session, not feature dialogs.
- `chat`: ChatMessage types, chat state/storage, turn-stream submission and markers. Compose feature-specific marker information as values so the storage layer does not import UI.
- `narration`: markup parsing/rendering; inject or separately compose entity tooltip behavior if otherwise it creates an inventory/session cycle.

These can be folders with explicit exports; they do not need new npm packages. Keep game as the composition root that imports features and the new shared boundaries. Preserve the existing typed event bus for broadcasts; use contexts/props for required values and operations.

Two concrete backwards imports should be corrected first:

1. `lib/session-storage.ts` imports ChatMessage from `features/game/components/chat-history`. Move the type to a chat model/contracts file and have both UI and storage depend on it.
2. `lib/game-event-bus.ts` imports TerminalCombatOutcome from the combat feature. Either derive the bus payload from generated transport contracts or put that shared type with the bus/session contracts and let combat consume it.

`components/ui` should remain feature-independent; generic hooks in `src/hooks` can stay. `src/api/client` and `src/api/signalr-client` are generated transport boundaries; regenerate them after contract changes, never relocate/edit generated models by hand. `src/test` owns test providers/MSW support; its dependency on feature providers is expected. Assets have no application responsibility to relocate. Top-level app/router code remains a composition root.

## Package and infrastructure dependencies

Direct package references, excluding transitive packages and .NET framework assemblies:

| Project(s) | Direct packages |
|---|---|
| Books, Chat, GameTurns | Microsoft.Extensions.AI |
| Combat | Microsoft.Extensions.DependencyInjection.Abstractions; Microsoft.Extensions.Options |
| LocationSimulation | NCrontab |
| Narration, Props | Microsoft.Extensions.Caching.Abstractions |
| WorldGeneration | Microsoft.Extensions.AI; Microsoft.Extensions.Options; SharpVoronoiLib |
| Data | EFCore.NamingConventions; Microsoft.EntityFrameworkCore; Microsoft.EntityFrameworkCore.Relational; Microsoft.EntityFrameworkCore.Design; Npgsql.EntityFrameworkCore.PostgreSQL; TickerQ.EntityFrameworkCore |
| Host | Anthropic; Microsoft.AspNetCore.OpenApi; Microsoft.Extensions.AI; Microsoft.Extensions.ApiDescription.Server; Microsoft.OpenApi; OllamaSharp; Scrutor; TickerQ; TickerQ.EntityFrameworkCore; TypedSignalR.Client.TypeScript.Attributes; TypedSignalR.Client.TypeScript.Analyzer; ZLogger |
| Tests | coverlet.collector; Microsoft.AspNetCore.Mvc.Testing; Microsoft.AspNetCore.SignalR.Client; Microsoft.NET.Test.Sdk; Testcontainers.PostgreSql; TypedSignalR.Client; xunit.runner.visualstudio; xunit.v3 |
| All other backend projects | No direct PackageReference entries |

The SPA shares one package manifest: React/React DOM, TanStack Query/Router/Form, SignalR, Zod, Tailwind/Radix/shadcn, icons/fonts, Motion/Sonner, XYFlow/Dagre and utility packages. Its toolchain includes TypeScript/Vite, Hey API generation, Vitest/Testing Library/MSW, Storybook and Oxfmt/Oxlint. Feature folders do not declare separate package manifests, so the feature table describes source ownership rather than installed-package isolation. Exact packages/versions are in [spa/package.json](../spa/package.json).

## Suggested implementation order and verification

1. Independently reproduce/fix world-purge coverage. Remove compiler-proven unused project references and clarify the WorldGeneration guide. These are separate concerns and should be reviewable separately.
2. Move Fact APIs/ownership to Knowledge; correct faction writes/reads; pass captured time into mechanics. These address real ownership with limited new abstractions.
3. Replace Caravans/Routing foreign schema access with batched owner queries; extract Weather and QuestGeneration if those boundaries are accepted.
4. Thin MoveTool and split normal room-key return from confrontation. Add only event seams that remove a known responsibility, starting with faction membership and inn restocking.
5. Extract Scenes and pure TravelPlanning if independent consumers justify them. Fix SPA reverse imports and separate shared session/scene/chat concerns.

For each change, distinguish a removed method call, a removed project edge and a replaced dependency. Do not claim a project edge is gone while queries/result types still use it. New modules add foundation edges; optimize the dependency direction and contract surface, not just the number 135.

Add architecture checks for: no Data/persistence dependency in pure rule libraries; no foreign module context access outside an explicit lifecycle allowlist; direct declarations for used project dependencies; no feature UI imports from SPA shared state/storage; and no forbidden project cycles. A shared Data assembly does not enforce any of these automatically.

For gameplay refactors, trace UI submission → host validation → mutation → event reactions → narration → transport mapping → follow-up UI. Cover critical branches deterministically:

- Quest completion with/without faction rewards; membership precedes follow-up generation; rollback does not leave rewards applied.
- Learning a book fact for the first time vs rereading; quest eligibility/journal updates remain correct.
- Ordinary movement vs interception/forced relocation; destination catch-up precedes arrival encounters.
- Ticket purchase/boarding before/after narration delay, bad weather and insufficient funds.
- Sleep/wait effects that kill vs survive, rested XP boundary and scene/vitals version ordering.
- Due vs not-due restock, missing vs existing room keys, duplicate event/request delivery where supported.
- World purge across all owning tables, verified from a fresh context.

Run the repository build and relevant backend tests for code changes. For frontend changes, run fmt:check, typecheck and tests; regenerate API/SignalR clients when contracts change. No build or behavioral test was run for this documentation-only audit, and no production files were changed.
