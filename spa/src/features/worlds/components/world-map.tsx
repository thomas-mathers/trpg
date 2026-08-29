import {
  BaseEdge,
  getStraightPath,
  Handle,
  Position,
  ReactFlow,
  useReactFlow,
  useViewport,
  type EdgeProps,
  type Node,
  type NodeProps,
} from '@xyflow/react';
import { useEffect, useMemo, type CSSProperties, type ReactNode } from 'react';
import {
  GiCastle,
  GiHouse,
  GiMountains,
  GiPerson,
  GiScrollUnfurled,
  GiTombstone,
} from 'react-icons/gi';

import type {
  CityMapResponse,
  CorpseMapResponse,
  CountryMapResponse,
  PointResponse,
  QuestMapResponse,
  RoadMapResponse,
  StateMapResponse,
} from '@/api/client';
import {
  HoverPopover,
  HoverPopoverContent,
  HoverPopoverTrigger,
} from '@/components/ui/hover-popover';

import '@xyflow/react/dist/style.css';

// Fixed parchment/ink palette — the map always renders as a paper map, in its own fixed light
// styling, regardless of the app's own light/dark theme (see colorMode="light" below).
const PARCHMENT = '#ece0c3';
const INK = '#3a2d1f';
const INK_MUTED = '#6b5f47';
const GOLD = '#a9761f';

const MAP_FONT = 'var(--font-fell), serif';

const COUNTRY_PALETTE = [
  '#43664a', // forest
  '#3c5670', // slate
  '#5c3a66', // plum
  '#2f6b62', // teal
  '#6b4a2f', // umber
  '#3d3f7a', // indigo
];

const ROAD_COLOR = '#8a3c1f'; // rust
const ROAD_OPACITY = 0.68;

// A country's own Boundary polygon *is* the shared edge with its neighbor (the backend walks
// Voronoi edges where the two sides belong to different countries) — so a border between two
// countries is drawn twice, once by each side's own polygon stroke. Width is already consistent
// everywhere (countryStrokeWidth is one value derived from the whole map, not per-country), but
// using each country's own hash color for the stroke would make a shared edge look like two
// different, partially-transparent lines stacked on each other. Using one fixed, fully-opaque
// color for every country's stroke sidesteps that: painting the same opaque color over itself
// twice looks identical to painting it once, so the double-draw becomes invisible. Kept thin and
// partly transparent — most of a country's own outer edge lands exactly on the world's own
// boundary (Voronoi cells are clipped against it), so a bold opaque stroke there read as a hard
// frame around the whole map; this softer line stays quiet everywhere, including there.
const COUNTRY_BORDER_COLOR = INK;
const COUNTRY_BORDER_OPACITY = 0.35;

// A settlement icon's own artwork isn't necessarily centered in its viewBox, and it sits over
// varying backgrounds (a road, another state's fill, open map) — a stroke outline (not a blur)
// keeps every icon legible regardless of what's behind it, most noticeably the wilderness
// mountain glyph against the road's similarly dark tone.
const ICON_OUTLINE_COLOR = INK;

// Banner-style country name plaque — a controlled background of its own, so it needs its own
// text/border colors rather than reusing map-surface tokens. Translucent (not solid): it draws
// above every marker (see Z_INDEX) specifically so it's never hidden behind one, but a solid
// plaque at that top layer would then permanently blot out whatever marker it happens to land on.
const BANNER_BORDER_COLOR = GOLD;
const BANNER_TEXT_COLOR = PARCHMENT;
const BANNER_BACKGROUND_OPACITY = 0.92;

// Raised (was 0.02) — that let the player zoom out far enough that a normal-sized world shrank to
// an illegible speck, well past the point of being useful.
const MIN_ZOOM = 0.06;
const MAX_ZOOM = 6;

// Icons (settlements, wilderness markers, player/corpse/quest pins) are always visible, at
// whatever zoom the map is at — they're floor-clamped (see ZoomFloorConstant below) to a
// constant on-screen size below this zoom, then grow naturally with the map above it. Text
// labels are noisier at a glance (dozens of state/city names at once is illegible clutter) and
// only appear once the player has zoomed in this far to pick out a specific region. Lowered (was
// 0.35) — that threshold sat well past the map's own fitted starting zoom for a typically-sized
// world, so city names never appeared without the player manually zooming in first.
const DETAIL_ZOOM_THRESHOLD = 0.15;

