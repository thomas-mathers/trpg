# Continuous-Time Implementation Plan

This document is the durable source of truth for the continuous-time project. Update it at the end of every implementation commit so that a new task or compacted conversation can resume from the repository alone.

## Objective

Replace action-banked playtime with a world-owned clock that progresses at exactly 1:1 with real time while the world is active. Waiting, sleeping, walking, and caravan travel advance the whole world immediately. Active locations continue simulating travelers, routines, regeneration, and hostile spawning while the player remains connected.

## Agreed design

### Clock

- Game time belongs to `World`, not `GameSession`.
- Normal progression is exactly 1:1.
- A world progresses while it has an active SignalR connection or is inside the existing 30-second disconnect grace period.
- A hidden browser tab remains connected and therefore keeps the world active.
- Reconnecting during grace continues the same clock without a pause.
- Grace expiry checkpoints and pauses the world.
- Server downtime and fully disconnected time do not advance the world.
- Server shutdown checkpoints active worlds. Startup treats all worlds as paused.
- There are no developer pause or acceleration controls in the initial implementation.
- Use injected `TimeProvider` for all real-time clock behavior.

### Time types

- `GameInstant` represents an absolute fictional date and time.
- `DateTimeOffset` represents real UTC.
- `TimeSpan` represents a duration.
- Persist `GameInstant` as PostgreSQL `timestamp without time zone`.
- The fictional epoch remains `0975-01-01 08:00`.
- Remove the existing 12x conversion and `GameClock.RealTimePerInGameHour`.
- Existing worlds do not require migration compatibility.

### Operation consistency

- Capture one `GameInstant` at the boundary of each deterministic command/query workflow.
- Pass that instant explicitly through nested commands, queries, and events.
- Nested handlers do not independently read the current clock.
- An LLM narration stream does not hold one timestamp for its entire duration. Each tool invocation captures an instant when its deterministic operation begins.
- Explicit time advancement returns a new `GameInstant`; subsequent reconciliation uses the resulting instant.
- Serialize gameplay and background mutations per world without holding a lease during LLM generation, narration streaming, SignalR acknowledgement waits, or unrelated network work.

### Time skips

- Waiting, sleeping, walking, and caravan travel advance the entire world.
- Waiting and sleeping have a maximum duration of 24 in-game hours per operation.
- Travel is not subject to the wait/sleep maximum.
- Large jumps reconcile directly to a plausible current state and do not replay every missed interval.
- Weather produces one plausible current condition.
- Restocking refills toward the normal target once.
- Quest seeding receives one current opportunity.
- Jobs resolve the routine applicable at the result instant.
- Routes resolve their position directly from their timelines.

### Creature state and engagement

- Do not introduce a posture axis in the initial implementation.
- Keep `Sitting` and `Sleeping` in `CreatureState`.
- Rename `CreatureState.Busy` to `CreatureState.Working`.
- Add `Creature.IsEngaged` as an independent routing and routine lock.
- Conversation, trade, NPC quest interaction, caravan panels, encounters, and fights engage their involved creatures.
- Passive inspection does not engage a creature.
- Jobs and routes cannot mutate an engaged creature.
- Releasing a creature reconciles its route and current routine at the supplied `GameInstant`.
- Sitting and sleeping survive disconnects.
- Non-encounter engagement is cleared after disconnect grace and during startup recovery.
- Unresolved encounter and fight participants remain engaged.

### Routes

- `Creature.LocationId` stores the last reached location: the origin of the current leg while in transit.
- A traveler on A to B remains persisted at A until arriving at B.
- Route projection, not `LocationId` alone, determines visible presence.
- A traveler in transit is not visibly present at the leg origin.
- Road scenes may project travelers on the matching connector.
- Arrival persists the destination as the creature's new `LocationId`.
- Engaging any member pauses the entire route-traveler group.
- Route progress must not jump forward during engagement.
- Finite travel resumes from its suspended progress.
- Job-backed creatures reevaluate the routine applicable when released.

### Background simulation

