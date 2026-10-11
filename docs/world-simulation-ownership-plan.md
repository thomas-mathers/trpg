# World simulation ownership plan (issue #324 and follow-ups)

Written 2026-10-10, restructured 2026-10-11 around the decide/apply pipeline so work can resume with no prior context. Read `AGENTS.md` and `api/AGENTS.md` first; this file only adds the plan.

## Goal

`TRPG.Application.WorldSimulation` owns the runtime world. One loop per world, one pipeline:

```
request / mutation -> mailbox -> runner -> event queue -> persist batch -> projector -> SignalR client events
```

1. The runner is the only thing that executes or mutates world state. Everything outside posts a mailbox message.
2. A single entry point (working name `CreatureSimulator`) holds autonomous per-creature decision making. Its `Tick` takes a creature's state and returns a list of events. It is a thin composition over small pure concern functions (movement, effects, regeneration, ambient encounters), each with its own input, not one class holding all the logic. It is pure: no `DbContext`, no clock reads, no I/O. The runner loops over the due creatures, collects the events, persists them in one batch and hands them to the projector.
3. `ContinuousWorldService` / `ContinuousWorldProcessor` are deleted. Time-driven work is the creature simulator plus a due schedule inside the runner.
4. Scene diffing is deleted. Mutations announce what changed and the projector turns that into client events. The full-scene payload stays an event, `SceneUpdated` carrying `SceneSnapshot` (no rename), raised only when the player enters a location, connects or reconnects, explicitly asks for a refresh, or the server resyncs a client after a failed projection. Any other change to a location's contents MUST be a granular event, never `SceneUpdated`.
5. The granular scene delta events and their publishing move from `TRPG.Application.Scenes` to `TRPG.Application.WorldSimulation`. Scenes keeps only the snapshot read model.
6. `IWorldMutationGate` is removed. Nothing at the boundary (SignalR handler, HTTP endpoint, LLM tool call) knows a gate exists; it posts a message and awaits the result.

## Done so far (merged)

- #335 removed no-op traveler sync commands.
- #336 shared prop solidity and added a server-side movement integrator.
- #337 applies player movement inputs on the server.
- #338 flushes player poses from the runner and steps movement at a fixed rate.
- #339 the runner owns live player poses (`LivePlayerPoses`).
- #340 sit, stand and sneak are request messages through the mailbox. Sneak moved from REST to the SignalR action `SendToggleSneaking`.

## Target shape

Per tick the runner does: retry any events retained from a failed persist, drain a bounded number of mailbox messages (each message is a handler), batch-load state for the creatures due from the due schedule, call `CreatureSimulator.Tick` for each, push every event onto the event queue, persist the queue's changes in one batch, then project to clients.

### Terminology

- **Location maintenance:** the 30 s work done per watched location by `SyncLocationRoutinesCommand` (weather sync, door locks, spawning, restocking, quest seeding), plus ambient encounter evaluation for newly spawned groups (`SyncActiveLocationRoutinesCommand`). Scene refresh runs the same work as lazy catch-up on entry. Code names keep "Routines"; this document says location maintenance.
- **NPC planning and movement:** journey planning and movement (`PlanMovements`, `WorldSimulator.Step`). It runs for every creature in the world and is not location maintenance.
- **Watched location:** a location a connected player is in, tracked by the scene subscriptions.
- **Catch-up:** resolving owed time-based state (effects, regeneration, maintenance) in one step at a given instant instead of tick by tick.

### What lives in memory, and why

Memory holds only state that is high-frequency or derivable. The database is the source of truth for mutable gameplay state, and because the runner is the single writer, committed database state does not change underneath it between ticks (see "Working state and ordering" for what that does and does not guarantee). Immutable shared data (the travel graph, navigation grids) is cached freely; mutable gameplay state is not.

- In memory: NPC motion state (route progress, local-move progress, idle and sleep timers), live player poses, the due schedule, the scene subscriptions, `SevereWeatherLocationIds`, the mailbox and event queue. The scene subscriptions and `SevereWeatherLocationIds` are rebuildable (a restart drops every connection, and each reconnect posts `Connect`).
- Database: creature stats, inventory, effects, quests, relationships, schedules, weather state, clock anchor, props, caravans, scene contents.
- Decided: no in-memory scene store. `SceneUpdated` is built by a query at enter time.

### The creature simulator