// Every settlement icon (capital, city, or rural/wilderness) is the same size at a given zoom
// level — the shape (castle/house/mountain) and color already carry that distinction, so the
// size doesn't need to repeat it.
const ICON_FLOW = {
  state: 20 / DETAIL_ZOOM_THRESHOLD,
  marker: 24 / DETAIL_ZOOM_THRESHOLD,
  markerIcon: 14 / DETAIL_ZOOM_THRESHOLD,
};

const FONT_FLOW = {
  state: 10 / DETAIL_ZOOM_THRESHOLD,
  country: 12 / DETAIL_ZOOM_THRESHOLD,
};

const LABEL_ANCHOR_SIZE = 10;

interface WorldMapProps {
  countries: CountryMapResponse[];
  states: StateMapResponse[];
  cities: CityMapResponse[];
  roads: RoadMapResponse[];
  playerStateId: string;
  corpses: CorpseMapResponse[];
  questMarkers: QuestMapResponse[];
}

export function WorldMap({
  countries,
  states,
  cities,
  roads,
  playerStateId,
  corpses,
  questMarkers,
}: WorldMapProps) {
  const { nodes, edges } = useMemo(
    () => buildWorldMap(countries, states, cities, roads, playerStateId, corpses, questMarkers),
    [countries, states, cities, roads, playerStateId, corpses, questMarkers],
  );
  const { fitView } = useReactFlow();

  useEffect(() => {
    if (nodes.length === 0) return;

    requestAnimationFrame(() => {
      fitView({ padding: 0.1, minZoom: MIN_ZOOM, maxZoom: MAX_ZOOM, duration: 0 });
    });
  }, [nodes, fitView]);

  return (
    <div
      className="trpg-world-map min-h-0 flex-1"
      style={{
        background: `radial-gradient(ellipse at 20% 15%, rgba(255,255,255,0.35), transparent 55%),
          radial-gradient(ellipse at 80% 85%, rgba(0,0,0,0.06), transparent 60%),
          ${PARCHMENT}`,
      }}
    >
      <ReactFlow
        nodes={nodes}
        edges={edges}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        colorMode="light"
        minZoom={MIN_ZOOM}
        maxZoom={MAX_ZOOM}
        nodesFocusable={true}
        nodesConnectable={false}
        nodesDraggable={false}
        elementsSelectable={true}
      />
    </div>
  );
}

interface MapScale {
  countryStrokeWidth: number;
  stateStrokeWidth: number;
  roadStrokeWidth: number;
  markerSize: number;
  markerGap: number;
}

function boundingBoxOf(points: PointResponse[]) {
  const xs = points.map((point) => point.x);
  const ys = points.map((point) => point.y);
  const minX = Math.min(...xs);
  const minY = Math.min(...ys);
  return {
    minX,
    minY,
    width: Math.max(...xs) - minX,
    height: Math.max(...ys) - minY,
  };
}

function computeScale(countries: CountryMapResponse[], states: StateMapResponse[]): MapScale {
  const points =
    countries.length > 0
      ? countries.flatMap((country) => country.boundary)
      : states.flatMap((state) => state.boundary);
  const box = points.length > 0 ? boundingBoxOf(points) : null;
  const extent = box ? Math.max(box.width, box.height) : 1000;

  return {
    // Softened (was 0.0026) — a bold, fully-opaque stroke along most of a country's own outer
    // edge (which lands exactly on the world's own boundary) read as a hard frame around the
    // whole map. See COUNTRY_BORDER_OPACITY.
    countryStrokeWidth: extent * 0.0016,
    stateStrokeWidth: extent * 0.001,
    // Roads read as the map's primary connective feature, distinctly bolder than the hairline
    // state/country borders — 3x the country border width, matching the design reference.
    roadStrokeWidth: extent * 0.0048,
    markerSize: extent * 0.022,
    markerGap: extent * 0.006,
  };
}

function hexWithAlpha(hex: string, opacity: number): string {
  const alpha = Math.round(opacity * 255)
    .toString(16)
    .padStart(2, '0');
  return `${hex}${alpha}`;
}