- Use ASP.NET `BackgroundService`, not TickerQ.
- Use a five-second cadence for clock checkpointing and passive regeneration.
- Use a 30-second cadence for active-scene simulation.
- Process unique `(WorldId, LocationId)` pairs rather than connections.
- Skip overlapping passes rather than queueing stale ticks.
- Log failures and retry naturally on the next tick.
- The fast lane handles travelers and caravans.
- The slow lane handles jobs, shops, doors, weather, spawning, restocking, quest seeding, corpse cleanup, and alerted-state reset when due.
- Routes use exact timeline arithmetic. Jobs and shops primarily use hourly boundaries.

### Regeneration

- Regeneration ticks every five seconds while the world is active.
- HP initially regenerates 5% of maximum per tick.
- AP initially regenerates 10% of maximum per tick.
- MP initially regenerates 5% of maximum per tick.
- Rates remain independently configurable.
- Process only relevant creatures below at least one resource maximum.
- Preserve fractional elapsed time between complete ticks.
- Explicit time skips apply elapsed regeneration immediately.
- Stat-sensitive operations catch up involved creatures exactly to their captured instant.
- Active fight participants do not receive background passive regeneration because combat advances only through submitted actions.
- Finishing or fleeing combat resets surviving participants' regeneration anchors so combat time is not banked.

### Encounters and player safety

- Hostile groups may spawn while the player remains in a spawn-enabled location.
- A new hostile group may be evaluated and presented as an encounter immediately.
- Ambient processing never resolves a combat round or applies damage.
- Ambient spawning evaluates encounter groups only; it does not trigger traps, trespassing, overdue room-key confrontations, or routine guard checks.
- Encounters and fights persist across disconnects.
- Ending a game session no longer abandons an active fight.

### SignalR and interface

- Ambient scene updates send a full authoritative snapshot only after player-visible semantic change.
- Scene/state snapshots carry a persisted monotonically increasing version.
- Targeted vitals updates also carry ordering information so stale state cannot win.
- Ambient updates do not wait for client acknowledgement.
- Player-action events retain acknowledgement only where causal ordering before narration requires it.
- The client derives the visible clock from a game-time and real-time anchor and updates it once per second.
- Wait and sleep calculations use the derived current minute rather than a stale scene hour.

## Explicitly deferred

- A separate `CreaturePosture` axis.
- Special hidden-tab buffering, visibility handling, or refresh-on-return behavior.
- Deterministic arrival and departure text notifications.
- Narration-time ambient update deferral and coalescing.
- New metrics, counters, histograms, or dashboards beyond ordinary structured logging.
- Existing-world conversion or backward compatibility.
- Developer clock pause, acceleration, or arbitrary time-setting controls.

## Commit protocol

Implementation uses multiple focused commits. Every commit must leave its touched area internally coherent, and the final commit of each milestone must leave the solution buildable with the milestone's required checks passing.

### Progress commit format

Use the repository's imperative subject style. Do not prefix subjects with `WIP`.

```text
<Imperative summary of the concrete change>

<Why this change belongs in the milestone and any important design detail.>

Tests:
- <commands run or "Not run" with a reason>

Continuous-Time-Milestone: <two-digit number>-<slug>
Milestone-Status: Progress
```

Example:

```text
Add GameInstant persistence conversion

Store fictional instants as timestamp without time zone so they cannot be
confused with real UTC anchors.

Tests:
- dotnet test api/TRPG.Tests --filter FullyQualifiedName~GameInstant

Continuous-Time-Milestone: 01-time-foundation
Milestone-Status: Progress
```

### Milestone-closing commit format

The subject must clearly state that the commit completes the milestone:

```text
Complete continuous-time milestone <number>: <milestone name>

<Concise summary of the completed milestone and its externally meaningful result.>

Tests:
- <commands run>

Continuous-Time-Milestone: <two-digit number>-<slug>
Milestone-Status: Complete
```

Example:

```text
Complete continuous-time milestone 01: time foundation

Finish GameInstant arithmetic, calendar integration, persistence conversion,
and focused test coverage without changing runtime clock ownership.

Tests:
- dotnet test api/TRPG.Tests --filter FullyQualifiedName~GameInstant
- dotnet csharpier check .

Continuous-Time-Milestone: 01-time-foundation
Milestone-Status: Complete
```

If a milestone needs only one commit, use the milestone-closing format for that commit. Do not begin the next milestone until the closing commit exists. Update this document's status and continuation notes in every milestone-closing commit.