- `Tick(CreatureTickInput input, SimulationContext ctx) -> IReadOnlyList<SimEvent>` composes the concern functions. `ctx` carries `now`, the world clock and weather, not a clock the creature simulator reads itself. Unit-testable with no database.
- Per-creature concerns: movement (the existing `WorldSimulator` logic), active effects, regeneration, ambient encounters for newly spawned groups. Location maintenance is location-scoped, not per creature: it stays a scheduled command on watched locations until I5. Weather and the clock are the same shape on a world-level input (one pseudo-entity), not a separate mechanism.
- Input is the hot in-memory motion state plus a snapshot the runner batch-loads for creatures that are due for a slow concern.
- Scope: movement stepping ticks every creature in the world in memory. Journey and local-activity planning (`PlanMovements`) is database-backed and bounded by a per-tick candidate budget, so it is not free. Effects and regeneration tick every creature at a watched location every 5 s, NPCs included. Location maintenance runs at watched locations every 30 s. Creatures elsewhere are caught up (owed effects first, then regeneration) when they enter a watched location or the player looks at them.
- Boundary rules: a creature entering a watched location is caught up before its arrival is announced, then joins the ticked set; one leaving drops out and is caught up next time. A catch-up DoT can kill: effects are resolved at the catch-up instant and at the creature's current place (the same rule as a time skip today), so a 1 HP creature with a DoT that follows the player into a watched location arrives dead, with the corpse, loot, reputation, quest and route-cancellation consequences applied there. No history of earlier positions is retained; reconstructing death at the historical instant and place is a separate decision, not part of this refactor. Fight participants are excluded from regeneration and handled by the fight path, as today.
- Player visibility (requirement): effect and regen change events at a watched location project to `CreatureVitalsUpdated` for the player and NPCs alike (renamed from `PlayerVitalsUpdated` in I3; see I3 and I4), so DoT ticks and regen on nearby creatures are visible, one update per 5 s pass. Changes beyond vitals (effect added or expired, death) still go through `CreaturesUpdated`. Today the diff produces these through `CurrentHp` in `SceneSemanticComparer`; deleting the diff must not lose it.
- Due schedule: per creature and concern, a next-due time (movement every tick, effects and regen 5 s, location maintenance 30 s). Most creatures are skipped most ticks. This is how the loop avoids loading the whole world 5 times a second.
- Weather exposure (the 30 s refresh feeding NPC sheltering) is an input to movement decisions, not its own concern, and emits no client event. The weather state change itself comes from the world-level decision and announces `WeatherChanged`. Caveat: sheltering is not wired today. `WeatherShelter.Apply` has no caller, so a change to the exposed set only resets `NextUpdate` and forces a replan; no NPC is redirected (see Open decisions).

### Working state and ordering

- A tick's decisions read a per-tick working copy of creature state loaded after the mailbox drain, not the database. Each concern's events are applied to the working copy before the next concern reads it (effects, then regeneration, then encounters), so regeneration sees HP that effects just changed. The applier then persists the combined result.
- A single writer does not make every read current: unpersisted decisions exist between decide and apply. Anything that reads the database for a message (the entry snapshot, the scene query) runs only against persisted state: the tick starts by flushing events retained from a failed persist, and the batch-load happens after the drain so decisions see what message handlers committed.

### Time

- The runner re-reads the world clock after any message that advances it (wait, sleep, caravan boarding via `IWorldClock.Advance`) and uses the new instant for the rest of the tick. The coordinator-captured `now` is only the starting instant. Today a stale instant would reach `PlanMovements` and `Step`; this becomes reachable once those actions are mailbox messages in PR 0b.
- The due schedule compares game-time deadlines, not a real-time cadence. The 5 s and 30 s intervals are the normal cadence at the world's time scale; after a skip of several game hours, everything past due runs on that tick, owed effects first and regeneration at the resulting instant.

### Lifecycle and fairness

- The mailbox is serviceable whenever the runner exists, including while the world clock is paused. Today `WorldSimulationCoordinator.Tick` visits only active worlds, so a request to a paused world waits until its token cancels. The coordinator drains every loaded runner's mailbox each tick; the creature simulator, due schedule and movement advance only while the world is active.
- Explicit outcomes: loaded means queued; loading means await the load, then queue; failed or unavailable throws a defined error (today `Post` silently drops and `Request` runs inline); deleted throws not found. There is no inline path without a runner.
- Fairness: each tick handles at most a fixed number of messages and a time budget, and the rest stay queued so the simulation keeps stepping. Both limits are named constants next to the planning budget. Target latency for a request is one tick plus the work of the messages ahead of it. Long work (LLM narration) never runs inside a message.

### Failure and recovery

