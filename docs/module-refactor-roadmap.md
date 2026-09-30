# Module refactor roadmap

This document tracks the remaining work from the module audit. Each milestone is intended to be a focused branch and pull request. Complete milestones in order unless a milestone explicitly says that it is independent.

The Effects extraction was completed before this checklist was introduced in PR #286. The milestones below cover the remaining work.

## Completion convention

Each milestone has one completion commit whose subject uses this exact format:

```text
milestone(MNN): complete <short milestone name>
```

Example:

```text
milestone(M01): complete world deletion coverage
```

The completion commit is the clear marker that the milestone is ready for review. It must:

- Be the final milestone commit after the required tests and checks pass.
- Change the milestone's top-level checkbox in this document from `[ ]` to `[x]`.
- Check every completed scope, acceptance, and verification item for that milestone.
- Contain a real checklist or implementation change; do not create an empty marker commit.
- Use the milestone ID and short name shown in this document.

Normal implementation commits may precede the completion commit. If implementation changes are added after the marker, the milestone is no longer complete: reset its top-level checkbox, re-run its verification, and add a new completion commit after the follow-up work.

Each milestone should normally use a branch named `refactor/mNN-short-name` and a separate pull request. Start the branch from the latest `main`. Do not combine milestones solely to reduce the number of pull requests.

## Milestone tracker

- [x] M01 — World deletion coverage
- [x] M02 — Dependency cleanup and architecture checks
- [x] M03 — Fact ownership in Knowledge
- [x] M04 — Faction ownership
- [x] M05 — World time and captured gameplay time
- [x] M06 — QuestGeneration extraction
- [x] M07 — Weather extraction
- [x] M08 — Routing data boundaries
- [ ] M09 — Room-key workflow split
- [ ] M10 — GameTurns movement workflow
- [ ] M11 — Notification event seams
- [ ] M12 — Application scene projection
- [ ] M13 — SPA shared boundaries
- [ ] M14 — Optional placement cleanup

## Wave 1 — correctness and guardrails

### [x] M01 — World deletion coverage

Ensure deleting a world removes every record owned by that world. This is a bug fix and must begin with a failing integration test.

Scope:

- [x] Add records for every world-owned table to a world-deletion integration test.
- [x] Confirm the test fails before changing `DropWorldCommand`.
- [x] Add ordered deletion for Routes, RouteSteps, RouteTravelers, CaravanFares, CaravanTickets, WeatherStates, QuestSeedSchedules, and QuestChainGenerationRequests.
- [x] Check the current EF model for any additional world-owned tables missing from the command.
- [x] Query through a fresh context after deletion so tracked entities cannot hide survivors.

Acceptance:

- [x] No seeded record for the deleted world remains.
- [x] Records belonging to another world remain unchanged.
- [x] Foreign-key ordering is explicit and the deletion remains transactional.

Verification:

- [x] Focused `DropWorldCommand` integration tests pass.
- [x] Full backend test suite passes.
- [x] Repository formatting checks pass.

Completion commit:

```text
milestone(M01): complete world deletion coverage
```

### [x] M02 — Dependency cleanup and architecture checks

Remove compiler-proven unused project references and add automated checks that prevent the same boundary problems from returning.

Scope:

- [x] Remove Caravans → Configuration.
- [x] Remove CreatureJobs → GameSessions.
- [x] Remove Creatures → Factions.
- [x] Remove Crimes → Factions.
- [x] Remove Reputations → Configuration.
- [x] Remove Routing → Configuration.
- [x] Move the Combat-only `Shuffled` helper out of Common.
- [x] Add an architecture check for forbidden project cycles.
- [x] Add an architecture check for undeclared direct project dependencies.
- [x] Add an allowlisted check for foreign module database-context access.
- [x] Add a check that persistence-free rule modules do not reference Data.
- [x] Correct the WorldGeneration guide wording to allow foundational, stateless dependencies.

Acceptance:

- [x] Each removed reference is independently verified by a successful solution build.
- [x] Existing foreign-context violations are documented in a small, named allowlist.
- [x] A new unapproved foreign-context dependency makes the architecture test fail.
- [x] The checks inspect production projects without relying on broad test-project transitive references.

Verification:

- [x] Architecture tests pass.
- [x] Full solution build passes.
- [x] Full backend test suite passes.
- [x] Repository formatting checks pass.