### Commit boundaries

- Do not mix unrelated cleanup into continuous-time commits.
- Do not include questionnaire/site artifacts in implementation commits.
- Generated migrations and the model changes they represent belong in the same commit.
- Generated SPA clients and the server contracts that require them should be kept close enough that the milestone closes with both synchronized.
- A progress commit may use a narrower test command, but a closing commit must run every check listed for that milestone.
- Record any intentionally skipped check in the commit body with its reason.

## Milestone checklist

### Milestone 01 — Time foundation

Status: Complete

- [x] Add `GameInstant` to the domain.
- [x] Enforce `DateTimeKind.Unspecified`.
- [x] Add instant/duration arithmetic and comparisons.
- [x] Add EF conversion support.
- [x] Convert calendar helpers to accept `GameInstant`.
- [x] Add focused domain and persistence tests.
- [x] Keep existing runtime clock ownership unchanged in this milestone.
- [x] Run focused tests.
- [x] Run CSharpier check.
- [x] Create milestone-closing commit.

Exit condition: `GameInstant` is usable and tested without changing live game behavior.

### Milestone 02 — Absolute-time model conversion

Status: Complete

- [x] Replace persisted absolute `TimeSpan` fields with `GameInstant`.
- [x] Remove `*AtPlaytime`, `*UntilPlaytime`, and `*SyncPlaytime` naming.
- [x] Convert related commands, queries, results, events, builders, and tests.
- [x] Convert route timelines and scheduling arithmetic.
- [x] Remove 12x duration scaling.
- [x] Add the destructive database migration.
- [x] Verify no absolute fictional timestamp remains represented as `TimeSpan`.
- [x] Run the backend test suite.
- [x] Run CSharpier check.
- [x] Create milestone-closing commit.

Exit condition: absolute fictional time uses `GameInstant` throughout the model and application contracts.

### Milestone 03 — World-owned continuous clock

Status: Complete

- [x] Add `IWorldClock` with current, resume, pause, advance, and checkpoint operations.
- [x] Add active-world in-memory anchors.
- [x] Inject `TimeProvider`.
- [x] Integrate SignalR connection and 30-second grace lifecycle.
- [x] Support multiple connections without resetting the anchor.
- [x] Remove time ownership from `GameSession`.
- [x] Remove per-message advancement.
- [x] Checkpoint on shutdown and treat worlds as paused on startup.
- [x] Add fake-time lifecycle tests.
- [x] Run relevant hub/session tests.
- [x] Run CSharpier check.
- [x] Create milestone-closing commit.

Exit condition: connected worlds advance 1:1 and paused worlds do not advance.

### Milestone 04 — Operation timestamps and explicit skips

Status: Complete

- [x] Capture one instant per deterministic operation.
- [x] Pass instants explicitly through nested application work.
- [x] Capture LLM tool time at tool invocation, not narration-stream start.
- [x] Convert waiting and sleeping.
- [x] Enforce the 24-hour wait/sleep maximum.
- [x] Convert walking and caravan travel.
- [x] Reconcile the world at the resulting instant.
- [x] Add bounded large-jump behavior.
- [x] Add consistency and time-skip tests.
- [x] Run affected backend tests.
- [x] Run CSharpier check.
- [x] Create milestone-closing commit.

Exit condition: ordinary operations use one instant and explicit skips correctly produce a later instant.

### Milestone 05 — Engagement and route suspension

Status: Complete

- [x] Add `Creature.IsEngaged`.
- [x] Rename `Busy` to `Working` across server, wire contracts, SPA, and tests.
- [x] Add shared engage and release commands.
- [x] Add engagement and release application events.
- [x] Add `RouteTraveler.PausedAtGameTime`.
- [x] Pause a group when its first member becomes engaged.
- [x] Resume only after every member is released.
- [x] Shift the route timeline by the paused duration.
- [x] Reconcile released job-backed creatures at the release instant.
- [x] Integrate conversations.
- [x] Integrate trade and NPC quest interactions.
- [x] Integrate caravans.
- [x] Integrate encounters and fights.
- [x] Add disconnect and startup cleanup.
- [x] Preserve unresolved encounter engagement.
- [x] Add engagement, grouped-route, and routine-resumption tests.
- [x] Run backend and relevant SPA tests.
- [x] Run formatting and type checking.
- [x] Create milestone-closing commit.