// Assigns colors by index into a stable (id-sorted) ordering, not by hashing each country's own
// id independently — a hash bucketed into only as many slots as there are palette colors collides
// readily once the country count approaches the palette size (birthday-paradox odds, not an edge
// case), which is exactly the common case here. Indexing guarantees every country gets a distinct
// color as long as there are at least as many palette entries as countries.
function colorsByCountryId(countries: CountryMapResponse[]): Map<string, string> {
  const sorted = [...countries].sort((a, b) => a.id.localeCompare(b.id));
  return new Map(
    sorted.map((country, index) => [country.id, COUNTRY_PALETTE[index % COUNTRY_PALETTE.length]]),
  );
}

// z-index tiers keep every border below roads below marker connector stems below state labels
// below marker badges below the country banner, regardless of draw order — otherwise a later,
// spatially-overlapping border can paint over an earlier label, country/state fills would
// otherwise sit on top of the roads crossing them, a marker's connector stem would paint over the
// settlement icon it's meant to visually tuck underneath instead, and the country banner (which
// can overlap markers near its anchor) would be hidden behind them rather than legible above
// everything else on the map.
const Z_INDEX = {
  countryBorder: 0,
  stateBorder: 1,
  road: 2,
  markerConnector: 3,
  stateLabel: 4,
  marker: 5,
  countryLabel: 6,
};

type CountryBorderData = { points: PointResponse[]; color: string; strokeWidth: number };
type CountryLabelData = { name: string; color: string };
type StateBorderData = {
  points: PointResponse[];
  color: string;
  strokeWidth: number;
};
type StateLabelData = {
  name: string;
  description: string;
  color: string;
  city: CityMapResponse | null;
};
type MarkerKind = 'player' | 'corpse' | 'quest';
type MarkerData = { kind: MarkerKind; label: string; itemCount?: number };
// A vertex-to-badge connector stem, already translated into the node's own local bounding-box
// space (see buildWorldMap) — `from` is always the vertex, `to` the badge's own position.
type MarkerConnectorData = { from: PointResponse; to: PointResponse; strokeWidth: number };

type CountryBorderFlowNode = Node<CountryBorderData, 'country-border'>;
type CountryLabelFlowNode = Node<CountryLabelData, 'country-label'>;
type StateBorderFlowNode = Node<StateBorderData, 'state-border'>;
type StateLabelFlowNode = Node<StateLabelData, 'state-label'>;
type MarkerFlowNode = Node<MarkerData, 'marker'>;
type MarkerConnectorFlowNode = Node<MarkerConnectorData, 'marker-connector'>;

