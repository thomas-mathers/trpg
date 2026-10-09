# Unified Travel Journeys Checklist

## Working agreement

- Complete milestones strictly in order.
- At the end of every milestone, run the relevant build and tests; the repository must be green before starting the next milestone.
- Intermediate commits are allowed. The final commit of each milestone must use the exact subject `milestone N: <clear completed outcome>`.
- This work requires regenerated worlds. Do not add a legacy route, pose, or generated-world compatibility path.

## Target data model

### `TravelCircuit`

| Field | Type | Rules |
| --- | --- | --- |
| `Id` | `Guid` | Primary key |
| `WorldId` | `Guid` | Required |
| `Name` | `string` | Required |

`TravelCircuit` is an immutable reusable cyclic itinerary. It has no loop flag: closure is an invariant of its ordered legs.

### `TravelCircuitLeg`

| Field | Type | Rules |
| --- | --- | --- |
| `Id` | `Guid` | Primary key |
| `TravelCircuitId` | `Guid` | Required; foreign key to `TravelCircuit` |
| `Index` | `int` | Required; unique with `TravelCircuitId` |
| `FromNodeId` | `Guid` | Required; foreign key to `TravelNode` |
| `ToNodeId` | `Guid` | Required; foreign key to `TravelNode` |
| `ConnectorId` | `Guid` | Required; foreign key to `Connector` |
| `DwellAfter` | `TimeSpan` | Required; zero means continue immediately |

Every leg represents one directed connector traversal. The final leg's `ToNodeId` must equal the first leg's `FromNodeId`; return travel is always an explicit leg. Distance and path remain owned by the referenced connector.

### `Journey`

| Field | Type | Rules |
| --- | --- | --- |
| `Id` | `Guid` | Primary key |
| `WorldId` | `Guid` | Required |
| `TravelCircuitId` | `Guid?` | Optional source template |
| `Purpose` | `string?` | Group-travel scene/narration context |
| `ArrivalActivity` | `CreatureActivity?` | Activity during a circuit dwell |
| `DestinationJobId` | `Guid?` | Routine-job completion context |
| `DestinationPropId` | `Guid?` | Target seat, bed, or workstation |
| `Status` | `JourneyStatus` | `Planned`, `Traveling`, `Dwelling`, `Completed`, or `Cancelled` |
| `PlannedAt` | `GameInstant` | Required |
| `DepartureAt` | `GameInstant` | Required; calculated from route length and stagger |
| `CheckpointLegIndex` | `int` | Required; last durable route-progress baseline |
| `CheckpointLegProgressMeters` | `double` | Required; meters into checkpoint leg |
| `CheckpointedAt` | `GameInstant` | Required |
| `PausedAt` | `GameInstant?` | Set while the journey is paused |

`Journey` is the durable execution of a one-off or circuit-derived trip. It never stores speed. The effective pace is the minimum current movement speed of its active members. Its checkpoint fields are a time-based baseline, not per-tick state.

### `JourneyLeg`

| Field | Type | Rules |
| --- | --- | --- |
| `Id` | `Guid` | Primary key |
| `JourneyId` | `Guid` | Required; foreign key to `Journey` |
| `Index` | `int` | Required; unique with `JourneyId` |
| `FromNodeId` | `Guid` | Required |
| `ToNodeId` | `Guid` | Required |
| `ConnectorId` | `Guid` | Required |
| `Distance` | `double` | Required planning-time snapshot |
| `Path` | `Polyline` | Required directed planning-time snapshot |
| `DwellAfter` | `TimeSpan` | Required; copied from a circuit leg or zero for ordinary traversal |

The copied `Path` is directed from `FromNodeId` to `ToNodeId`, including reverse traversal of bidirectional point connectors. It is immutable after creation so an active journey never changes if graph topology later changes.

### `JourneyMember`

| Field | Type | Rules |
| --- | --- | --- |
| `Id` | `Guid` | Primary key |
| `JourneyId` | `Guid` | Required; foreign key to `Journey` |
| `CreatureId` | `Guid` | Required; foreign key to `Creature` |

`JourneyId + CreatureId` is unique. A filtered unique constraint on `CreatureId` across `Planned`, `Traveling`, and `Dwelling` journeys prevents concurrent active membership.

### `Creature`

| Field | Type | Rules |
| --- | --- | --- |
| `CurrentTravelNodeId` | `Guid?` | The routing node last reached by a stationary creature |

This is updated when a creature settles at an anchor or ordinary node. A creature at a prop uses its prop's approach node as its routing origin, even when its visual pose is snapped to the prop.

## Milestone 1 — Travel model foundation