Exit condition: active interactions pin their subjects and release them into the correct current routine without route jumps.

### Milestone 06 — Simulation lanes

Status: Complete

- [x] Extract the frequent traveler and caravan pass.
- [x] Split due slow-lane reconciliation by subsystem.
- [x] Remove `LocationCatchUpCache`.
- [x] Make subsystem synchronization idempotent at a supplied instant.
- [x] Process unique world/location pairs.
- [x] Preserve exact route projection and leg-origin persistence.
- [x] Add player-visible semantic scene comparison.
- [x] Test watched travelers arriving, departing, and traversing roads.
- [x] Run location simulation and routing tests.
- [x] Run CSharpier check.
- [x] Create milestone-closing commit.

Exit condition: active locations can be safely reconciled repeatedly and travelers visibly progress.

### Milestone 07 — Background service

Status: Not started

- [ ] Add a thin ASP.NET `BackgroundService` timer wrapper.
- [ ] Extract a timer-independent continuous-world processor.
- [ ] Add five-second and 30-second cadences.
- [ ] Add per-world mutation serialization.
- [ ] Add per-world overlap skipping.
- [ ] Log failures and retry on the next cadence.
- [ ] Ensure failures never disconnect clients.
- [ ] Add deterministic processor and overlap tests.
- [ ] Run hosted-service and simulation tests.
- [ ] Run CSharpier check.
- [ ] Create milestone-closing commit.

Exit condition: active-world simulation runs continuously without TickerQ or overlapping stale passes.

### Milestone 08 — Passive regeneration

Status: Not started

- [ ] Replace hourly regeneration settings with five-second tick settings.
- [ ] Implement HP 5%, AP 10%, and MP 5% defaults.
- [ ] Preserve fractional elapsed tick time.
- [ ] Query only relevant non-full creatures.
- [ ] Catch up exactly before stat-sensitive operations.
- [ ] Apply regeneration over explicit time skips.
- [ ] Suppress background regeneration for active fight participants.
- [ ] Reset combatant anchors when combat ends or is fled.
- [ ] Add targeted versioned player-vitals SignalR updates.
- [ ] Add formula, command, background, combat, and transport tests.
- [ ] Run backend and relevant SPA tests.
- [ ] Run formatting and type checking.
- [ ] Create milestone-closing commit.

Exit condition: active-world regeneration is visible every five seconds and cannot be exploited by idling in combat.

### Milestone 09 — Ambient encounters and versioned state

Status: Not started

- [ ] Add a persisted monotonically increasing world state version.
- [ ] Include the version and clock anchor in full scene snapshots.
- [ ] Reject stale snapshots and vitals updates on the client.
- [ ] Send full ambient scenes only after semantic change.
- [ ] Make spawner synchronization report newly created groups.
- [ ] Evaluate newly spawned hostile groups in occupied locations.
- [ ] Publish ambient encounters without resolving attacks or damage.
- [ ] Keep non-spawn encounter categories player-action-driven.
- [ ] Preserve and republish encounters across reconnects.
- [ ] Remove session-end fight abandonment.
- [ ] Add semantic comparison, ordering, spawn, safety, and reconnect tests.
- [ ] Run backend and relevant SPA tests.
- [ ] Run formatting and type checking.
- [ ] Create milestone-closing commit.

Exit condition: ambient scene and encounter state reaches the client safely and monotonically without automatic harm.

### Milestone 10 — SPA continuous-time experience

Status: Not started

- [ ] Regenerate HTTP and SignalR clients.
- [ ] Add client-derived clock state from server anchors.
- [ ] Display custom calendar date and `HH:mm:ss`.
- [ ] Handle date rollover.
- [ ] Update wait and sleep calculations to use derived time.
- [ ] Integrate validated begin/end interaction calls in panels.
- [ ] Apply targeted vitals updates.
- [ ] Add client clock, version, interaction, and vitals tests.
- [ ] Run SPA formatting, type checking, and tests.
- [ ] Run affected backend transport tests.
- [ ] Create milestone-closing commit.