function buildWorldMap(
  countries: CountryMapResponse[],
  states: StateMapResponse[],
  cities: CityMapResponse[],
  roads: RoadMapResponse[],
  playerStateId: string,
  corpses: CorpseMapResponse[],
  questMarkers: QuestMapResponse[],
) {
  const scale = computeScale(countries, states);
  const countryColors = colorsByCountryId(countries);
  const cityByStateId = new Map(cities.map((city) => [city.stateId, city]));

  const statesByCountryId = new Map<string, StateMapResponse[]>();
  for (const state of states) {
    const group = statesByCountryId.get(state.countryId) ?? [];
    group.push(state);
    statesByCountryId.set(state.countryId, group);
  }

  const countryBorders: CountryBorderFlowNode[] = [];
  const countryLabels: CountryLabelFlowNode[] = [];

  for (const country of countries) {
    const box = boundingBoxOf(country.boundary);
    const color = countryColors.get(country.id)!;

    countryBorders.push({
      id: `${country.id}-border`,
      type: 'country-border',
      position: { x: box.minX, y: box.minY },
      style: { width: box.width, height: box.height },
      zIndex: Z_INDEX.countryBorder,
      draggable: false,
      selectable: false,
      data: {
        points: country.boundary.map((point) => ({
          x: point.x - box.minX,
          y: point.y - box.minY,
        })),
        color,
        strokeWidth: scale.countryStrokeWidth,
      },
    });

    // The bounding box of an irregular (often concave) country polygon can put its corner well
    // outside the actual shape. Anchor the label at the capital's location instead — guaranteed
    // to sit inside the country's own territory, and a natural place to read a country's name
    // from. Fall back to the average of member states' centers, then the bounding box center,
    // for the (rural-only) case where a country has no capital city.
    const memberStates = statesByCountryId.get(country.id) ?? [];
    const capitalState = memberStates.find((state) => cityByStateId.get(state.id)?.isCapital);
    // Anchored exactly on the capital's own point (same as its icon) — CountryLabelNode itself
    // applies the upward clearance, at render time, scaled by the same live zoom-compensation
    // factor the icon's own on-screen size uses. A static world-space offset baked in here would
    // shrink away to nothing at low zoom (icons are floor-clamped to a constant screen size, but
    // a fixed offset isn't), so the banner would drift back down onto the icon exactly at the
    // map's own fitted starting zoom — the "banner overlapping the icon" bug this replaces.
    const labelCenter = capitalState
      ? { x: capitalState.center.x, y: capitalState.center.y }
      : memberStates.length > 0
        ? {
            x: memberStates.reduce((sum, state) => sum + state.center.x, 0) / memberStates.length,
            y: memberStates.reduce((sum, state) => sum + state.center.y, 0) / memberStates.length,
          }
        : { x: box.minX + box.width / 2, y: box.minY + box.height / 2 };

    countryLabels.push({
      id: `${country.id}-label`,
      type: 'country-label',
      position: {
        x: labelCenter.x - LABEL_ANCHOR_SIZE / 2,
        y: labelCenter.y - LABEL_ANCHOR_SIZE / 2,
      },
      style: { width: LABEL_ANCHOR_SIZE, height: LABEL_ANCHOR_SIZE },
      zIndex: Z_INDEX.countryLabel,
      draggable: false,
      selectable: false,
      data: { name: country.name, color },
    });
  }

  const stateBorders: StateBorderFlowNode[] = [];
  const stateLabels: StateLabelFlowNode[] = [];

  for (const state of states) {
    const box = boundingBoxOf(state.boundary);
    const color = countryColors.get(state.countryId)!;
    const city = cityByStateId.get(state.id) ?? null;

    stateBorders.push({
      id: `${state.id}-border`,
      type: 'state-border',
      position: { x: box.minX, y: box.minY },
      style: { width: box.width, height: box.height },
      zIndex: Z_INDEX.stateBorder,
      draggable: false,
      selectable: false,
      data: {
        points: state.boundary.map((point) => ({ x: point.x - box.minX, y: point.y - box.minY })),
        color,
        strokeWidth: scale.stateStrokeWidth,
      },
    });

    stateLabels.push({
      // Roads attach to this node's id — a road should visually terminate at the settlement
      // point (state.center), not somewhere inside the border polygon's bounding box.
      id: state.id,
      type: 'state-label',
      position: {
        x: state.center.x - LABEL_ANCHOR_SIZE / 2,
        y: state.center.y - LABEL_ANCHOR_SIZE / 2,
      },
      style: { width: LABEL_ANCHOR_SIZE, height: LABEL_ANCHOR_SIZE },
      zIndex: Z_INDEX.stateLabel,
      draggable: false,
      data: { name: state.name, description: state.description, color, city },
    });
  }

  const stateById = new Map(states.map((state) => [state.id, state]));

  // A player/corpse/quest marker always lands exactly on a state's vertex — the same point the
  // settlement icon occupies — so markers stack as small badges on the icon's left side instead,
  // vertically centered as a group on that same point. The group's final size has to be known
  // before laying any of its members out, so markers are collected here first and grouped by
  // state, rather than positioned one at a time as each is added.
  const markerInputs: Array<{ stateId: string; id: string; data: MarkerData }> = [
    {
      stateId: playerStateId,
      id: 'marker-player',
      data: { kind: 'player', label: 'You are here' },
    },
    ...corpses.map((corpse) => ({
      stateId: corpse.stateId,
      id: `marker-corpse-${corpse.id}`,
      data: { kind: 'corpse' as const, label: corpse.name, itemCount: corpse.itemCount },
    })),
    ...questMarkers.map((marker) => ({
      stateId: marker.stateId,
      id: `marker-quest-${marker.questId}-${marker.stateId}`,
      data: { kind: 'quest' as const, label: marker.objectiveName },
    })),
  ];

  const markerInputsByState = new Map<string, typeof markerInputs>();
  for (const input of markerInputs) {
    if (!stateById.has(input.stateId)) continue;
    const group = markerInputsByState.get(input.stateId) ?? [];
    group.push(input);
    markerInputsByState.set(input.stateId, group);
  }

  const markerNodes: MarkerFlowNode[] = [];
  const markerConnectorNodes: MarkerConnectorFlowNode[] = [];
  const connectorStrokeWidth = scale.stateStrokeWidth * 2;

  for (const [stateId, group] of markerInputsByState) {
    const state = stateById.get(stateId)!;
    const stackHeight = group.length * scale.markerSize + (group.length - 1) * scale.markerGap;
    const stackTop = -stackHeight / 2;
    const dx = -(ICON_FLOW.state / 2 + scale.markerGap + scale.markerSize / 2);

    group.forEach((input, slot) => {
      const dy = stackTop + slot * (scale.markerSize + scale.markerGap) + scale.markerSize / 2;

      markerNodes.push({
        id: input.id,
        type: 'marker',
        position: {
          x: state.center.x + dx - scale.markerSize / 2,
          y: state.center.y + dy - scale.markerSize / 2,
        },
        style: { width: scale.markerSize, height: scale.markerSize },
        zIndex: Z_INDEX.marker,
        draggable: false,
        selectable: false,
        data: input.data,
      });

      // The connector node's own box has to contain both the vertex (local 0,0) and the badge
      // offset (dx, dy) — translated into that box's own local space, same pattern as the
      // country/state border polygons above.
      const connectorBox = boundingBoxOf([
        { x: 0, y: 0 },
        { x: dx, y: dy },
      ]);
      markerConnectorNodes.push({
        id: `${input.id}-connector`,
        type: 'marker-connector',
        position: {
          x: state.center.x + connectorBox.minX,
          y: state.center.y + connectorBox.minY,
        },
        style: {
          // A single-marker group's badge sits level with the vertex (dy = 0), giving a
          // zero-height box — floor both dimensions so the node is never degenerate.
          width: Math.max(connectorBox.width, connectorStrokeWidth),
          height: Math.max(connectorBox.height, connectorStrokeWidth),
        },
        zIndex: Z_INDEX.markerConnector,
        draggable: false,
        selectable: false,
        data: {
          from: { x: -connectorBox.minX, y: -connectorBox.minY },
          to: { x: dx - connectorBox.minX, y: dy - connectorBox.minY },
          strokeWidth: connectorStrokeWidth,
        },
      });
    });
  }

  const roadEdges = roads.map((road) => ({
    id: road.id,
    source: road.originStateId,
    target: road.destinationStateId,
    type: 'road',
    zIndex: Z_INDEX.road,
    style: { strokeWidth: scale.roadStrokeWidth },
  }));

  return {
    nodes: [
      ...countryBorders,
      ...stateBorders,
      ...markerConnectorNodes,
      ...countryLabels,
      ...stateLabels,
      ...markerNodes,
    ],
    edges: roadEdges,
  };
}