- Replace `Route`, `RouteStep`, and `RouteTraveler` persistence with `TravelCircuit`, `TravelCircuitLeg`, `Journey`, `JourneyLeg`, and `JourneyMember`.
- Model circuits as explicitly closed directed graph-edge sequences with dwell after each endpoint; do not store a looping flag or implicit wrap distance.
- Add planned/departure instants; planned, traveling, dwelling, completed, and cancelled journey states; source circuit; optional routine target context; directed leg node IDs; immutable distance/path snapshots; and durable checkpoint fields.
- Enforce one active journey membership per creature.
- Add each creature's durable current travel-node identity so every new route starts from the node the creature actually reached, rather than an arbitrary node in its location.
- Remove superseded routing persistence, queries, commands, and tests without retaining compatibility shims.
- Verify: migration generation, API build, and focused domain/routing tests.
- Final commit: `milestone 1: establish travel circuits and persisted journeys`.

## Milestone 2 — Generate destination anchors

- Generate a walkable approach node for every seat, assigned bed, and assigned workstation.
- Create invisible bidirectional point connectors from each anchor to the graph, with obstacle-aware directed polylines and matching distances.
- Ensure every working NPC has a dedicated workstation before generation completes.
- Retain road rendering only for connectors with `RoadClass`.
- Verify: world-generation build and anchor, graph, and geometry tests.
- Final commit: `milestone 2: generate routable prop approach anchors`.

## Milestone 3 — Build persisted journeys from graph paths

- Replace location-only route lookup with directed graph-path construction that produces complete journey-leg snapshots.
- Create routine and group journeys dynamically from their current travel intent; use a travel circuit only as the copied leg template when a group follows one.
- When an NPC has no pending journey, resolve its next job transition, choose its concrete target, persist the complete journey immediately, and calculate its staggered `DepartureAt` from the selected path.
- At `DepartureAt`, start travel from the creature's current travel node; on target completion, request planning for the next job transition.
- Select assigned bed/workstation anchors, free-seat anchors for idle work, and ordinary travel nodes for targetless or no-seat outcomes.
- Derive effective pace from the slowest active journey member; do not persist speed.
- Verify: journey planning, circuit instantiation, pace, and departure-timing tests.
- Final commit: `milestone 3: plan complete journeys from the travel graph`.

## Milestone 4 — Advance journeys with durable checkpoints

- Keep exact progression in the in-memory simulator while persisting only journey checkpoints and boundary state transitions; planned journeys wait without movement until `DepartureAt`.
- Derive projected position from checkpoint progress, checkpoint time, current member pace, and directed path geometry.
- Checkpoint before speed, membership, engagement, release, replan, dwell, location-crossing, cancellation, and completion transitions.
- Update stationary creature poses only when settling, crossing a durable location boundary, or leaving a journey; never write per-tick X/Y positions.
- Rehydrate the clock and active journeys at the same persisted instant on restart, with no special catch-up path.
- Verify: progression, speed-change, pause/resume, membership, restart, and no-per-tick-write tests.
- Final commit: `milestone 4: simulate journeys from durable checkpoints`.

## Milestone 5 — Resolve arrival targets and seat contention

- Complete bed and workstation journeys only at their assigned anchors, then atomically apply occupancy and job pose/activity.
- Keep `Seat.OccupantId` as physical occupancy only; planning records a preferred seat without reservation.
- Atomically claim only the reached preferred seat. On failure, append a route to the closest reachable free seat; if none exists, append a route to the nearest ordinary node and complete standing.
- Remove arbitrary fallback seat claims and post-arrival workstation batch assignment.
- Verify: player/NPC seat races, competing NPCs, reroute selection, no-seat fallback, bed, and workstation integration tests.
- Final commit: `milestone 5: resolve journey targets and seat contention`.

## Milestone 6 — Make journeys the scene movement source

- Add one read-only current-position projection shared by scene mapping and all creature proximity checks. It returns static pose for stationary creatures and journey-projected pose for moving creatures.
- Map only the current location's contiguous directed journey-leg paths into the existing SPA walk payload; never send a complete journey or future locations.
- Preserve client playback behavior, pause information, and exit indication while removing scene-time route calculation.
- Remove `NpcPathPlanner`, `DistrictRoutePlanner`, `GetCreatureWalkPathsQuery`, and legacy entry/exit walk persistence.
- Verify: scene snapshots, walk payload scope, route-free pose resolution, proximity checks against walking creatures, and SPA type/tests.
- Final commit: `milestone 6: render and query movement from journeys`.

## Milestone 7 — Regenerate-world cutover and full validation

- Wire generated circuits and journeys through all traveler creation flows, remove remaining `RouteTraveler` references, and document the required world reset.
- Run the complete backend and SPA test suites plus production builds from a clean generated world.
- Review the migration/reset procedure and ensure no legacy-route fallback remains.
- Final commit: `milestone 7: complete unified travel journey cutover`.
