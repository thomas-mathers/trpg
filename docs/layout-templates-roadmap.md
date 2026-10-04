# Layout templates roadmap

Port the Storybook layout workshop (`spa/src/features/game/viewport/district-layout.stories.tsx` and `layout-preview/`) to the backend. The workshop is the spec: building templates with fixed room sizes, furniture recipes, stairs that line up across floors, and districts with squares, courts and street furniture. The SPA gets low-fidelity primitive meshes for every new furniture model.

Resume rule: read this whole document, find the first unchecked milestone, and do only that milestone. Re-read the files it names instead of relying on memory of earlier sessions.

## Completion convention

Same as `spatial-layout-roadmap.md`, with the `L` prefix. Each milestone ends with one commit:

```text
milestone(LNN): complete <short milestone name>
```

The completion commit must:

- Be the final commit of the milestone, after its verification items pass.
- Flip the milestone's top-level checkbox and every completed item in this document.
- Contain a real implementation or checklist change, never an empty marker.

All milestones land in one branch (`feat/layout-templates`) and one pull request. Do not push until told. If work is added after a marker commit, uncheck the milestone, re-verify, and add a new marker.

Working rules for every milestone:

- Build the API with `scripts/build.sh`. In `spa`, check types and lint, and format with `pnpm run fmt`. Run only the tests the milestone adds or touches. The full suites run once, in L07.
- Follow `AGENTS.md`, `api/AGENTS.md` and `spa/AGENTS.md` (primary constructors, file-scoped namespaces, no abbreviations, no tuples, no em dashes, one-line why-comments only, pure functions, methods under 40 lines, CSharpier).
- New generator code lives in `TRPG.Application.WorldGeneration/Generators`, stays `internal` and stateless. Anything random is seeded from the entity id (`LayoutSeed.From`), never `Random.Shared`.
- Wire types live in the host `TRPG` project (`GameSessions/Responses`), never in domain types.
- Existing worlds may break. Do not write compatibility code for old layouts. Migrations follow `scripts/add-migration.sh`, and scaffolded `defaultValue: ""` on jsonb columns must be fixed by hand.

## Milestone tracker

- [x] L00 Checklist and workshop
- [x] L01 Furniture models and SPA meshes
- [x] L02 Templates and fixed sizing
- [x] L03 Recipe engine and room recipes
- [x] L04 House variants and household bedrooms
- [x] L05 Stair and connector alignment
- [x] L06 District generator and outdoor furnishing
- [x] L07 Docs and final verification
- [x] L08 Remaining room recipes

## Design reference

Principles:

- The Storybook is the spec. Where the backend and the workshop disagree, the backend changes.
- Sizes are fixed per template, not computed from contents. Variability comes only from choosing a template (for example small, medium and large houses). Every non-house building type has one template for now; adding a variant later is a data entry plus a test row.
- Furnishing is deterministic from room width and depth. No seeds are involved in room furnishing.
- Fit is guaranteed by tests over every template and recipe, and generation throws if a required prop has no slot. There is no grow-and-retry loop and no silent skip for required props. Optional decor may be dropped when it does not fit.

Rooms and floors:

- Rooms stay separate `Location`s, each with its own frame, joined by door connectors. A floor with more than one room has a synthesized hallway location with a connector to each room and back; a single-room floor skips it. A shared floor plan is out of scope.
- Upper floors may be smaller than the ground floor in the workshop. On the backend every floor of a building keeps the ground floor's footprint for now; the exterior height reflects the floor count.
- `Building` gains a floor count so the client can extrude the exterior. Stair connector points line up across floors.

Props:

- Gameplay-bearing furniture stays a persisted `Prop` subtype: `Bed`, `Workstation`, `Container`, `Seat`, `Cell`, `Sign`.
- Pure decor is a new `Furniture : Prop;` subtype, excluded from interaction listings. There is no separate decoration entity.
- Every furniture kind in the workshop gets a `PropModel` value, in the domain and on the wire. The SPA draws each with composed primitives (`SeatMesh` is the pattern). Non-blocking rugs sit below `WALKABLE_HEIGHT` and stay non-solid in layout math.
- `PropModelResolver` stays derived and unpersisted.

Recipes:

- A recipe engine replaces `RoomPropPlacer`. A room recipe lists furniture entries (model, anchor or wall run or table set or grid, rotation) and is evaluated against the fixed room size, mirroring the workshop's `furnishRoom`: rugs first, then solids in plan order with a 0.12 m clearance.
- Grid is 0.25 m, rotations are multiples of 90 degrees: north 0, east 90, south 180, west 270 in the workshop; the backend keeps its radian convention (0 = north, clockwise).
- Rooms are shared recipes composed into building templates.

Houses:

- The household is generated before the house, so the template is a function of the residents, not a seeded pick. Variants are Small, Medium and Large with uniform bedrooms of up to 2 beds. Parents share a room; children get their own until the largest variant runs out, then siblings double up.
- The template is the smallest whose bedroom count and bed capacity cover the household. Tests cover every household size from the configured minimum to maximum, and every member gets a bed (sleep jobs need it). If the configured maximum exceeds the largest variant, validation or generation fails loudly.

Out of scope:

- Dungeon rooms keep the existing fallback layout.
- Shared multi-room floor plans, walls built by the server, navmesh.

## Milestones

### [x] L00 Checklist and workshop

Scope:

- [x] Add this checklist.
- [x] Commit the Storybook workshop (`district-layout.stories.tsx` and `layout-preview/`) as the spec.

Verification:

- [x] `pnpm` type check passes in `spa`.

### [x] L01 Furniture models and SPA meshes

Scope:

- [x] Read `PropModel`, `PropModelResolver`, `PropFootprintCatalog`, the SPA `model-styles.ts` and `viewport-scene.tsx` first.
- [x] Add the `Furniture : Prop` subtype and any new `PropModel` values the workshop needs, in the domain and wire.
- [x] Exclude `Furniture` from interaction listings.
- [x] Add a primitive mesh and style entry in the SPA for every `PropModel` value.
- [x] Migration if the new subtype needs one.

Verification:

- [x] Coverage tests: every `PropModel` resolves to a footprint and an SPA style.
- [x] `scripts/build.sh` and SPA type check pass.

### [x] L02 Templates and fixed sizing

Scope:

- [x] Add data-driven building templates (floors, rooms, role, fixed width and depth) for every non-dungeon `BuildingType`, with one template per type.
- [x] (Dungeons keep the old sizing.) Replace `LocationSizer`'s area formulas, `SizeHallway`, the 1.15 building factor and aspect jitter with template sizes.
- [x] Add floor count to `Building` and the layout wire.

Verification:

- [x] Tests: every building type has a template, sizes are grid-snapped, buildings fit their template rooms.
- [x] `scripts/build.sh` passes.

### [x] L03 Recipe engine and room recipes

Scope:

- [x] Read `BuildingGenerator` and `BuildingSpecCatalog` first, and settle where props get created.
- [x] Add the recipe engine and port the workshop room plans, replacing `RoomPropPlacer`.
- [x] Wire recipes into building generation so required props come from templates.
- [x] First slice: the inn and the blacksmith, end to end.

Verification:

- [x] Tests (ported from the TS invariants): everything in bounds, no solid overlaps, nothing blocks doors or stairs, required props always placed, determinism.
- [x] Generated-world test for the inn and the blacksmith.

Note: this milestone shipped the inn and blacksmith recipes only. L08 adds the rest.

### [x] L04 House variants and household bedrooms

Scope:

- [x] Add Small, Medium and Large house templates with uniform bedrooms of up to 2 beds.
- [x] Pack the household into bedrooms (parents together, children alone, then doubled up) and pick the smallest covering template.
- [x] Build `GetHouseSpecs` from the chosen template's bedroom slots.

Verification:

- [x] Tests: every household size from minimum to maximum gets a template and every member gets a bed; failure when the configured maximum exceeds capacity.

Note: house bedroom recipes use ordered fallback slots, so the same recipe fits all three variants. The new-world dialog caps household size at the largest house capacity (6).

### [x] L05 Stair and connector alignment

Scope:

- [x] Align stair connector points across floors.
- [x] Adjust hallway connector points to the templates.

Verification:

- [x] Tests: the stair exit on one floor matches the arrival on the next.

Note: stairs sit on the north wall (the workshop puts them south, but the ground-floor front door is on the south wall center). Each flight has its own column, 1.2 m apart, so a middle-floor hallway can hold both flights. Hallway doors are centered on their rooms with even gaps, as in the workshop.

### [x] L06 District generator and outdoor furnishing

Scope:

- [x] Port the district generator (squares, courts, street network) with a seeded step for jitter.
- [x] Port outdoor furnishing (centrepieces, benches, notice boards).

Verification:

- [x] Tests: no overlap, everything in bounds, door approaches clear, determinism.

Note: Residential districts are a seeded grid of blocks around one village square (houses face the surrounding streets, 3 m alleys, block count derived from a guaranteed per-block capacity so no retry loop is needed). CityCenter, Encampment, Scientific and Governmental use a fixed precinct layout (roster-ordered buildings around a court, 7 m margin, avenue to the south); other kinds use the same precinct layout with whatever buildings they hold. The square gets a centrepiece per district type and a notice board; the three public bench props bind to bench slots. Decorative crates, barrels and extra benches from the workshop are not ported.

### [x] L07 Docs and final verification

Scope:

- [x] Update the structure section of `api/AGENTS.md` and delete dead code. Grow-and-retry and `LocationSizer` room sizing stay for dungeons and recipe-less rooms; only the district sizing was dead.
- [x] Run the full API and SPA suites once and fix regressions.

Verification:

- [x] `scripts/build.sh` and the full suites pass.
- [x] Every milestone above is checked.

### [x] L08 Remaining room recipes

Scope:

- [x] Add a recipe for every non-dungeon building room: shops (one shared station, counter and display-shelf layout), living and owner quarters, tavern, guild hall, library, temple, stable, barracks, castle and jail. Dungeons keep `LocationSizer` and `RoomPropPlacer`.
- [x] Extend the recipe engine with table sets, centred and rug-runner steps, pew rows, reading tables, back-to-back book stacks and south stock rows.
- [x] Add decor stand-ins (chair, pew, bench, bookcase, staff rack) so a seat, workstation or weapon rack slot with no bound prop still furnishes the room. Add the matching `PropModel` values, footprints and low-fidelity SPA meshes.

Verification:

- [x] Every non-dungeon building type passes the in-bounds, no-overlap, door-clearance and determinism tests.
- [x] A fully staffed barracks (7 guards) and guild hall (6 members) furnish without overlap.
- [x] Stand-in decor is emitted for each stand-in model.

Note: not ported are the jail cell gameplay prop (the slots exist, so a `Cell` binds if present), extra containers beyond the spec props, extra counters, and the workshop's hay bales and anvils. `Furniture.Model` is stored as text, so the new enum values need no migration.