function polygonPoints(points: PointResponse[]): string {
  return points.map((point) => `${point.x},${point.y}`).join(' ');
}

// 0 = perfectly constant on-screen size below the floor (full compensation); 1 = no clamp at all
// (icons would shrink fully proportionally with zoom, exactly like the un-clamped state/country
// polygons). A fully-constant icon, held at its floor-zoom size all the way down to MIN_ZOOM,
// visually dwarfs its own (fully zoom-scaled, so genuinely shrinking) state polygon the further
// out the map is zoomed — this softens that: icons still shrink past the floor, just slower than
// the polygons around them, so they stay proportionate without disappearing at extreme zoom-out.
const ZOOM_FLOOR_DAMPING = 0.5;

// The inverse-scale factor that keeps something close to a constant on-screen size below `floor`
// zoom (see ZOOM_FLOOR_DAMPING), growing normally above it — shared by ZoomFloorConstant (for
// scaling content) and any node that also needs a screen-space-constant *offset* alongside that
// content (see CountryLabelNode: a flow-space distance multiplied by this same factor before
// being scaled by the live zoom keeps a proportionate screen distance, exactly like the content
// it's positioned relative to).
function zoomFloorCompensation(zoom: number, floor: number): number {
  return zoom < floor ? (floor / zoom) ** (1 - ZOOM_FLOOR_DAMPING) : 1;
}