Exit condition: the complete continuous-time loop is observable and controllable from the SPA.

### Milestone 11 — Balance, hardening, and documentation

Status: Not started

- [ ] Review route distance and movement speed.
- [ ] Review caravan travel and dwell times.
- [ ] Review jobs, patrols, shops, weather, restocking, spawning, quest seeding, bookings, jail, rested effects, doors, corpse cleanup, and alerted resets.
- [ ] Remove temporary compatibility code.
- [ ] Review background queries and write volume.
- [ ] Run the complete backend test suite.
- [ ] Run CSharpier check.
- [ ] Run SPA formatting, type checking, and tests.
- [ ] Update `AGENTS.md` request-flow descriptions.
- [ ] Confirm every earlier milestone has a closing commit.
- [ ] Create milestone-closing commit.

Exit condition: durations are intentionally balanced, all verification passes, and repository documentation matches the shipped architecture.

## Continuation notes

Current milestone: 07 — Background service

Current status: Not started

Last completed milestone: 06 — Simulation lanes

Next action: Add the thin ASP.NET `BackgroundService` timer wrapper and a timer-independent continuous-world processor that calls `SyncActiveLocationTravelersCommand` on the 5-second cadence and `SyncActiveLocationRoutinesCommand` on the 30-second cadence, with per-world mutation serialization and overlap skipping. Do not add regeneration or ambient encounter work.

Milestone 06 progress: `CatchUpLocationCommand` is now a thin composition of two lanes. `SyncLocationTravelersCommand` (fast lane) ensures the recurring route projection exists, materializes scheduled journeys through the location, and relocates every route-backed traveler to its projected leg origin or stop. `SyncLocationRoutinesCommand` (slow lane) runs weather, `SyncLocationJobsCommand` (job resolution, workstation occupancy), front-door locks, spawning, restocking, and quest seeding in that order. `SyncActiveLocationTravelersCommand` and `SyncActiveLocationRoutinesCommand` take a world's watching players and process each unique location once, on behalf of the highest-level player. `LocationCatchUpCache` and its once-per-in-game-hour gate are removed; `ResetAlertedCreaturesCommand` no longer evicts anything, `RefreshSceneResult.Refreshed` is gone, and `CatchUpLocationCommand` no longer carries a redundant `CurrentDate`. `GetSceneQuery` no longer lists creatures in the `Walking` state, so a traveler in transit is invisible at its persisted leg origin and appears only once the fast lane persists its arrival. `SceneSemanticComparer.HasPlayerVisibleChange` compares two scenes ignoring the clock, vital meters, and caravan departure countdowns, ordering, and duplicates.

Milestone 06 decisions: Idempotence at a supplied instant comes from each subsystem's own persisted schedule (weather `NextChangeGameTime`, spawner, restock, and quest-seed `LastSyncGameTime`) or from deriving state purely from the instant (routes, jobs, door locks; door locks were already re-synced on every move attempt), so no shared gate or new table was added. The slow lane is self-sufficient: `SyncLocationJobsCommand` now unions traveler members through the location (new `GetTravelerCreatureIdsByLocationIdQuery`) instead of receiving the fast lane's materialized ids. Running the routine lane on every scene refresh costs more queries than the old hourly gate; milestone 07 chooses the cadence and milestone 11 reviews query volume. Hiding Walking creatures is limited to the scene projection; other creature queries (encounters, conversations) are unchanged. Caravan `MinutesUntilDeparture` is treated as clock-derived and excluded from semantic scene comparison. The semantic comparer is not wired into any transport yet; milestone 09 uses it.

Milestone 06 validation: `dotnet build api/TRPG.Tests/TRPG.Tests.csproj --no-restore --verbosity quiet` passed. The LocationSimulation namespaces (132 tests), GameTurns namespaces (113 tests), and Routing namespaces (16 tests) passed through the xUnit executable with Docker access, including the new traveler-lane, routine-lane, active-location, scene-visibility, and semantic-comparer suites. The complete backend suite ran 2740 tests twice with 2739 passing each time; the single failure, `PendingSessionEndRegistryTests.Disconnect_PausesAndEndsSession_AfterLastConnectionGraceExpires`, passed 5 of 5 in isolation and also failed on a clean worktree of the milestone 05 closing commit, where a second unrelated test (`PurchaseCaravanTicketCommandHandlerTests.Handle_ReturnsTravelSuspended_WithoutChargingGold_DuringSnow`) also failed intermittently. The registry test polls for the session row to disappear and then asserts the world was paused, but `End` deletes the session before pausing the world, so it races under full-suite load. `dotnet csharpier check .` passed. The pre-closing status and diff inspection contained only intended milestone files; design-questionnaire artifacts remained excluded.