Completion commit:

```text
milestone(M02): complete dependency guardrails
```

## Wave 2 — restore data ownership

### [x] M03 — Fact ownership in Knowledge

Make Knowledge the owner of general facts while Books continues to own works, pages, reading, and book text generation.

Scope:

- [x] Move `AddFactsCommand` from Books to Knowledge.
- [x] Move `GetFactByIdQuery` from Books to Knowledge.
- [x] Move `Facts` access from `IBooksDbContext` to `IKnowledgeDbContext`.
- [x] Update Books page composition to use Knowledge contracts.
- [x] Update Quests fact disclosure to use Knowledge contracts.
- [x] Update GameTurns briefing and completion narration to use Knowledge contracts.
- [x] Update quest generation to add facts through Knowledge.
- [x] Remove Books project references where fact access was the only use.
- [x] Update the backend project map and affected workflow documentation.

Acceptance:

- [x] Books has no general Fact persistence ownership.
- [x] Reading a fact for the first time still returns the correct learned result.
- [x] Rereading does not duplicate knowledge or quest progress.
- [x] Quest fact disclosure and NPC briefing return the same information.

Verification:

- [x] Knowledge fact command/query tests pass.
- [x] Book reading and fact-learning tests pass.
- [x] Quest disclosure and GameTurns briefing tests pass.
- [x] Full backend test suite passes.

Completion commit:

```text
milestone(M03): complete knowledge fact ownership
```

### [x] M04 — Faction ownership

Remove direct faction persistence access from Quests. Required reads use Factions queries; membership and standing writes use explicit Factions commands so transaction order remains visible.

Scope:

- [x] Replace faction reads in quest acceptance with a Factions query.
- [x] Replace faction reads in quest interactions and marker projection with a batched Factions query.
- [x] Add an idempotent command for granting faction membership.
- [x] Add a focused command for applying the terminal-chain standing change.
- [x] Call faction reward commands inside the existing quest-completion transaction.
- [x] Complete faction rewards before publishing `QuestCompletedEvent`.
- [x] Remove direct `IFactionsDbContext` access from Quests.
- [x] Replace quest-generation faction reads with Factions queries, either here or in M06.

Acceptance:

- [x] Completing a membership-reward quest grants membership once.
- [x] Retrying an already-applied membership does not insert a duplicate.
- [x] Terminal-chain standing changes preserve the existing rules.
- [x] Follow-up quest generation observes the new membership.
- [x] A failed completion transaction leaves no partial quest or faction reward.

Verification:

- [x] Quest acceptance and marker tests pass with and without required membership.
- [x] Quest completion tests cover membership, standing, retry, and rollback cases.
- [x] Full backend test suite passes.

Completion commit:

```text
milestone(M04): complete faction ownership
```

### [x] M05 — World time and captured gameplay time

Keep session lookup in GameSessions while moving world-based clock operations to Worlds. Deterministic mechanics receive the already-captured game instant instead of reading the clock again.

Scope:

- [x] Move `AdvanceTimeCommand` to Worlds.
- [x] Move `GetGameTimeByWorldIdQuery` to Worlds.
- [x] Keep the session-ID-based game-time query in GameSessions.
- [x] Add `GameInstant` to `AdjustCreatureSkillsCommand`.
- [x] Propagate the captured instant through `ApplyCombatUsageCommand` and combat round/cast workflows.
- [x] Propagate the captured instant through theft, lockpick, cell-unlock, and sneak workflows.
- [x] Add the captured instant to `CreatureFreedEvent`.
- [x] Stop the creature-freed schedule handler from reading the clock again.
- [x] Remove Creatures → GameSessions after all callers migrate.
- [x] Remove other GameSessions references that become unused.

Acceptance:

- [x] One gameplay operation uses one captured game instant throughout.
- [x] Rested skill experience is deterministic at the exact expiry boundary.
- [x] Combat and non-combat skill gains use the supplied instant.
- [x] Time skips retain their explicit order for effects, regeneration, and catch-up.
- [x] No generic `WorldTimeAdvanced` event replaces ordered workflows.

Verification:

- [x] Skill-adjustment tests cover both sides of the rested boundary.
- [x] Combat casting and round-resolution tests pass.
- [x] Theft, lockpick, jailbreak, wait, sleep, movement, and caravan tests pass.
- [x] Full backend test suite passes.