- A failed batch blocks everything behind it. The tick starts by retrying the retained events; until they commit, the mailbox is not drained, nothing is decided and no projection runs. This keeps a replayed event from overwriting a later mutation, since absolute values alone are not idempotent.
- Each batch is one transaction that also writes a marker (the per-world state version from `StampWorldStateCommand`). After an uncertain failure (connection lost around commit), the retry first checks whether the marker committed and, if so, only discards the batch and projects it. Consequences that are not naturally idempotent (death, loot creation, quest progress) run inside the batch transaction, so they commit or roll back with it.
- Events leave the queue only after their batch commits. After a set number of consecutive failures the runner reloads from the database, dropping in-memory motion state so it cannot drift.
- A projection failure is not silently dropped. The runner marks each affected player's watch as needing resync and pushes a fresh `SceneUpdated` baseline on the next tick, making up to three attempts within five seconds of real time (configurable, owned by PR D). A slow send must not hold the world loop for that period: each attempt runs with its own timeout off the tick path, and the loop only observes its outcome. If that also fails, it drops the watch and closes the player's hub connection so the SPA reconnects and posts `Connect`. Recovery must be automatic because a client cannot detect a missed delta itself (versions are shared by siblings, and ordered delivery cannot recover an event the server never sent).
- A message handler that throws returns the failure to its caller and the runner continues. Commands it already committed stay committed (they are separate transactions today), and announcement events are emitted only after a command returns, so a late throw can leave watchers stale. Decided: after any failed mutating message, regardless of the world state version (some writers, such as `SetCreatureActivityCommand`'s `ExecuteUpdateAsync`, commit without advancing it), every scene subscription in that world is marked for resync like a projection failure, and the runner reconciles its in-memory state with the database, because a resync repairs only client state. Reconciliation reuses the reload-from-database path (per creature through `WorldSimulatorLoader.LoadSeed` is a later optimization). Ordering: reconciliation finishes before the runner processes another message or advances the simulation; if the reload fails, the runner stays in recovery (retrying the reload, rejecting new messages with the defined unavailable error, no stepping) instead of continuing on stale state. The reload also clears events queued from the discarded in-memory state, after first resolving any batch with an uncertain commit through its marker, so no stale event can later overwrite what the database now holds. Failures are rare, so an occasional unnecessary refresh and reload is the accepted cost. Running a message inside one outer transaction (commands joining an ambient `TransactionScope`) would avoid this, but keeping independent commits is the chosen design for now, not a technical limit; revisit it separately.
- A tick exception is logged by the coordinator and the next tick resumes.

### Client resync

- Each event batch carries the per-world version from `StampWorldStateCommand`; `SceneUpdated` carries the baseline version; deltas in one batch share a version and the client accepts all of them; other locations are dropped.
- The SPA reducer (`spa/src/features/game/providers/scene-state.ts`) needs three changes, because today `applySnapshot` rejects `snapshot.version <= state.scene.version`. A client that applied one delta at version 42 and missed its sibling would then reject the corrective snapshot at 42.
  - An authoritative `SceneUpdated` replaces state when its version is equal to or newer than the latest version applied; an older one is rejected.
  - Track the snapshot baseline version separately from the latest delta version. A delta whose version is at or below the baseline is already included in that snapshot and is dropped, so a delayed delta cannot undo a resync. A delta above the baseline applies if it is not older than the latest delta.
  - `applyVitals` uses the same rule instead of `vitals.version <= scene.version`, which today rejects same-version sibling events.
- Explicit refresh: a hub method posts a mailbox message that runs the same handler as entry and replies with a new `SceneUpdated`. The SPA uses it when it cannot reconcile (for example a delta for an unknown creature) and after a reconnect. The server uses the same path to resync after a failed projection.
- No gap detection in the first slice; recovery is server-driven (see "Failure and recovery").

### Two kinds of event

- Change events: output of the creature simulator. They carry enough to persist (a regen result carries the new HP, not "HP changed"). The applier persists them in one batch per tick, before anything is projected, so a client never hears about an unpersisted change.
- Announcement events: output of existing mailbox commands. The command already persisted; the event only needs projecting.
- Both go through the same queue to the same projector. Client events (`CreaturesUpdated` and so on) are projections of these, not the same objects.
- Open (see below): whether commands are ever converted to simulator concerns. Default no.

### How `SceneUpdated` and the scene subscriptions fit

Entering a location (connect, reconnect, `PlayerMoved`, or an explicit refresh request) is a mailbox message handled on the runner. The handler, in order: builds `SceneUpdated` with the scene query at enter time, then records that the player now watches that location. The projector sends a player only the granular events for their watched location. The runner is serial, so nothing changes the location between the read and the watch registration, and every later delta applies on top of the `SceneUpdated` state. Both carry the same per-world version (`StampWorldStateCommand`). Moving replaces the player's watch and sends a new `SceneUpdated`. Today the watched location comes from `GetActiveLocationPlayersQuery`; the scene subscriptions replace it.

## Mechanisms to know today (all verified 2026-10-10)

- Runner tick (`WorldSimulationRunner.Tick`, ~200 ms, ticked per active world by `WorldSimulationCoordinator.TickWorld`, which catches and logs any exception): weather refresh every 30 s (`RefreshWeather`, `_lastWeatherRefresh`), `DrainMailbox`, `FlushPlayerPoses`, `PlanMovements`, `Simulator.Step`; then, only if there is work, it acquires `IWorldMutationGate` and runs `WritePoses`, `ExecuteArrivals`, `WritePoses`, `PublishScenes`.
- `WorldSimulator` (`api/TRPG.Application.WorldSimulation/Movement/WorldSimulator.cs`) is already a near-pure NPC motion engine: it holds the creature list and a `WeatherShelter` and exposes `Add`, `Remove`, `StateOf`, `IdleLocationIds`, `CreaturesAwaitingJourneyPlanning`, `IsSchedulerSleeping`, `SleepUntilNextRoutineChange`, `StartLocalMove`, `Step`, `Engage`, `Release`. Route planning happens in the runner (`PlanMovements`). Its `TravelGraph` ctor parameter is unused.
- Mailbox: unbounded single-reader `Channel<WorldSimulationMessage>`. `SimulationRequest` carries a closure run with the live `LivePlayerPoses`. `WorldSimulationCoordinator.Request<T>` posts one and awaits it, or runs the work inline when the world has no loaded runner. `Post` silently drops the message when no runner is loaded. `Coordinator.Tick` visits only active worlds (`IWorldClock.GetActiveWorldIds`), so a paused world's mailbox is never drained. `DrainMailbox` loops until the channel is empty.
- Mailbox deadlock rule (applies after PR 0c): a message handler running on the runner must never await another request to the same world. Inside a handler, call commands directly.
- Today the gate is acquired at the boundary in: `GameActionRunner`, `GameTurnStreamer` (3 sites), `PublishSessionStateCommand`, `EndGameSessionCommand`, `GameSessionEndpoints` (line ~78), `CreatureEndpoints` (2 sites), `WorldMutationGatedFunction` (wraps LLM tool calls), `ContinuousWorldProcessor`, and the runner itself.
- Today's continuous pass lives in `api/TRPG/Worlds/ContinuousWorldProcessor.cs` and `ContinuousWorldService.cs`:
  - Frequent lane (5 s): `SyncActiveLocationEffectsCommand`, `SyncActiveLocationRegenerationCommand`, then ambient scene publish per active player (`PublishAmbientSceneCommand`).
  - Location maintenance lane (30 s, the code calls it the routine lane): `SyncActiveLocationRoutinesCommand` (location maintenance plus `EvaluateAmbientEncounterCommand`), an event flush, then the ambient scene publish.
  - Each world pass holds the gate, flushes events inside it, skips a world whose previous pass for the lane is still running, and logs and swallows failures.
  - The three `SyncActiveLocation*Command` handlers live in `api/TRPG.Application.WorldSimulation/Commands/`. They take `WorldId`, `Players` (from `GetActiveLocationPlayersQuery`) and `GameTime`.
- Scene diffing today: `SceneChangePlanner.Plan` (Scenes) returns either a snapshot or granular events from `SceneSemanticComparer` comparisons; `ScenePublisher` + `PublishedSceneRegistry` record the last published scene per player. Callers: `TurnSceneDiffer` (GameTurns; used by `GameActionRunner` and `GameTurnStreamer`), `PublishAmbientSceneCommand`, `PublishSceneCreatureChangesCommand` (used by `PublishSimulationScenesCommand` from the runner), `PublishSessionStateCommand`.
- Granular events live in `api/TRPG.Application.Scenes/Events/`: `CreaturesArrived/Left/Moved/Updated`, `CaravansArrived/Left/Updated`, `WeatherChanged`, `ClockReanchored`, `SceneUpdated` (snapshot). Each carries a per-world state version from `StampWorldStateCommand`; the SPA reducer (`spa/src/features/game/providers/scene-state.ts`) accepts same-version siblings and rejects older versions or other locations.
- Seat occupancy (`IsOccupied`, `IsOccupiedByPlayer`) is part of `NearbyProps`, so any sit/stand currently forces a full snapshot (`SceneSemanticComparer.HasStaticChange`).
- Dependency direction: WorldSimulation references Scenes; Scenes must not reference WorldSimulation or GameTurns.

## Sequence

Each step is its own PR off `main`. Do them in order. Commit, push and open the PR only when the user asks. Present a short plan and get approval before starting each multi-file step (user preference). Update `api/AGENTS.md` in the same PR as any structural or flow change.

### PR 0a: mailbox lifecycle and fairness

- Implement "Lifecycle and fairness": the coordinator drains every loaded runner's mailbox including paused worlds, `Post`/`Request` get explicit outcomes for loading, failed and deleted worlds, the inline no-runner path is removed (load the runner on demand), and `DrainMailbox` is bounded by a message cap and time budget.
- Tests: a request to a paused world completes; a failed world rejects with the defined error; a flood of messages does not stop movement from stepping; a message that advances the clock makes the rest of the tick use the new instant; and a mutating message that commits a creature change and then throws leaves the simulator matching the database; and a failing reload keeps the runner in recovery without processing messages or stepping. The "queued events from the discarded state are not applied after a reload" test needs the queue and applier and lives in I2. The "everything past due runs after a multi-hour skip" acceptance tests live with the schedule in PR A and B.

### PR 0b: move every world writer behind the mailbox

- First audit for world writers that never took the gate (TickerQ jobs such as `GenerateQuestChainJob`, `WorldClockCheckpointService`, any endpoint not listed below) and decide for each whether it posts a message or writes rows that no runner decision reads.
- Audit result: the only ungated writers are `GenerateQuestChainJob` (inserts quest rows and runs an LLM call, so it must not hold a world lease, and no runner decision reads those rows) and `WorldClockCheckpointService` (persists only the world clock, which the runner re-reads after every message). Both stay as they are. `CreateWorldJob` writes a world no runner has loaded. Every other gate site is in the list below.
- Sub-PRs: 0b-1 game-turn actions (`GameActionRunner` is a mailbox request, and sit/stand collapse to one request); 0b-2 `ContinuousWorldProcessor` passes; 0b-3 session start/end and the creature interaction endpoints; 0b-4 the streamer and tool calls.
- Migrate the sites below. While any site is unmigrated, a migrated message may still take the gate inside its closure so the remaining sites stay serialized. `ContinuousWorldProcessor` passes become mailbox messages in this PR.

### PR 0c: delete the mutation gate

- Only when the audit shows no ungated writer and no caller remains. Every site listed above stops calling `IWorldMutationGate.Acquire`. Each one becomes a `WorldSimulationCoordinator` request (or fire-and-forget post) whose closure does the work the lease protected, including the event flush that happened inside the lease.
- `GameActionRunner` becomes the message body: it runs on the runner thread, so handlers keep their current commands but never see the gate. Action handlers that today `Request` first and then `Run` collapse to a single request.
- LLM tool calls (`WorldMutationGatedFunction`) become one request per tool call. The streamer and narration loop hold no world lock while the model generates, so check each call site for reliance on atomicity across several awaits (action then narration then tool calls) and define which parts are one message and which are separate.
- The runner tick stops acquiring the gate. Delete `IWorldMutationGate`, `WorldMutationGate`, its DI registration and `WorldSimulationCoordinatorTests` usages when no caller remains.
- Tests: a boundary-level test per migrated site proving it works with no gate registered, plus a concurrency test that two simultaneous actions on one world run one after the other.
- PR 0b and 0c are likely several PRs by site group: game turns, session start and end, creature endpoints, tool calls.

### PR A: runner owns time, part 1 (effects and regen)

- A small private due schedule in the runner (one next-due game instant per entry compared against the tick's `now`, as in "Time"; no `Stopwatch`, no interface family, no DI list) calls `SyncActiveLocationEffectsCommand` and `SyncActiveLocationRegenerationCommand` every 5 s with the tick's `now` (not `worldClock.GetCurrent`). Active players come from `GetActiveLocationPlayersQuery`; events flush as the processor does today.
- This is deliberate scaffolding: it moves the timer off `ContinuousWorldProcessor` now, and PR I replaces each call with a simulator concern.
- Time skips (wait, sleep, caravan boarding, or a clock jump): an entry whose deadline has passed runs once on that tick and reschedules to `now` plus its interval; it does not replay each missed interval. Owed effects are still computed from elapsed game time by the command. A clock moving backwards (re-anchor) reschedules the entry instead of waiting out the old deadline.
- The "nothing moved" early return must not skip a due entry. A failure in one entry is logged and does not abort movement writes or the other entries. The old per-world overlap guard disappears because ticks are serial.
- `ContinuousWorldProcessor` keeps only the routine lane and the ambient publish.
- Tests: runner-level tests for cadence (not run before due, runs once when due, runs on a tick with no movement events), a multi-hour clock skip runs each entry once, a backwards re-anchor reschedules, failure isolation, and that results are flushed. Keep the existing command tests; move the relevant parts of `ContinuousWorldProcessorTests`.

### PR B: runner owns time, part 2 (location maintenance, ambient encounter, weather refresh)

- Add location maintenance (30 s, wraps `SyncActiveLocationRoutinesCommand`) to the schedule, keeping the order: maintenance, then the event flush so spawn-driven encounters reach clients before the scene, then the ambient publish.
- Replace `_lastWeatherRefresh` with a schedule entry for the exposure refresh.
- The processor and service now only do the ambient publish; they are removed in PR H, not here.
- Tests: the same cadence and skip tests as A for the new entries: a multi-hour clock skip runs location maintenance and the weather refresh once each, with owed effects first and regeneration at the resulting instant.

### PR C: deferred until the diff producers are gone

- Decided: do not move `Scenes/Events/*` ahead of the diff. `SceneChangePlanner`, `SceneSemanticComparer` and `ScenePublisher` (Scenes) construct these events, and Scenes must not reference WorldSimulation, so moving only the events would force the forbidden reverse reference. WorldSimulation already references Scenes, so PRs D to G can emit and project the events where they are, and new events (for example `SeatingChanged`) are defined beside them.
- After PR H deletes the producers, moving the events into WorldSimulation is an optional pure move. Skip it unless something needs it.

### PR D: event queue, projector, scene subscriptions, enter-location message

- Introduce the runner's event queue and the projector (WorldSimulation). Existing granular events flow through it. The projector sends events only to players whose watch matches.
- Add the scene subscriptions and the enter-location mailbox message (Connect, Reconnect, `PlayerMoved`): build `SceneUpdated` via the scene query, then register the watch, in that order.
- Add the explicit refresh hub method and message (hand-add the hub method, see "Generated clients"), answered with a fresh `SceneUpdated`.
- Persisting still happens in the command that made the change (announcement events only). The applier arrives in PR I.
- Implement the projection and delivery parts of "Failure and recovery" and "Client resync" here, before any diff is removed. Resync snapshots and ordinary deltas share one ordered per-connection sending path, because a separate path could let a newer delta arrive first and get the corrective snapshot rejected as old. The commit-ordering, retry and marker parts move to I2 with the applier. A projection failure triggers the automatic resync (never a silent drop). Include the SPA reducer changes from "Client resync" and the resync settings (attempt count and window, both configurable, real time), kept separate from 0a's mailbox budget.
- Tests: reconnect then delta applies cleanly; moving replaces the watch; an event for another location is not sent; refresh returns the current baseline; a throwing projector does not stop the tick and the affected player is resynced with a fresh baseline; a mutating message that commits a write without advancing the world version and then throws resyncs every subscription in that world; a resync snapshot and ordinary deltas go through the same ordered per-connection send, so a newer delta cannot overtake the corrective snapshot. Reducer tests: a same-version `SceneUpdated` replaces state after a partially delivered batch; an older snapshot is rejected; a delta at or below the baseline version is dropped; same-version sibling deltas and `PlayerVitalsUpdated` both apply; a slow resync send does not delay the next tick. The failed-batch and transaction-marker tests belong to I2, where the applier lands; D has no change-event persistence to retry.

### PR E: seating and sneak emit events directly

- Add a creature-generic `SeatingChanged(creatureId, seatId?, posture, placement)` (seat id null on stand; the client derives `isOccupiedByPlayer` by comparing the creature id with the player's id), emitted by the sit and stand handlers inside their mailbox request. The SPA reducer applies seat occupancy plus posture plus placement in one step.
- Sit/stand must stop producing a snapshot, and must not also appear in `CreaturesUpdated`/`CreaturesMoved`. Sneak already needs no new event (`CreaturesUpdated` for the player only).
- Until the diff is deleted, after the handler emits the event it must not also publish via `TurnSceneDiffer.EnqueueChange` for these actions, or the client gets duplicates.
- SPA: reducer case plus tests. For the generated client see "Generated clients" below.

### PR F: runner-sourced creature arrived, left, moved

- `SimEvent`s the runner already produces (via `SimEventLocations.Touched`) drive `CreaturesArrived/Left/Moved` directly instead of re-diffing the location. These are the first events produced by decision logic rather than a command. An NPC arriving at a watched location is caught up (owed effects, then regeneration) before its `CreaturesArrived` is projected; a kill during catch-up is announced instead of the arrival. Remove `PublishSimulationScenesCommand` and `PublishSceneCreatureChangesCommand` when nothing uses them.

### PR G: remaining fields, one category per PR

Equipment, faction names, quest markers, effect visuals, vitals of nearby creatures, weather, caravans and journeys, props. For each: either the owning command announces the change, or it emits "refresh this creature's status" (a targeted `CreaturesUpdated` built with `GetSceneCreaturesQuery`). The diff stays as the safety net for categories not yet migrated. Add a test per category that exercises the real mutation and asserts the event, since a missed announcement means a silently stale client.

### PR H: delete the diff and the continuous pass

- Delete `SceneChangePlanner`, `SceneSemanticComparer` (and tests), `PublishedSceneRegistry`, `ScenePublisher`'s diff path, `TurnSceneDiffer`'s diff calls, `PublishAmbientSceneCommand`, and the processor and service with their DI registration (`api/TRPG/Extensions/ServiceCollectionExtensions.cs`) and tests (`ContinuousWorldProcessorTests`, `ContinuousWorldServiceTests`, `ContinuousWorldWriteVolumeTests`; rewrite the write-volume test against the runner).
- `SceneUpdated` remains only for connect, reconnect, the player entering a new location, the explicit refresh request and the server-triggered resync after a failed projection. Wait/sleep uses the granular `ClockReanchored`.
- Remove the snapshot fallback in the planner (`HasStaticChange`) and add a test that fails if `SceneUpdated` is published from anywhere other than the entry paths and the resync path.
- Update `api/AGENTS.md` ("Continuous world simulation" and "Scenes" sections, Key request flows).

### PR I: the creature simulator (one concern per PR)

- I1: extract `CreatureSimulator` as a thin composition and move `WorldSimulator`'s decision logic into a pure movement concern function, returning events instead of mutating in place. Introduce the `CreatureTickInput` and `SimulationContext` types and the due schedule per creature. Movement events are the existing `SimEvent`s, unrenamed. Rename `WorldSimulationOptions.RouteSearchesPerTick` to `RoutinePlansPerTick` (the budget covers local activity planning too), `CreaturesAwaitingJourneyPlanning` to `CreaturesAwaitingRoutinePlanning`, and `SleepUntilNextRoutineChange` to `DeferUntilNextRoutineChange` (it suspends scheduler work, not the creature). Drop the unused `TravelGraph` ctor parameter. Behavior must not change; the existing simulator tests move to the creature simulator.
- I2: add the applier: persists change events in one batch per tick, before the projector runs. Events leave the queue only after commit, a failed batch blocks the drain, and the marker row detects an uncertain commit. Tests: a failed batch blocks the drain until it commits; a retry after an uncertain commit is detected by the marker and only projected; N consecutive failures reload from the database; queued events from the discarded in-memory state are not applied after a reload. Poses keep their existing flush path unless a measurement says to fold them in.
- I3 to I5: convert regeneration, then active effects, then ambient encounters and location maintenance from "runner calls a command" (PR A and B) to simulator concerns that emit change events. Each converts one scaffolding schedule entry and its command, with a simulator unit test needing no database. Maintenance and encounter decisions may need larger inputs; if a concern needs data the batch load cannot supply cheaply, leave it as a command and note it.
- I3 and I4 must also: tick every creature at a watched location (NPCs included), apply effects before regeneration on the working copy, catch up creatures entering a watched location, and project their change events to `CreatureVitalsUpdated`. Each PR adds an end-to-end test: an NPC with a DoT at the player's location produces `CreatureVitalsUpdated` with lower HP at the next pass, and a 1 HP creature with a DoT that enters the location arrives dead (killed at the catch-up instant, corpse at the new location).
- I3 also renames `PlayerVitalsUpdated` to `CreatureVitalsUpdated` and changes its shape to `(WorldId, LocationId, Entries[CreatureId, CurrentHp, MaximumHp, CurrentAp, MaximumAp, CurrentMp, MaximumMp], Version)`, so the reducer applies the same world, location and version identity checks as the scene deltas and drops another location's events. Touches the contract, `PlayerVitalsChangedEventMapper`, `IGameClient`, the hand-added hub client, the SPA event bus and provider, and about 46 references in tests. The reducer updates the player and `nearbyCreatures` by id and applies the baseline and sibling version rule from "Client resync". A DoT tick that kills a creature emits both this event and `CreaturesUpdated`, and on that tick both payloads describe the same final state at the same version.
- I6: weather as a world-level decision whose change event has no `CreatureId`, so I6 reshapes the hierarchy: `SimEvent(At)` becomes the common base, an abstract `CreatureSimEvent(CreatureId, At)` carries the creature id for the existing movement events and the new creature change events, and the weather event extends `SimEvent` directly. Code that reads `CreatureId` off a base-typed event (`SimEventLocations`, the runner) moves to `CreatureSimEvent`. It flows through the same queue, applier and projector. The clock stays an announcement event: `ClockReanchored` comes from commands that already persisted.
- Stage each conversion: if a command is too tangled to make pure in one PR, the wrapped form from PR A or B stays until it is.

### PR J: single-writer enforcement (end state)

- Make it structural, not a convention. Pick one in the PR plan: (a) world-write commands and the world `DbContext` write path are only resolvable from a scope the runner creates (an internal `IWorldWriteScope`); (b) an architecture test (see `check_architecture` and the existing arch tests) that fails when a world-mutating command is dispatched from an endpoint, hub, background service or tool outside `WorldSimulation`; (c) both.
- Classify every command as world-mutating (runner only) or not (callable anywhere). Pure reads stay open to the boundary; they read committed state and may be slightly behind the runner.
- Non-world state (accounts, session metadata, settings) is out of scope unless it shares rows with world state.
- Dedicated thread versus async loop: the runner is an async single-reader loop, which already guarantees one-at-a-time execution per world. A literal OS thread per world buys nothing with async EF and costs a thread per world (44 runners load at startup). Default: keep the async loop.
- Background producers (TickerQ jobs, `GameTurnStreamer` narration, LLM tool calls) post messages like any other caller.

## Open decisions (ask the user before the relevant PR)

- Name of the simulator class (`CreatureSimulator` is a working name). Event types, decided: `SimEvent(At)` is the common base and keeps its name. `CreatureSimEvent(CreatureId, At)` is an abstract intermediate for creature-bound events (existing movement events, regeneration, effect results), and the creature-less weather event extends `SimEvent` directly. The split happens in I6, when weather first needs it.
- PR A and B: ambient publish currently runs on both lanes (5 s and 30 s); keep it on the processor until PR H, or publish every tick through the existing runner path. Default: keep as is.
- PR D: watched players still come from `GetActiveLocationPlayersQuery` until the scene subscriptions land; later the runner could derive them from `LivePlayerPoses`.
- PR I: whether announcement-producing commands are ever converted to simulator concerns. Default: no; only autonomous world behavior goes through the creature simulator.
- PR I: whether poses fold into the applier's batch or keep their own flush. Default: keep.
- PR J: enforcement mechanism (scope-based, architecture test, or both) and thread versus async loop. Default: both mechanisms, async loop.
- PR 0a: the per-tick message cap, the message time budget, and the consecutive-failure count before a runner reloads from the database.
- PR D: resync attempt count and window (proposed three attempts within five real seconds, configurable). Separate settings from 0a's.
- Decided: wire NPC weather sheltering (standalone PR "apply weather shelter", before I1; see Constraints and gotchas). `WeatherShelter.Apply` redirects an idle job to the creature's first Sleep location but nothing calls it, so `SevereWeatherLocationIds` only forces replanning and `api/AGENTS.md` overstates the behavior today.
- PR D: whether the projector runs on the runner thread or a separate sender (default: runner thread, separate sender if a slow client shows up in measurements).
- Whether the explicit refresh request ships with PR D or later (default: PR D).
- Until PR H, the diff planner can still emit a full-scene event for static changes. That path violates the `SceneUpdated` rule and is tracked as debt; PRs E to G remove each cause.

## Constraints and gotchas

- Apply weather shelter (standalone PR, ahead of I1; proposal, not yet approved for code):
  - Seeding already works: `SimCreatureSeed.SeeksShelter` is `Profession != Guard` in `WorldSimulatorLoader.ToSeed`, and `ShelterLocationId` is the creature's first Sleep job location.
  - Both planners pick the job from the database (`PlanCurrentLocalActivityCommand`, `PlanRoutineJourneyCommand`), so the redirect has to happen there. Move the pure `Apply` into `TRPG.Application.CreatureJobs` (Routing cannot reference WorldSimulation) as `WeatherShelter.Apply(job, shelterLocationId, severeWeatherLocationIds)`.
  - Add optional `ShelterLocationId` and `SevereWeatherLocationIds` properties to both commands. The runner fills them from the simulator, which exposes the set and each creature's shelter. Each command maps its job list through `Apply` before `FindDueJob` / `FindNextTransition`.
  - Return trip, decided: a journey already under way to shelter is not cancelled when the weather clears; the creature finishes it and replans back from the shelter on arrival. `SetExposedLocations` only wakes creatures that are stationary, unfrozen and without a journey, so those replan back immediately when the set changes. Cancelling mid-trip is out of scope.
  - Risk to verify: the sheltered copy keeps the original job `Id`, so `Journey.DestinationJobId` resolves to the unredirected job when the loader rebuilds a journey after a restart. Test a restart mid-shelter journey.
  - Tests, failing first: a journey plans to the Sleep location for an idle job at a severe-weather location; a creature already at its shelter settles locally; a guard and a creature with no Sleep job are not redirected; clearing the weather replans a stationary sheltered creature back; clearing it mid-trip lets the creature finish the trip to shelter and then replan back.
  - Do the `SevereWeatherLocationIds` rename here, since it touches the same call sites: `WeatherShelter._exposedLocationIds`, `SetExposedLocations` and `FindExposedLocationIds` follow it.

- Build with `scripts/build.sh`; run a focused test with `dotnet api/TRPG.Tests/bin/Debug/net10.0/TRPG.Tests.dll -class <FQN> -reporter quiet` (quiet prints nothing on success). `dotnet test` is unsupported. Testcontainers needs Docker (elevated sandbox permission).
- Bugfix rule: write a failing test first. Gameplay workflows: trace UI to command to event to narration to UI state; test both outcomes through deterministic seams.
- Format with `dotnet csharpier format api` and, for the SPA, `pnpm run fmt` (oxfmt, never prettier). Pre-commit hooks run the format checks.
- **Generated clients:** `HEAD`'s REST client was already stale (caravan endpoints removed by #331, still referenced by `caravan-dialog.tsx` and `use-interaction-lifecycle.ts`). A full `pnpm run generate-client` deletes them and breaks the SPA typecheck and ~40 tests. Either fix the caravan SPA code first, or hand-add only the new hub methods to `spa/src/api/signalr-client/TypedSignalR.Client/*`, as #340 did. Do not blindly commit a regenerated REST client.
- Handlers: never `(await Foo()).Bar`, commands take scalar ids, wire models go in `TRPG.Contracts` or the host `Responses`, never serialize raw domain types to the model.
- Exclude `.claude/worktrees/*` from greps. `api/TRPG/Properties/launchSettings.json` and `api/TRPG/appsettings.Development.json` are untracked and must stay out of commits.
- Never commit unless asked. No em dashes in any writing. Concise answers; do not restate the diff afterwards.
- Verify a PR fix in a clean `git worktree add` before calling it done; the user does manual in-game verification.

## Progress checklist

Tick a box in the PR that lands the step. Order is the sequence above; the weather shelter PR can land any time before I1.

- [x] PR 0a: mailbox lifecycle and fairness
- [ ] PR 0b: move every world writer behind the mailbox
  - [x] audit of ungated writers
  - [x] 0b-1 game-turn actions
  - [x] 0b-2 `ContinuousWorldProcessor` passes
  - [x] 0b-3 session start/end and creature endpoints
  - [x] 0b-4 streamer and tool calls
- [x] PR 0c: delete the mutation gate
- [x] PR A: runner owns time, part 1 (effects, regen)
- [x] PR B: runner owns time, part 2 (maintenance, ambient, weather refresh)
- [ ] PR C: deferred (optional pure move of scene events after H)
- [x] PR D: event queue, projector, scene subscriptions, enter message, resync
- [ ] PR E: seating and sneak emit events
- [ ] PR F: runner-sourced arrived, left, moved
- [ ] PR G: remaining fields (equipment, factions, quest markers, effect visuals, nearby vitals, weather, caravans, props), one category per PR
- [ ] PR H: delete the diff and the continuous pass
- [ ] Weather shelter PR (standalone, before I1)
- [ ] PR I1: creature simulator extraction and planning renames
- [ ] PR I2: applier (failed-batch, marker and reload tests)
- [ ] PR I3: regeneration concern and `CreatureVitalsUpdated`
- [ ] PR I4: active effects concern
- [ ] PR I5: ambient encounters and location maintenance
- [ ] PR I6: weather as a world-level decision, `SimEvent` hierarchy split
- [ ] PR J: single-writer enforcement