// Inverse-scales its children below `floor`, so they stay a constant on-screen size no matter
// how far zoomed out the map is — above `floor`, content scales up naturally with zoom like any
// other map geometry. Used for icons and the country label, which must stay visible/legible
// across the entire zoom range while still growing normally once the player zooms in.
function ZoomFloorConstant({
  zoom,
  floor,
  className,
  style,
  children,
}: {
  zoom: number;
  floor: number;
  className?: string;
  style?: CSSProperties;
  children: ReactNode;
}) {
  const compensation = zoomFloorCompensation(zoom, floor);
  return (
    <div
      className={className}
      style={{ ...style, transform: `${style?.transform ?? ''} scale(${compensation})` }}
    >
      {children}
    </div>
  );
}

function CountryBorderNode({ data }: NodeProps<CountryBorderFlowNode>) {
  return (
    <svg className="pointer-events-none absolute inset-0 h-full w-full overflow-visible">
      <polygon
        points={polygonPoints(data.points)}
        fill={data.color}
        fillOpacity={0.12}
        stroke={COUNTRY_BORDER_COLOR}
        strokeOpacity={COUNTRY_BORDER_OPACITY}
        strokeWidth={data.strokeWidth}
      />
    </svg>
  );
}

// A bordered plaque, not floating text — a solid, controlled background of its own, so it needs
// no halo/opacity tricks to stay legible over whatever's behind it. Sized to its content
// naturally via normal box-model padding, unlike an SVG shape which would need its text measured
// first to know how wide to draw.
function CountryLabelNode({ data }: NodeProps<CountryLabelFlowNode>) {
  const { zoom } = useViewport();
  // The node's own box is centered exactly on the capital's point (see buildWorldMap) — the
  // clearance above it (half the icon's own on-screen size, plus a small gap) is applied here,
  // at render time, multiplied by the same zoom-floor compensation factor the icon's own size
  // uses, so the gap stays a constant screen distance instead of shrinking away at low zoom.
  const clearance =
    (ICON_FLOW.state / 2 + ICON_FLOW.state * 0.25) *
    zoomFloorCompensation(zoom, DETAIL_ZOOM_THRESHOLD);

  return (
    // Anchored at the node box's own center (50%/50%), not its top-left — the box itself is
    // already positioned so that center lands exactly on the capital (see buildWorldMap). The
    // translate must live on this same, absolutely-positioned element as the zoom-compensation
    // scale, matching the fix in StateLabelNode/MarkerNode, or the plaque drifts off the capital.
    <div className="pointer-events-none absolute" style={{ left: '50%', top: '50%' }}>
      <ZoomFloorConstant
        zoom={zoom}
        floor={DETAIL_ZOOM_THRESHOLD}
        // transform-origin must move to the same point this transform anchors (the box's own
        // bottom-center) — left at the CSS default (the box's center), the scale would pivot
        // around a different point than the translate targets, drifting the plaque's clearance
        // above the icon by more the further zoomed out the map is (the same class of bug fixed
        // in StateLabelNode's icon centering).
        style={{
          transformOrigin: '50% 100%',
          transform: `translate(-50%, calc(-100% - ${clearance}px))`,
        }}
      >
        <span
          className="rounded-sm whitespace-nowrap"
          style={{
            display: 'inline-block',
            padding: '0.2em 0.7em',
            fontFamily: MAP_FONT,
            fontSize: FONT_FLOW.country,
            fontWeight: 500,
            letterSpacing: '0.05em',
            color: BANNER_TEXT_COLOR,
            background: hexWithAlpha(data.color, BANNER_BACKGROUND_OPACITY),
            border: `${FONT_FLOW.country * 0.08}px solid ${hexWithAlpha(BANNER_BORDER_COLOR, BANNER_BACKGROUND_OPACITY)}`,
          }}
        >
          {data.name}
        </span>
      </ZoomFloorConstant>
    </div>
  );
}

// Urban and rural states share the same fill/border treatment — the icon (castle/house vs.
// mountain) already carries that distinction, so the region itself doesn't need to repeat it.
function StateBorderNode({ data }: NodeProps<StateBorderFlowNode>) {
  return (
    <svg className="pointer-events-none absolute inset-0 h-full w-full overflow-visible">
      <polygon
        points={polygonPoints(data.points)}
        fill={data.color}
        fillOpacity={0.4}
        stroke={data.color}
        strokeWidth={data.strokeWidth}
      />
    </svg>
  );
}