Milestone 05 progress: `Creature.IsEngaged` and `RouteTraveler.PausedAtGameTime` are persisted by the `AddCreatureEngagement` migration. Shared engage/release commands publish application events. Engaging any route member pauses the group at the first engagement instant; position projection remains frozen there; releasing the final member shifts the route start by the full paused duration before current-job reconciliation. Route and job synchronization skip engaged creatures. Conversations engage and release both participants. Validated server begin/end operations cover trade and NPC quest panels, while caravan operations engage the player and every route-group member. Encounters engage their deterministic participant set when published, fights preserve engagement across encounter transitions, and resolution releases only participants not retained by another active encounter. Session-end and startup cleanup release abandoned interactions while preserving unresolved encounter participants. `CreatureState.Busy` is now `Working` in domain, host wire types, generated SignalR TypeScript, and tests.

Milestone 05 decisions: Engagement remains one persisted exclusivity flag rather than an ownership model. Route pause state belongs to the traveler group, and the route timeline moves forward only when the final engaged member is released. Trade and NPC quest panels use shared validated creature-interaction endpoints; the SPA invokes them in milestone 10 as already planned. Caravan interaction has group-specific endpoints because presence depends on the route projection. Session end no longer abandons active fights because unresolved encounter engagement must survive disconnects; the later milestone 09 reconnect work will republish those encounters. Startup recovery is best-effort so unavailable persistence or logging cannot prevent host startup or generated-client production.

Milestone 05 validation: `dotnet build api/TRPG.Tests/TRPG.Tests.csproj --no-restore --verbosity quiet` passed with pre-existing warnings. Focused creature engagement, caravan interaction, encounter publication and cleanup, fight completion, session end, conversation, route synchronization, job scheduling, and job execution suites passed through the xUnit executable with Docker access. The first complete backend run exposed a missing test registration for the end-conversation tool; after adding the production-equivalent registration and repairing the delegate invocation, `dotnet api/TRPG.Tests/bin/Debug/net10.0/TRPG.Tests.dll -reporter quiet` passed. `pnpm generate-client` regenerated the HTTP and SignalR clients; its first OpenAPI-host attempt exposed startup recovery's logging-sink failure path, which was made non-fatal before the successful rerun. `pnpm run fmt:check`, `pnpm run typecheck`, and `pnpm test` passed all 173 SPA tests. `dotnet csharpier check .` passed. The final source inventory contains no non-migration `Busy` references.

Milestone 04 progress: Explicit advances are world-scoped and return the resulting instant. Wait and sleep reject non-positive durations and durations over 24 hours, apply regeneration, and reconcile the active location at the returned instant. Walking captures time when the `move` tool is invoked and uses that instant for destination validation and departure interception before advancing to arrival; caravan boarding likewise validates at one captured instant and advances without a duration cap. Encounter actions and combat now capture one instant at their turn boundary and pass it through nested flee, relocation, fight-start, combat-round, and fight-end work. Location catch-up already implements bounded large jumps by resolving weather, restocking, spawning, quest seeding, jobs, and routes once at the supplied current instant rather than replaying missed intervals.

Milestone 04 decisions: Time advancement is keyed directly by world rather than resolving a session inside the command. Wait and sleep cap each operation at 24 hours, while walking and caravan travel retain their calculated unbounded duration. Wait and sleep reconcile the occupied location after the skip; movement reconciles the destination through the existing arrival event. Large jumps reuse the existing direct catch-up behavior, which gives weather one current transition, fills stock and spawns toward their targets once, gives quest seeding one opportunity, and resolves jobs and routes at the resulting instant without replaying elapsed intervals. LLM tools capture time inside each invocation, and encounter/combat turn handlers pass one captured instant through all nested deterministic work.