Completion commit:

```text
milestone(M05): complete captured gameplay time
```

## Wave 3 — split LocationSimulation

### [x] M06 — QuestGeneration extraction

Create `Application.QuestGeneration` for quest seeding, request lifecycle, and generation orchestration. LocationSimulation remains the scheduler of location catch-up and calls one seed-if-due operation.

Scope:

- [x] Create the QuestGeneration project and service-registration extension.
- [x] Move the six deterministic quest seed commands.
- [x] Move LLM quest-chain seeding and generation commands.
- [x] Move `SyncQuestSeedScheduleCommand`.
- [x] Move `QuestCompletedFactionChainSeedEventHandler`.
- [x] Move `IQuestChainGenerationScheduler`.
- [x] Move persistence ownership for QuestSeedSchedule and QuestChainGenerationRequest.
- [x] Extract shared generated-creature persistence into CreatureSpawning.
- [x] Update the host TickerQ job and startup recovery to use QuestGeneration contracts.
- [x] Keep pure graph, casting, and content generators in WorldGeneration initially.
- [x] Replace foreign Factions persistence reads with Factions queries.
- [x] Update the backend project map and continuous-simulation workflow documentation.

Acceptance:

- [x] LocationSimulation exposes one seed-if-due call at a captured location and instant.
- [x] Slow LLM generation never runs under the world mutation gate or quest-completion transaction.
- [x] Pending/in-progress/completed/failed request behavior is unchanged.
- [x] Duplicate request prevention remains effective.
- [x] Generated facts, quests, items, and triggers remain atomic.

Verification:

- [x] Every deterministic seed command test passes in its new module.
- [x] LLM request lifecycle and startup recovery tests pass.
- [x] Location routine tests pass.
- [x] Full backend test suite passes.

Completion commit:

```text
milestone(M06): complete quest generation extraction
```

### [x] M07 — Weather extraction

Create `Application.Weather` as the focused owner of weather state, rolls, synchronization, and queries.

Scope:

- [x] Create the Weather project (no service-registration extension: it registers no services beyond the assembly scan).
- [x] Move `SyncWeatherCommand`.
- [x] Move `GetWeatherByStateIdQuery` and `GetWeatherByLocationIdQuery`.
- [x] Move `WeatherRoll`.
- [x] Move WeatherState persistence ownership to a Weather module context.
- [x] Let Weather use Worlds to resolve a location's state when needed.
- [x] Update LocationSimulation to schedule Weather synchronization.
- [x] Update location job simulation to query Weather.
- [x] Update Caravans to query Weather directly.
- [x] Remove Caravans → LocationSimulation if no other use remains.
- [x] Update the backend project map and continuous-simulation workflow documentation.

Acceptance:

- [x] Worlds does not depend on Weather.
- [x] Weather synchronization remains idempotent at the supplied instant.
- [x] Caravan purchase and boarding retain their bad-weather behavior.
- [x] Scene weather output remains unchanged.

Verification:

- [x] Weather synchronization and query tests pass.
- [x] Caravan purchase and boarding tests pass in good and bad weather.
- [x] Scene and location-job tests pass.
- [x] Full backend test suite passes.

Completion commit:

```text
milestone(M07): complete weather extraction
```

### [x] M08 — Routing data boundaries

Hide Routing persistence behind owner queries and projections. Caravans should consume stable journey information rather than Routing DbSets.

Scope:

- [x] Add a Routing projection for traveler identity and world.
- [x] Add a Routing projection for valid stops.
- [x] Add a journey quote containing route, origin, destination, and timing.
- [x] Replace direct `IRoutingDbContext` access in caravan interaction, ticket purchase, and boarding.
- [x] Add batched Worlds travel-topology and connector-distance queries.
- [x] Remove LocationConnector and TravelConnector access from `IRoutingDbContext`.
- [x] Update Routing algorithms to consume the Worlds topology projection.
- [x] Decide whether shared pure algorithms justify a later TravelPlanning project; do not create it solely for symmetry.

Decision: Keep pathfinding in Routing for now. No second module needs the algorithm, so a TravelPlanning project would add a boundary without creating shared ownership value.

Acceptance:

- [x] A purchased ticket retains its quoted schedule across narration delay.
- [x] Invalid stops, insufficient funds, and late boarding behave as before.
- [x] Caravans cannot access Routing DbSets.
- [x] Routing cannot access Worlds-owned connector DbSets.
- [x] No WorldGeneration → Routing → Worlds → WorldGeneration cycle is introduced.

Verification:

- [x] Caravan interaction, purchase, and boarding tests pass.
- [x] Routing schedule, position, and pathfinding tests pass.
- [x] Architecture tests confirm the foreign contexts are gone.
- [x] Full backend test suite passes.

Completion commit:

```text
milestone(M08): complete routing data boundaries
```

## Wave 4 — workflow boundaries and event seams

### [ ] M09 — Room-key workflow split

Move ordinary key return and booking deletion to RoomBookings while Encounters retains overdue confrontation. GameTurns or the host adapter coordinates the two outcomes without creating a module cycle.

Scope:

- [ ] Add a RoomBookings command for ordinary key return and entitlement removal.
- [ ] Keep overdue confrontation in Encounters.
- [ ] Replace `GetTradeWorkstationByBuildingIdQuery` with composition from Worlds and Props queries, or a focused owner projection.
- [ ] Update `ReturnRoomKeyTool` to call the workflow boundary.
- [ ] Preserve key ownership, booking state, and confrontation narration results.

Acceptance:

- [ ] An on-time return never creates an encounter.
- [ ] An overdue return follows the existing confrontation branches.
- [ ] RoomBookings does not reference Encounters.
- [ ] Encounters no longer owns ordinary booking deletion.

Verification:

- [ ] On-time, overdue, missing-key, and replacement-key tests pass.
- [ ] Tool-level return-key tests pass.
- [ ] Full backend test suite passes.

Completion commit:

```text
milestone(M09): complete room key workflow split
```

### [ ] M10 — GameTurns movement workflow

Move deterministic movement orchestration from the host `MoveTool` into GameTurns. The host remains an LLM adapter.

Scope:

- [ ] Add a GameTurns command for destination validation and movement orchestration.
- [ ] Move interception, time advancement, effect reconciliation, movement, and destination catch-up into that command.
- [ ] Return application facts needed for narration.
- [ ] Keep tool descriptions, JSON schema, and LLM serialization in the host.
- [ ] Preserve the order between departure interception, relocation, destination catch-up, and arrival encounter evaluation.

Acceptance:

- [ ] The movement workflow can be tested without invoking an LLM tool.
- [ ] Ordinary movement, blocked movement, interception, and forced relocation retain behavior.
- [ ] Destination catch-up completes before arrival encounter evaluation.
- [ ] One captured instant flows through the operation.

Verification:

- [ ] New GameTurns command tests cover every movement branch.
- [ ] Existing `MoveTool` tests pass as adapter tests.
- [ ] Scene and ambient encounter tests pass.
- [ ] Full backend test suite passes.

Completion commit:

```text
milestone(M10): complete movement workflow extraction
```

### [ ] M11 — Notification event seams

Use events for completed notifications where no synchronous result is required. Keep validation, transaction-sensitive work, and ordered gameplay as direct calls.

Scope:

- [ ] Add a small `WorkstationRestocked` contract with world, building, workstation, and captured time.
- [ ] Publish it once after a due restock is persisted.
- [ ] Let RoomBookings own replacement-key reaction.
- [ ] Remove LocationSimulation → RoomBookings if no direct use remains.
- [ ] Evaluate an `EncounterGroupsSpawned` event carrying only newly spawned group IDs, location, player, and time.
- [ ] Add the spawn event only if ordering and duplicate behavior remain explicit.
- [ ] Keep movement interception, time skips, quest rewards, and combat persistence as direct commands.

Acceptance:

- [ ] Not-due restocks publish nothing.
- [ ] Due restocks publish once and preserve key replacement behavior.
- [ ] Event payloads contain IDs and values, not feature implementation result types.
- [ ] Spawn reactions retain dead-player and active-encounter checks if implemented.
- [ ] No workflow relies on sibling event-handler registration order.

Verification:

- [ ] Restock tests cover due, not due, missing key, existing key, and repeat delivery.
- [ ] Ambient encounter tests cover only newly spawned groups if the spawn event is added.
- [ ] Architecture tests confirm any removed project edge.
- [ ] Full backend test suite passes.

Completion commit:

```text
milestone(M11): complete notification event seams
```