function StateLabelNode({ data }: NodeProps<StateLabelFlowNode>) {
  const { city } = data;
  const { zoom } = useViewport();
  const Icon = city ? (city.isCapital ? GiCastle : GiHouse) : GiMountains;
  const label = city ? city.name : data.name;
  const iconSize = ICON_FLOW.state;
  const anchor = LABEL_ANCHOR_SIZE / 2;

  return (
    <div className="relative h-full w-full">
      <Handle
        type="target"
        position={Position.Left}
        style={{ left: anchor, top: anchor, opacity: 0 }}
      />
      <Handle
        type="source"
        position={Position.Right}
        style={{ left: anchor, top: anchor, opacity: 0 }}
      />

      <HoverPopover>
        <HoverPopoverTrigger asChild>
          <div className="absolute cursor-help" style={{ left: anchor, top: anchor }}>
            {/* The centering translate must live on this same element as the zoom-compensation
                scale, not on a separately-transformed child — a scale on an ancestor re-scales a
                descendant's own translate around the ancestor's pivot, drifting the icon away
                from the vertex by more the further zoomed out the map is. */}
            <ZoomFloorConstant
              zoom={zoom}
              floor={DETAIL_ZOOM_THRESHOLD}
              style={{ transform: 'translate(-50%, -50%)' }}
            >
              {/* This box, not the label below it, is what centers on the vertex — the label is
                  positioned absolute, so it hangs below without affecting this box's own size
                  (and so without dragging the icon's center off the point, the old "banner
                  stacked above the vertex" bug this replaces). */}
              <div className="relative" style={{ width: iconSize, height: iconSize }}>
                {/* The icon lives INSIDE the backdrop (parent/child), not beside it as a sibling
                    — a position:absolute sibling always paints after a static one regardless of
                    DOM order (CSS stacking rules), so a same-level backdrop div would paint over
                    the icon and hide it completely. Nesting sidesteps that: a child always paints
                    after its own parent's background. The backdrop itself is opaque, not just the
                    glyph's own fill, because these icons (the wilderness mountain ring especially)
                    are mostly hollow line art with lots of transparent negative space, so a road
                    or connector stem tucked underneath (see Z_INDEX/paint order) would otherwise
                    still show straight through the icon's own gaps instead of being hidden by it. */}
                <div
                  className="absolute flex items-center justify-center rounded-full"
                  style={{
                    left: '50%',
                    top: '50%',
                    width: iconSize * 1.3,
                    height: iconSize * 1.3,
                    transform: 'translate(-50%, -50%)',
                    background: PARCHMENT,
                    border: `${iconSize * 0.03}px solid ${ICON_OUTLINE_COLOR}`,
                  }}
                >
                  {/* Sized well below the backdrop's own diameter, not just slightly smaller — a
                      glyph's artwork reaches toward the corners of its own square bounding box,
                      and a circle only a little larger than that box still doesn't cover those
                      corners (a circle's radius undershoots a square's half-diagonal), so the
                      towers/peaks poke past the backdrop's edge unless there's real margin. Same
                      ratio MarkerNode already uses successfully for its own icon-in-badge sizing. */}
                  <Icon
                    style={{
                      width: iconSize * 0.75,
                      height: iconSize * 0.75,
                      color: city ? INK : INK_MUTED,
                      stroke: ICON_OUTLINE_COLOR,
                      strokeWidth: iconSize * 0.045,
                      paintOrder: 'stroke',
                    }}
                  />
                </div>
                <span
                  className="absolute font-semibold whitespace-nowrap"
                  style={{
                    top: '100%',
                    left: '50%',
                    transform: 'translateX(-50%)',
                    marginTop: iconSize * 0.12,
                    fontFamily: MAP_FONT,
                    fontSize: FONT_FLOW.state,
                    color: INK,
                    // A parchment-colored halo (not a background chip) keeps the name legible
                    // over whatever's directly behind it — parchment, a state's fill, or a road —
                    // the standard cartographic label treatment, matching the map's own fixed
                    // parchment surface color rather than the app's own (themeable) surfaces. A
                    // continuous text-stroke, not a few offset text-shadow copies: shadow copies
                    // only cover discrete directions and leave visible gaps a road this thick
                    // shows straight through at most angles. paintOrder keeps the stroke from
                    // eating into (thickening) the letterforms themselves, same as the mockup's
                    // own SVG technique (stroke painted before the fill, not after).
                    WebkitTextStroke: `${FONT_FLOW.state * 0.22}px ${PARCHMENT}`,
                    paintOrder: 'stroke',
                  }}
                >
                  {label}
                </span>
              </div>
            </ZoomFloorConstant>
          </div>
        </HoverPopoverTrigger>

        <HoverPopoverContent side="top">
          <div className="space-y-1">
            <div className="font-semibold">{label}</div>
            <p>{data.description}</p>
          </div>
        </HoverPopoverContent>
      </HoverPopover>
    </div>
  );
}