Milestone 04 validation: `dotnet build api/TRPG.Tests/TRPG.Tests.csproj --no-restore --verbosity quiet` passed with pre-existing warnings. Focused wait, sleep, movement, timed-door, combat lifecycle, flee, guard, hostile encounter, and fight-completion suites passed through the xUnit executable with Docker access. The first full-suite run found three tests that depended on removed nested clock reads; they were updated to supply or assert the explicit operation instant, and the complete backend suite then passed with `dotnet api/TRPG.Tests/bin/Debug/net10.0/TRPG.Tests.dll -reporter quiet`. `dotnet csharpier check .` passed. The final status and diff inspection contained only intended milestone files; design-questionnaire artifacts remained excluded.

Milestone 03 decisions: `IWorldClock` is a shared application contract implemented by Worlds as a singleton with per-world serialized state. An active anchor pairs a fictional `GameInstant` with `TimeProvider.GetUtcNow()` and derives time 1:1; persisted world time remains authoritative while paused. Explicit advances persist immediately and re-anchor active worlds without stopping them. SignalR tracks connections per session and activity across every session in a world: the first connection resumes the world, reconnecting cancels pending end work, and only the last disconnected session pauses after the 30-second grace. Startup creates no active anchors, while host shutdown checkpoints active worlds. `GameSession` no longer owns time, narrated messages no longer advance it, and the migration drops `game_sessions.game_time`.

Milestone 03 validation: `dotnet build api/TRPG.Tests/TRPG.Tests.csproj --no-restore --verbosity quiet` passed with pre-existing warnings. Focused world-clock, fake-time SignalR lifecycle, hub/session, movement, sleep, encounter, and fight suites passed through the xUnit executable with Docker access. The complete backend suite passed with `dotnet api/TRPG.Tests/bin/Debug/net10.0/TRPG.Tests.dll -reporter quiet`. `dotnet csharpier check .` passed. The pre-closing status and diff inspection contained only intended milestone files; design-questionnaire artifacts remained excluded.

Milestone 02 decisions: Persisted absolute timestamps and their application contracts use epoch-anchored `GameInstant` values and `GameTime` naming. Route, regeneration, and schedule arithmetic now use direct fictional durations; the 12x conversion and `RealTimePerInGameHour` bridge are removed. `ConvertAbsoluteTimeToGameInstant` intentionally drops the old interval columns and adds `timestamp without time zone` replacements because existing-world compatibility is deferred. Required operation contracts receive explicit instants, while domain timestamps that previously relied on `TimeSpan.Zero` default to `GameClock.Epoch`.

Milestone 02 validation: `dotnet build api/TRPG.Tests/TRPG.Tests.csproj --no-restore --verbosity quiet` passed with pre-existing warnings. Focused time, routing, scheduling, regeneration, encounter, and conversation regression suites passed through the xUnit executable with Docker access. The complete backend suite passed with `dotnet api/TRPG.Tests/bin/Debug/net10.0/TRPG.Tests.dll -reporter quiet`. `dotnet csharpier check .` passed. Source and test inventories contain no non-migration `Playtime` names, legacy scaling symbols, or persisted absolute fictional timestamps represented as `TimeSpan`.

Milestone 01 decisions: `GameInstant` is a scalar domain record struct over an unspecified-kind `DateTime`; its constructor rejects local and UTC values. `GameClock` exposes the fictional epoch and bridges legacy banked playtime to `GameInstant` while retaining the existing 12x runtime behavior until milestone 02. EF applies a global `GameInstant` conversion to `timestamp without time zone`.

Milestone 01 validation: `dotnet build api/TRPG.Tests/TRPG.Tests.csproj --no-restore --verbosity quiet` passed with pre-existing warnings. The focused xUnit runner invocation for `GameInstantTests`, `GameClockTests`, and `GameInstantValueConverterTests` passed all 23 tests, including a Testcontainers-backed PostgreSQL round trip. `dotnet csharpier check .` passed.

Known unrelated working-tree files: the untracked `design-questionnaire*` directories and archive belong to the earlier design questionnaire and must not be included in implementation commits.
