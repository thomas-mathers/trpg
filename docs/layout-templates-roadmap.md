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
- [ ] L01 Furniture models and SPA meshes
- [ ] L02 Templates and fixed sizing
- [ ] L03 Recipe engine and room recipes
- [ ] L04 House variants and household bedrooms
- [ ] L05 Stair and connector alignment
- [ ] L06 District generator and outdoor furnishing
- [ ] L07 Docs and final verification

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

### [ ] L01 Furniture models and SPA meshes

Scope:

- [ ] Read `PropModel`, `PropModelResolver`, `PropFootprintCatalog`, the SPA `model-styles.ts` and `viewport-scene.tsx` first.
- [ ] Add the `Furniture : Prop` subtype and any new `PropModel` values the workshop needs, in the domain and wire.
- [ ] Exclude `Furniture` from interaction listings.
- [ ] Add a primitive mesh and style entry in the SPA for every `PropModel` value.
- [ ] Migration if the new subtype needs one.

Verification:

- [ ] Coverage tests: every `PropModel` resolves to a footprint and an SPA style.
- [ ] `scripts/build.sh` and SPA type check pass.

### [ ] L02 Templates and fixed sizing

Scope:

- [ ] Add data-driven building templates (floors, rooms, role, fixed width and depth) for every non-dungeon `BuildingType`, with one template per type.
- [ ] Replace `LocationSizer`'s area formulas, `SizeHallway`, the 1.15 building factor and aspect jitter with template sizes.
- [ ] Add floor count to `Building` and the layout wire.

Verification:

- [ ] Tests: every building type has a template, sizes are grid-snapped, buildings fit their template rooms.
- [ ] `scripts/build.sh` passes.

### [ ] L03 Recipe engine and room recipes

Scope:

- [ ] Read `BuildingGenerator` and `BuildingSpecCatalog` first, and settle where props get created.
- [ ] Add the recipe engine and port the workshop room plans, replacing `RoomPropPlacer`.
- [ ] Wire recipes into building generation so required props come from templates.
- [ ] First slice: the inn and the blacksmith, end to end.

Verification:

- [ ] Tests (ported from the TS invariants): everything in bounds, no solid overlaps, nothing blocks doors or stairs, required props always placed, determinism.
- [ ] Generated-world test for the inn and the blacksmith.

### [ ] L04 House variants and household bedrooms

Scope:

- [ ] Add Small, Medium and Large house templates with uniform bedrooms of up to 2 beds.
- [ ] Pack the household into bedrooms (parents together, children alone, then doubled up) and pick the smallest covering template.
- [ ] Build `GetHouseSpecs` from the chosen template's bedroom slots.

Verification:

- [ ] Tests: every household size from minimum to maximum gets a template and every member gets a bed; failure when the configured maximum exceeds capacity.

### [ ] L05 Stair and connector alignment

Scope:

- [ ] Align stair connector points across floors.
- [ ] Adjust hallway connector points to the templates.

Verification:

- [ ] Tests: the stair exit on one floor matches the arrival on the next.

### [ ] L06 District generator and outdoor furnishing

Scope:

- [ ] Port the district generator (squares, courts, street network) with a seeded step for jitter.
- [ ] Port outdoor furnishing (centrepieces, benches, notice boards).

Verification:

- [ ] Tests: no overlap, everything in bounds, door approaches clear, determinism.

### [ ] L07 Docs and final verification

Scope:

- [ ] Update the structure section of `api/AGENTS.md` and delete dead code (grow-and-retry, old sizing).
- [ ] Run the full API and SPA suites once and fix regressions.

Verification:

- [ ] `scripts/build.sh` and the full suites pass.
- [ ] Every milestone above is checked.