const MARKER_ICON: Record<MarkerKind, typeof GiPerson> = {
  player: GiPerson,
  corpse: GiTombstone,
  quest: GiScrollUnfurled,
};

function MarkerNode({ data }: NodeProps<MarkerFlowNode>) {
  const { zoom } = useViewport();
  const Icon = MARKER_ICON[data.kind];

  return (
    <div className="relative h-full w-full">
      <HoverPopover>
        <HoverPopoverTrigger asChild>
          <div className="absolute cursor-help" style={{ left: '50%', top: '50%' }}>
            {/* Same centering pattern as StateLabelNode: this box, not the node's own (layout-only)
                box, is what centers on the stacked slot point the connector stem targets. */}
            <ZoomFloorConstant
              zoom={zoom}
              floor={DETAIL_ZOOM_THRESHOLD}
              style={{ transform: 'translate(-50%, -50%)' }}
            >
              <div
                className="flex items-center justify-center rounded-full border shadow"
                style={{
                  width: ICON_FLOW.marker,
                  height: ICON_FLOW.marker,
                  background: data.kind === 'player' ? GOLD : PARCHMENT,
                  borderColor: data.kind === 'player' ? PARCHMENT : INK,
                }}
              >
                <Icon
                  style={{
                    width: ICON_FLOW.markerIcon,
                    height: ICON_FLOW.markerIcon,
                    color: data.kind === 'player' ? PARCHMENT : INK,
                  }}
                />
              </div>
            </ZoomFloorConstant>
          </div>
        </HoverPopoverTrigger>

        <HoverPopoverContent side="top">
          <div className="space-y-1">
            <div className="font-semibold">{data.label}</div>
            {data.itemCount != null && <p>{data.itemCount} item(s)</p>}
          </div>
        </HoverPopoverContent>
      </HoverPopover>
    </div>
  );
}

// A marker's badge floats beside its state's icon (see buildWorldMap) with nothing else tying it
// to the exact point it marks — this stem draws that connection, from the real vertex to the
// badge. Its vertex end sits underneath the settlement icon (see the Z_INDEX ordering), the same
// way a road visually terminates at the icon it connects to, so it reads as "planted here".
function MarkerConnectorNode({ data }: NodeProps<MarkerConnectorFlowNode>) {
  return (
    <svg className="pointer-events-none absolute inset-0 h-full w-full overflow-visible">
      <line
        x1={data.from.x}
        y1={data.from.y}
        x2={data.to.x}
        y2={data.to.y}
        stroke={ICON_OUTLINE_COLOR}
        strokeWidth={data.strokeWidth}
        strokeLinecap="round"
      />
    </svg>
  );
}

// Each road is its own independent line between two settlement icons — no name, thin, and
// translucent, a quiet connection rather than a labeled band. The icon at each end (painted
// above roads via Z_INDEX) already covers the shared vertex point, so overlapping road segments
// at a junction blend together visually without needing an explicit hub shape.
function RoadEdge({ id, sourceX, sourceY, targetX, targetY, style }: EdgeProps) {
  const [edgePath] = getStraightPath({ sourceX, sourceY, targetX, targetY });

  return (
    <BaseEdge
      id={id}
      path={edgePath}
      style={{ ...style, stroke: ROAD_COLOR, strokeOpacity: ROAD_OPACITY, strokeLinecap: 'round' }}
    />
  );
}

const nodeTypes = {
  'country-border': CountryBorderNode,
  'country-label': CountryLabelNode,
  'state-border': StateBorderNode,
  'state-label': StateLabelNode,
  marker: MarkerNode,
  'marker-connector': MarkerConnectorNode,
};

const edgeTypes = {
  road: RoadEdge,
};
