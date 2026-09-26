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

Status: In progress

- [x] Replace persisted absolute `TimeSpan` fields with `GameInstant`.
- [x] Remove `*AtPlaytime`, `*UntilPlaytime`, and `*SyncPlaytime` naming.
- [x] Convert related commands, queries, results, events, builders, and tests.
- [x] Convert route timelines and scheduling arithmetic.
- [x] Remove 12x duration scaling.
- [x] Add the destructive database migration.
- [x] Verify no absolute fictional timestamp remains represented as `TimeSpan`.
- [ ] Run the backend test suite.
- [ ] Run CSharpier check.
- [ ] Create milestone-closing commit.

Exit condition: absolute fictional time uses `GameInstant` throughout the model and application contracts.

### Milestone 03 — World-owned continuous clock

Status: Not started

- [ ] Add `IWorldClock` with current, resume, pause, advance, and checkpoint operations.
- [ ] Add active-world in-memory anchors.
- [ ] Inject `TimeProvider`.
- [ ] Integrate SignalR connection and 30-second grace lifecycle.
- [ ] Support multiple connections without resetting the anchor.
- [ ] Remove time ownership from `GameSession`.
- [ ] Remove per-message advancement.
- [ ] Checkpoint on shutdown and treat worlds as paused on startup.
- [ ] Add fake-time lifecycle tests.
- [ ] Run relevant hub/session tests.
- [ ] Run CSharpier check.
- [ ] Create milestone-closing commit.

Exit condition: connected worlds advance 1:1 and paused worlds do not advance.

### Milestone 04 — Operation timestamps and explicit skips

Status: Not started

- [ ] Capture one instant per deterministic operation.
- [ ] Pass instants explicitly through nested application work.
- [ ] Capture LLM tool time at tool invocation, not narration-stream start.
- [ ] Convert waiting and sleeping.
- [ ] Enforce the 24-hour wait/sleep maximum.
- [ ] Convert walking and caravan travel.
- [ ] Reconcile the world at the resulting instant.
- [ ] Add bounded large-jump behavior.
- [ ] Add consistency and time-skip tests.
- [ ] Run affected backend tests.
- [ ] Run CSharpier check.
- [ ] Create milestone-closing commit.

Exit condition: ordinary operations use one instant and explicit skips correctly produce a later instant.

### Milestone 05 — Engagement and route suspension

Status: Not started

- [ ] Add `Creature.IsEngaged`.
- [ ] Rename `Busy` to `Working` across server, wire contracts, SPA, and tests.
- [ ] Add shared engage and release commands.
- [ ] Add engagement and release application events.
- [ ] Add `RouteTraveler.PausedAtGameTime`.
- [ ] Pause a group when its first member becomes engaged.
- [ ] Resume only after every member is released.
- [ ] Shift the route timeline by the paused duration.
- [ ] Reconcile released job-backed creatures at the release instant.
- [ ] Integrate conversations.
- [ ] Integrate trade and NPC quest interactions.
- [ ] Integrate caravans.
- [ ] Integrate encounters and fights.
- [ ] Add disconnect and startup cleanup.
- [ ] Preserve unresolved encounter engagement.
- [ ] Add engagement, grouped-route, and routine-resumption tests.
- [ ] Run backend and relevant SPA tests.
- [ ] Run formatting and type checking.
- [ ] Create milestone-closing commit.

Exit condition: active interactions pin their subjects and release them into the correct current routine without route jumps.

### Milestone 06 — Simulation lanes

Status: Not started

- [ ] Extract the frequent traveler and caravan pass.
- [ ] Split due slow-lane reconciliation by subsystem.
- [ ] Remove `LocationCatchUpCache`.
- [ ] Make subsystem synchronization idempotent at a supplied instant.
- [ ] Process unique world/location pairs.
- [ ] Preserve exact route projection and leg-origin persistence.
- [ ] Add player-visible semantic scene comparison.
- [ ] Test watched travelers arriving, departing, and traversing roads.
- [ ] Run location simulation and routing tests.
- [ ] Run CSharpier check.
- [ ] Create milestone-closing commit.

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

Current milestone: 02 — Absolute-time model conversion

Current status: In progress

Last completed milestone: 01 — Time foundation

Next action: Run the complete backend test suite, repair any behavioral regressions, run CSharpier, and close milestone 02 if every check passes.

Milestone 02 progress: Persisted absolute timestamps and their application contracts now use `GameInstant` and `GameTime` naming. Route and schedule arithmetic uses direct fictional durations, the 12x conversion and `RealTimePerInGameHour` bridge are removed, and `ConvertAbsoluteTimeToGameInstant` destructively replaces the old interval columns with `timestamp without time zone` columns. Source and test inventories contain no non-migration `Playtime` names or legacy scaling symbols.

Milestone 02 validation so far: `dotnet build api/TRPG.Tests/TRPG.Tests.csproj --no-restore --verbosity quiet` passed with pre-existing warnings. Focused `GameClockTests`, `RouteTimelineTests`, `CreatureJobSchedulingTests`, `RecurringSchedulingTests`, and `GameInstantValueConverterTests` passed through the xUnit executable with Docker access.

Milestone 01 decisions: `GameInstant` is a scalar domain record struct over an unspecified-kind `DateTime`; its constructor rejects local and UTC values. `GameClock` exposes the fictional epoch and bridges legacy banked playtime to `GameInstant` while retaining the existing 12x runtime behavior until milestone 02. EF applies a global `GameInstant` conversion to `timestamp without time zone`.

Milestone 01 validation: `dotnet build api/TRPG.Tests/TRPG.Tests.csproj --no-restore --verbosity quiet` passed with pre-existing warnings. The focused xUnit runner invocation for `GameInstantTests`, `GameClockTests`, and `GameInstantValueConverterTests` passed all 23 tests, including a Testcontainers-backed PostgreSQL round trip. `dotnet csharpier check .` passed.

Known unrelated working-tree files: the untracked `design-questionnaire*` directories and archive belong to the earlier design questionnaire and must not be included in implementation commits.
