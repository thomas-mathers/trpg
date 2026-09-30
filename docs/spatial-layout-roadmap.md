# Spatial layout roadmap

Backend groundwork for a realtime 3D viewport: every `Location` gets a local metric frame, every prop, building, creature, and connector gets a pose in it, and the scene snapshot carries that layout to the SPA. No renderer, walls, navmesh, or NPC movement plans are part of this work.

Resume rule: read this whole document, find the first unchecked milestone, and do only that milestone. Re-read the files it names instead of relying on memory of earlier sessions.

## Completion convention

Same as `module-refactor-roadmap.md`, with the `S` prefix. Each milestone ends with one commit:

```text
milestone(SNN): complete <short milestone name>
```

The completion commit must:

- Be the final commit of the milestone, after its verification items pass.
- Flip the milestone's top-level checkbox and every completed item in this document.
- Contain a real implementation or checklist change, never an empty marker.

All milestones land in one branch (`feat/spatial-layout`) and one pull request. Do not push until told. If work is added after a marker commit, uncheck the milestone, re-verify, and add a new marker.

Working rules for every milestone:

- Build with `scripts/build.sh`. Run only the tests the milestone adds or touches. The full suite runs once, in S13.
- Follow `AGENTS.md` and `api/AGENTS.md` (primary constructors, file-scoped namespaces, no abbreviations, no tuples, no comments unless truly needed, pure functions, methods under 40 lines, CSharpier).
- New generator code lives in `TRPG.Application.WorldGeneration/Generators` and stays `internal`, stateless, and seeded from the location id (`new Random(hash of location id)`), never `Random.Shared`.

## Milestone tracker

- [x] S01 Domain value types, columns, migration
- [x] S02 Oriented box geometry
- [ ] S03 Footprint catalogs and asset keys
- [ ] S04 Location sizing
- [ ] S05 District and building layout
- [ ] S06 Connector points
- [ ] S07 Room prop placement
- [ ] S08 Layout post-pass in world generation
- [ ] S09 Creature placement resolver
- [ ] S10 Creature placement at world generation
- [ ] S11 Creature placement at runtime
- [ ] S12 Contracts and scene layout
- [ ] S13 Docs and final verification

## Design reference

Frame and units:

- Meters as `double`, snapped to a 0.25 m grid. X east, Y south, Angle in radians with 0 facing north, rotation about the rect center. `Placement` X and Y are the rect center, and Width runs along the box's local X axis. Defined once on `Placement`.
- Each `Location` has a size only (`Width` x `Depth`). No parent rects, no world offsets. Locations are axis-aligned; props may be rotated.
- `Placement(X, Y, Angle)`, `Footprint(Width, Depth)`. A prop rect is a placement plus a footprint. Columns are plain non-null doubles defaulting to 0 (existing worlds are ignored; the jsonb default bug does not apply to scalars).
- The server never sends geometry: only sizes, rects, asset keys, connector points, and creature poses. The client builds walls and scenery.

Sizing:

- Room area is the larger of the catalog minimum (by building type and room role) and `1.5 x (sum of prop footprint areas) + 1.2 x Capacity`. Aspect is seeded in [1.0, 1.6]. Width is `sqrt(area x aspect)`, depth is `area / width`, snapped up to the grid, clamped to the catalog max.
- Hallway: 2 m wide, depth `2 + 1.5 x roomCount`.
- Building footprint: ground-floor rooms' area (hallway included) x 1.15, aspect near 1.3, catalog minimum for castle and temple. Stored on `Building` in the exterior district's frame.
- District: `(sum of building footprints + 6 m margin per building + seat area) x 2`, roughly square.
- Wilderness: constant 300 x 300 m regardless of state size.

Placement:

- Building boxes are shelf-packed on both sides of an east-west street 6 m wide, 3 m gaps, largest first, doors facing the street. Seats line the street edges facing the street.
- Props: asset key (subtype plus discriminator, for example `prop.workstation.alchemy`) maps to footprint, rule (`Corner`, `Wall`, `Anchor`, `Center`, `Free`), and front clearance. Process in rule order, largest first. Wall and corner props are flush (0.05 m inset) at 0/90/180/270 facing inward. Anchor seats ring the workstation facing it. Center and free props use 50 rejection-sampling tries with a 1 m margin; free angles are multiples of 15 degrees. Accept only if all corners are in bounds and there is no oriented-box overlap (separating axis) with placed rects inflated by front clearance or with door keep-outs. If a prop fails to fit, grow the room 10% and rerun, up to 3 times.
- Doors: one per room on the south wall center, 1.5 m keep-out in front. Hallway: front door at the south end, staircase at the north end, room doors alternating east and west walls at 1.5 m spacing. Single-room floors: front door south, stairs in the NE corner.
- Connector points: compass connectors exit at the edge center for the `CompassDirection` (evenly spaced by destination id when sharing an edge). Arrival is the reverse connector's exit moved 1 m inward, facing inward. Wilderness exits sit on the edge at the bearing toward the neighbor state's center, spread when within 6 m. Building front door arrival is just inside the entrance room's south door, facing north.

Creatures:

- Stored pose is the last settled pose in the current location's frame, written at events only (arrival, placement), never per tick.
- Players arrive at the arrival point of the connector whose destination matches `PreviousLocationId` (`GetSceneQuery` already matches on it), else the default spawn. NPCs go near their anchor (workstation, seat, bed) or a seeded free spot that overlaps no prop.
- `UpdateCreaturesCommand` applies one `ExecuteUpdateAsync` to many rows, so per-creature poses need a separate per-creature write step in the handler.
- Runtime callers of `UpdateCreaturesCommand.LocationId`: `MovePlayerCommand`, `SyncCreatureJobSchedulesCommand`, `SyncRouteTravelersCommand`, `SeedCaptiveRescueQuestCommand`, `MaterializeScheduledRouteTravelersCommand`, `RouteCreaturesToDestinationsCommand`.
- Creation-time writers of `Creature.LocationId`: `BarracksGuardDutyAssigner`, `CreatureGroupGenerator`, `DungeonExpeditionGenerator`, `DungeonInhabitantGenerator`, `EncounterMonsterGenerator`, `GuildHallOccupantGenerator`, `HouseholdGenerator`, `CreateWorldCommand` (player), `ResolvePlayerRespawnCommand` (corpse).
- Module boundaries hold: `Creatures` must not read `Worlds` or `Props` tables. Resolver inputs come through query handlers.

## Milestones

### [x] S01 Domain value types, columns, migration

Scope:

- [x] Add `Placement` and `Footprint` records in `TRPG.Domain`.
- [x] Add `Width` and `Depth` to `Location`.
- [x] Add `X`, `Y`, `Angle`, `Width`, `Depth` to `Prop` (base class).
- [x] Add `X`, `Y`, `Angle`, `Width`, `Depth` to `Building`.
- [x] Add `ExitX`, `ExitY`, `ArrivalX`, `ArrivalY`, `ArrivalAngle` to `LocationConnector`.
- [x] Add `X`, `Y`, `Angle` to `Creature`.
- [x] Add the EF configuration for the new columns and a migration via `scripts/add-migration.sh`.

Verification:

- [x] `scripts/build.sh` passes.
- [x] Migration SQL reviewed: plain double columns, default 0, no jsonb.

### [x] S02 Oriented box geometry

Scope:

- [x] Add a pure internal `OrientedBox` (center, footprint, angle) with corners, `IsInside(width, depth)`, and `Overlaps(other, margin)` using the separating axis test.

Verification:

- [x] Tests: axis-aligned and rotated overlap, touching edges, margin inflation, containment at bounds, 90 degree symmetry.

### [ ] S03 Footprint catalogs and asset keys

Scope:

- [ ] Add `AssetKeyResolver` (prop to key, per-subtype fallback).
- [ ] Add `PropFootprintCatalog` (key to footprint, rule, front clearance).
- [ ] Add `RoomSizeCatalog` and `BuildingFootprintCatalog`.
- [ ] Add the wilderness size constant.

Verification:

- [ ] Coverage tests: every `BuildingType`, `RoomRole`, `WorkstationType`, and `Prop` subtype resolves to a catalog entry.

### [ ] S04 Location sizing

Scope:

- [ ] Add a pure `LocationSizer` for rooms, hallways, districts, and wilderness per the design reference.

Verification:

- [ ] Tests: minimums and maximums respected, grid snapping, determinism for the same seed, hallway depth scales with room count.

### [ ] S05 District and building layout

Scope:

- [ ] Add `DistrictLayoutGenerator`: building footprints, shelf packing along the street, door points, and seat placement.

Verification:

- [ ] Tests: no building overlap, all inside district bounds, doors face the street, determinism.

### [ ] S06 Connector points

Scope:

- [ ] Add a pure `ConnectorPointResolver` for compass, door, stair, hallway, building front door, and wilderness-bearing connectors, producing exit and arrival points.

Verification:

- [ ] Tests: exit and arrival on opposite edges per `CompassDirection`, bearing maps to the right edge, edge spreading, arrival is 1 m inside facing inward.

### [ ] S07 Room prop placement

Scope:

- [ ] Add `RoomPropPlacer` with the rule order, door keep-outs, and the grow-and-retry fallback.

Verification:

- [ ] Tests: everything in bounds, no overlaps, nothing in door keep-outs, seats face their workstation, beds prefer corners, determinism, growth fallback terminates.

### [ ] S08 Layout post-pass in world generation

Scope:

- [ ] Add `LocationLayoutGenerator` orchestrating sizes, building boxes, connector points, then props.
- [ ] Call it as a post-pass in `WorldGenerator` and persist the results.

Verification:

- [ ] Generated-world test: every location has a positive size, every prop and building is inside its location, every connector has points.

### [ ] S09 Creature placement resolver

Scope:

- [ ] Add a pure `CreaturePlacementResolver` (player at arrival point, NPC near anchor or seeded free spot).

Verification:

- [ ] Tests: player arrival point and facing, NPC anchor proximity, no overlap with props, fallback when the room is crowded, determinism.

### [ ] S10 Creature placement at world generation

Scope:

- [ ] Place every creature produced by the creation-time writers listed in the design reference, plus the player in `CreateWorldCommand`.

Verification:

- [ ] Invariant test: after world generation every creature pose lies within its location bounds.

### [ ] S11 Creature placement at runtime

Scope:

- [ ] Add the per-creature pose write to `UpdateCreaturesCommand`'s handler, fed by query handlers in `Worlds` and `Props`.
- [ ] Cover the six runtime callers and `ResolvePlayerRespawnCommand`.

Verification:

- [ ] Command-level test: a move writes a pose within bounds; the player lands at the matching arrival point.
- [ ] Invariant test over each of the six runtime move paths.

### [ ] S12 Contracts and scene layout

Scope:

- [ ] Read `SceneResult`, the snapshot mapper, and `SceneSemanticComparer` first.
- [ ] Add `PlacementWire`, `FootprintWire`, and `LocationLayoutWire` to `TRPG.Contracts`.
- [ ] Add application result types, host mappers, and a `Layout` field on the scene snapshot, filtering hidden traps and triggers.
- [ ] Decide and implement whether pose changes count in `SceneSemanticComparer`.

Verification:

- [ ] Mapper tests, including the hidden trap and trigger filter.
- [ ] Seam test: a move produces a snapshot carrying the new pose.

### [ ] S13 Docs and final verification

Scope:

- [ ] Update the structure section of `api/AGENTS.md`.
- [ ] Run the full test suite once and fix regressions.

Verification:

- [ ] `scripts/build.sh` and the full suite pass.
- [ ] Every milestone above is checked.