## Wave 5 — scene and frontend boundaries

### [ ] M12 — Application scene projection

Create a real `Application.Scenes` project for the scene read model. GameTurns continues to orchestrate mutation and catch-up before requesting a scene.

Scope:

- [ ] Inventory scene queries, results, mappers, semantic comparison, movement detection, and publication state as one group.
- [ ] Move that group into the Scenes project.
- [ ] Move corresponding application events and publishers so Scenes does not import GameTurns events.
- [ ] Keep `RefreshSceneCommand` orchestration in GameTurns.
- [ ] Keep SignalR/HTTP `SceneSnapshot` contracts and wire mappers in the host.
- [ ] Update consumers to reference Scenes instead of GameTurns where appropriate.
- [ ] Update the backend project map and scene-refresh workflow documentation.

Acceptance:

- [ ] Scenes never calls GameTurns.
- [ ] GameTurns performs catch-up before scene projection.
- [ ] Snapshot version filtering and movement markers retain behavior.
- [ ] Host look/scene consumers do not need turn-execution contracts.

Verification:

- [ ] Scene projection, comparison, and movement detection tests pass.
- [ ] Refresh, SignalR snapshot, and ambient publication tests pass.
- [ ] Full backend test suite passes.

Completion commit:

```text
milestone(M12): complete scene projection extraction
```

### [ ] M13 — SPA shared boundaries

Separate reusable session, scene, chat, and narration state from the `game` composition feature. `game` remains the screen-level composition root.

Scope:

- [ ] Move `ChatMessage` into a chat model or contract file.
- [ ] Remove the `session-storage` import from a game component.
- [ ] Move the terminal combat outcome contract beside the event bus or generated transport boundary.
- [ ] Remove the shared event bus import from the Combat feature.
- [ ] Extract session identity, hub connection, and typed transport handling.
- [ ] Extract scene context, version filtering, game clock, and shared scene formatting.
- [ ] Extract chat state, storage, streaming submission, and marker models.
- [ ] Extract narration parsing/rendering without creating an inventory cycle.
- [ ] Move reusable room-role icons out of `features/game`.
- [ ] Migrate Character, Combat, Encounters, Inventory, Quests, and Worlds away from reusable `features/game` imports.

Acceptance:

- [ ] Shared storage and event infrastructure imports no feature component.
- [ ] Feature folders consume stable shared hooks/contracts rather than the composing game screen.
- [ ] `game` may import feature UI; reusable feature code does not import `game` composition code.
- [ ] Generated API and SignalR files remain generated and are not manually relocated.

Verification:

- [ ] SPA formatting passes.
- [ ] SPA typecheck passes.
- [ ] SPA unit tests pass.
- [ ] Relevant Storybook stories render successfully.

Completion commit:

```text
milestone(M13): complete spa shared boundaries
```

## Wave 6 — optional placement cleanup

### [ ] M14 — Optional placement cleanup

Apply the remaining small placement improvements only after the higher-value boundaries are stable. Each item should be kept or dropped based on the dependencies that exist at that time.

Scope:

- [ ] Move `CombatNarration` into the host Combat presentation area if it still has only host consumers.
- [ ] Reassess moving respawn preparation from Creatures to GameTurns.
- [ ] Reassess a persistence-free TravelPlanning project after M08.
- [ ] Reassess renaming Narration to LoreLinks if its responsibility remains lore linking.
- [ ] Reassess extracting Trading only if trade workflows have grown beyond Inventory ownership.
- [ ] Record rejected items and the current reason instead of moving code for symmetry.

Acceptance:

- [ ] Every performed move removes a demonstrated placement problem.
- [ ] No move introduces a reverse dependency or project cycle.
- [ ] Deferred or rejected items have a short decision recorded in this section.

Verification:

- [ ] Focused tests for every moved workflow pass.
- [ ] Architecture tests pass.
- [ ] Full backend or frontend suite passes as applicable.

Completion commit:

```text
milestone(M14): complete optional placement cleanup
```

## Definition of complete

A milestone is complete only when its scope and acceptance checklists are satisfied, its required verification has passed, its documentation reflects the final structure, and its completion commit follows the marker convention. A merged pull request without the marker commit does not complete the milestone.

When a milestone intentionally changes scope, update this roadmap before marking it complete. Keep the milestone ID stable so branches, commits, and pull requests remain searchable.
