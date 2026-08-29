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

// Fixed parchment/ink palette — the map is a paper map, not themed with the app's own light/dark mode.
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
const ROAD_OPACITY = 0.45;

// One fixed, opaque border color for every country (not each country's own color) — a shared edge is drawn twice, once by each side's polygon, and same-color double-draws are invisible where two different translucent colors would visibly stack.
const COUNTRY_BORDER_COLOR = INK;
const COUNTRY_BORDER_OPACITY = 0.35;

// Icons get a stroke outline so they stay legible against any background, most noticeably the mountain glyph over a road.
const ICON_OUTLINE_COLOR = INK;

// Translucent, not solid — the banner draws above every marker (see Z_INDEX) so it's never hidden behind one, without permanently blotting one out either.
const BANNER_BORDER_COLOR = GOLD;
const BANNER_TEXT_COLOR = PARCHMENT;
const BANNER_BACKGROUND_OPACITY = 0.92;

// Clamped short of a normal-sized world shrinking to an illegible speck at full zoom-out.
const MIN_ZOOM = 0.06;
const MAX_ZOOM = 6;

// Below this zoom, icons (see ZoomFloorConstant) stay a constant on-screen size instead of shrinking to illegible dots as the map zooms out.
const DETAIL_ZOOM_THRESHOLD = 0.15;

// One shared icon size per zoom level — shape (castle/house/mountain) already distinguishes capital/city/rural, so size doesn't need to repeat it.
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

// Voronoi-neighbor states share exact vertex coordinates for their common edge, so it's deduplicated across states instead of each one stroking its own whole loop, which double-draws every internal edge — invisible for a solid stroke, but a dashed one shows two independently-phased patterns overlapping.
function dedupedStateEdges(
  states: StateMapResponse[],
  countryColors: Map<string, string>,
): StateBorderEdge[] {
  const roundCoord = (value: number) => Math.round(value * 100) / 100;
  const pointKey = (point: PointResponse) => `${roundCoord(point.x)},${roundCoord(point.y)}`;

  const edgesByKey = new Map<string, StateBorderEdge>();
  for (const state of states) {
    const color = countryColors.get(state.countryId)!;
    const boundary = state.boundary;
    for (let i = 0; i < boundary.length; i++) {
      const from = boundary[i];
      const to = boundary[(i + 1) % boundary.length];
      const key = [pointKey(from), pointKey(to)].sort().join('|');
      if (!edgesByKey.has(key)) {
        edgesByKey.set(key, { from, to, color });
      }
    }
  }

  return [...edgesByKey.values()];
}

function computeScale(countries: CountryMapResponse[], states: StateMapResponse[]): MapScale {
  const points =
    countries.length > 0
      ? countries.flatMap((country) => country.boundary)
      : states.flatMap((state) => state.boundary);
  const box = points.length > 0 ? boundingBoxOf(points) : null;
  const extent = box ? Math.max(box.width, box.height) : 1000;

  return {
    // Thin — a bold, opaque stroke flush with the world's own boundary would read as a hard frame around the whole map.
    countryStrokeWidth: extent * 0.0016,
    stateStrokeWidth: extent * 0.001,
    // Roads are the map's primary connective feature, bolder than the hairline borders — 3x the country border width.
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

// Indexes into a stable (id-sorted) ordering rather than hashing each country's own id — a hash bucketed into only as many slots as palette colors collides readily once country count approaches palette size.
function colorsByCountryId(countries: CountryMapResponse[]): Map<string, string> {
  const sorted = [...countries].sort((a, b) => a.id.localeCompare(b.id));
  return new Map(
    sorted.map((country, index) => [country.id, COUNTRY_PALETTE[index % COUNTRY_PALETTE.length]]),
  );
}

// Paint order low to high: borders, roads, connector stems, state labels, markers, country banner — so nothing meant to sit underneath something else ends up overlapping it. Country above state (not the reverse) specifically: wherever a state sits at its own country's outer edge, that segment is drawn by both — country on top lets its solid stroke win cleanly there instead of fighting the state's dashed one.
const Z_INDEX = {
  stateBorder: 0,
  countryBorder: 1,
  road: 2,
  markerConnector: 3,
  countryLabelConnector: 3,
  stateLabel: 4,
  marker: 5,
  countryLabel: 6,
};

type CountryBorderData = { points: PointResponse[]; color: string; strokeWidth: number };
type CountryLabelData = { name: string; color: string };
type CountryLabelConnectorData = { strokeWidth: number };
type StateBorderData = { points: PointResponse[]; color: string };
// A deduplicated edge, shared by both neighboring states that border it — see dedupedStateEdges.
type StateBorderEdge = { from: PointResponse; to: PointResponse; color: string };
type StateBorderLinesData = { edges: StateBorderEdge[]; strokeWidth: number };
type StateLabelData = {
  name: string;
  description: string;
  color: string;
  city: CityMapResponse | null;
};
type MarkerKind = 'player' | 'corpse' | 'quest';
type MarkerData = { kind: MarkerKind; label: string; itemCount?: number };
// A vertex-to-badge connector stem, already translated into the node's own local box space — `from` is the vertex, `to` the badge's position.
type MarkerConnectorData = { from: PointResponse; to: PointResponse; strokeWidth: number };

type CountryBorderFlowNode = Node<CountryBorderData, 'country-border'>;
type CountryLabelFlowNode = Node<CountryLabelData, 'country-label'>;
type CountryLabelConnectorFlowNode = Node<CountryLabelConnectorData, 'country-label-connector'>;
type StateBorderFlowNode = Node<StateBorderData, 'state-border'>;
type StateBorderLinesFlowNode = Node<StateBorderLinesData, 'state-border-lines'>;
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
  const countryLabelConnectors: CountryLabelConnectorFlowNode[] = [];

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

    // A country polygon's own bounding box can fall outside its (often concave) shape, so the label anchors on the capital instead, falling back to the average of member states' centers, then the box center.
    const memberStates = statesByCountryId.get(country.id) ?? [];
    const capitalState = memberStates.find((state) => cityByStateId.get(state.id)?.isCapital);
    // Anchored on the capital's own point, same as its icon — CountryLabelNode applies the upward clearance itself at render time, scaled by the live zoom-floor factor, so it doesn't shrink away at low zoom.
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

    if (capitalState) {
      countryLabelConnectors.push({
        id: `${country.id}-label-connector`,
        type: 'country-label-connector',
        position: {
          x: capitalState.center.x - LABEL_ANCHOR_SIZE / 2,
          y: capitalState.center.y - LABEL_ANCHOR_SIZE / 2,
        },
        style: { width: LABEL_ANCHOR_SIZE, height: LABEL_ANCHOR_SIZE },
        zIndex: Z_INDEX.countryLabelConnector,
        draggable: false,
        selectable: false,
        data: { strokeWidth: scale.stateStrokeWidth * 2 },
      });
    }
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
      },
    });

    stateLabels.push({
      // Roads attach to this node's id, so they terminate at the settlement point, not the border polygon's bounding box.
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

  const stateEdges = dedupedStateEdges(states, countryColors);
  const stateBorderLinesNode: StateBorderLinesFlowNode[] = [];
  if (stateEdges.length > 0) {
    const edgesBox = boundingBoxOf(stateEdges.flatMap((edge) => [edge.from, edge.to]));
    stateBorderLinesNode.push({
      id: 'state-border-lines',
      type: 'state-border-lines',
      position: { x: edgesBox.minX, y: edgesBox.minY },
      style: { width: edgesBox.width, height: edgesBox.height },
      zIndex: Z_INDEX.stateBorder,
      draggable: false,
      selectable: false,
      data: {
        edges: stateEdges.map((edge) => ({
          from: { x: edge.from.x - edgesBox.minX, y: edge.from.y - edgesBox.minY },
          to: { x: edge.to.x - edgesBox.minX, y: edge.to.y - edgesBox.minY },
          color: edge.color,
        })),
        strokeWidth: scale.stateStrokeWidth,
      },
    });
  }

  const stateById = new Map(states.map((state) => [state.id, state]));

  // Markers stack as badges beside the settlement icon they share a vertex with — collected by state first since the group's total size must be known before laying out its members.
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

      // The connector node's box must contain both the vertex (0,0) and the badge offset (dx,dy), translated into its own local space, same pattern as the border polygons.
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
          // A single-marker group's badge sits level with the vertex (dy=0) — floor both dimensions so the node is never degenerate.
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
      ...stateBorderLinesNode,
      ...markerConnectorNodes,
      ...countryLabelConnectors,
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

// 0 = constant size below the floor; 1 = no clamp (shrinks like the un-clamped state polygons) — partial damping keeps icons legible without dwarfing their own shrinking state at extreme zoom-out.
const ZOOM_FLOOR_DAMPING = 0.5;

// Inverse-scale factor for a near-constant on-screen size below floor zoom — shared by ZoomFloorConstant and anything needing a matching constant-screen-space offset (see CountryLabelNode).
function zoomFloorCompensation(zoom: number, floor: number): number {
  return zoom < floor ? (floor / zoom) ** (1 - ZOOM_FLOOR_DAMPING) : 1;
}

// Inverse-scales children below floor zoom so they stay legible instead of shrinking to illegible dots; scales normally above it.
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
        strokeLinejoin="round"
      />
    </svg>
  );
}

// The banner's clearance above the capital's icon, scaled by the same zoom-floor factor the icon's own size uses, so the gap stays constant instead of shrinking away at low zoom — shared by CountryLabelNode (to position the banner) and CountryLabelConnectorNode (to draw the stem to it).
function bannerClearance(zoom: number): number {
  return (
    (ICON_FLOW.state / 2 + ICON_FLOW.state * 0.25) *
    zoomFloorCompensation(zoom, DETAIL_ZOOM_THRESHOLD)
  );
}

// A bordered plaque, not floating text — a solid background of its own needs no halo/opacity tricks, and sizes to its content via normal box-model padding.
function CountryLabelNode({ data }: NodeProps<CountryLabelFlowNode>) {
  const { zoom } = useViewport();
  const clearance = bannerClearance(zoom);

  return (
    // Anchored at the node box's own center, not its top-left — the translate must live on this same element as the scale, or the plaque drifts off the capital.
    <div className="pointer-events-none absolute" style={{ left: '50%', top: '50%' }}>
      <ZoomFloorConstant
        zoom={zoom}
        floor={DETAIL_ZOOM_THRESHOLD}
        // transform-origin must match the point this transform anchors (bottom-center), or the scale pivots elsewhere and the clearance drifts at low zoom.
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
            boxShadow: '0 3px 6px rgba(0, 0, 0, 0.55)',
          }}
        >
          {data.name}
        </span>
      </ZoomFloorConstant>
    </div>
  );
}

function CountryLabelConnectorNode({ data }: NodeProps<CountryLabelConnectorFlowNode>) {
  const { zoom } = useViewport();
  const clearance = bannerClearance(zoom);
  const anchor = LABEL_ANCHOR_SIZE / 2;

  return (
    <svg className="pointer-events-none absolute inset-0 h-full w-full overflow-visible">
      <line
        x1={anchor}
        y1={anchor}
        x2={anchor}
        y2={anchor - clearance}
        stroke={ICON_OUTLINE_COLOR}
        strokeWidth={data.strokeWidth}
        strokeLinecap="round"
      />
    </svg>
  );
}

// Fill only (urban/rural states share the same treatment) — the border line itself is drawn once per shared edge by StateBorderLinesNode instead of once per state.
function StateBorderNode({ data }: NodeProps<StateBorderFlowNode>) {
  return (
    <svg className="pointer-events-none absolute inset-0 h-full w-full overflow-visible">
      <polygon points={polygonPoints(data.points)} fill={data.color} fillOpacity={0.4} />
    </svg>
  );
}

// Dashed, not solid like the country border — the cartographic distinction between a soft/administrative boundary and a hard/authoritative one. Dash/gap sized off strokeWidth so the ratio stays proportionate at any zoom or world size.
function StateBorderLinesNode({ data }: NodeProps<StateBorderLinesFlowNode>) {
  return (
    <svg className="pointer-events-none absolute inset-0 h-full w-full overflow-visible">
      {data.edges.map((edge, index) => (
        <line
          key={index}
          x1={edge.from.x}
          y1={edge.from.y}
          x2={edge.to.x}
          y2={edge.to.y}
          stroke={edge.color}
          strokeOpacity={0.35}
          strokeWidth={data.strokeWidth}
          strokeLinecap="round"
          strokeDasharray={`${data.strokeWidth * 3} ${data.strokeWidth * 2}`}
        />
      ))}
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
          {/* The trigger element itself must carry the zoom-compensation scale (not a nested child) — Radix measures this exact element's own bounding rect to position the tooltip, and a transform on a descendant doesn't grow this element's own rect to match, which is what made the tooltip anchor to the wrong (untransformed, roughly icon-sized) box. */}
          <div
            className="absolute cursor-help"
            style={{
              left: anchor,
              top: anchor,
              transform: `translate(-50%, -50%) scale(${zoomFloorCompensation(zoom, DETAIL_ZOOM_THRESHOLD)})`,
            }}
          >
            {/* This box (not the label below it) is what centers on the vertex — the label is absolutely positioned, so it doesn't affect this box's own size. */}
            <div className="relative" style={{ width: iconSize, height: iconSize }}>
              {/* The icon lives inside the backdrop, not beside it — a position:absolute sibling paints after a static one regardless of DOM order, so nesting it as a child is what keeps the opaque backdrop from hiding the icon. Opaque because these glyphs are mostly hollow line art, so a stem/road tucked underneath would otherwise show through the gaps. */}
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
                  boxShadow: '0 3px 6px rgba(0, 0, 0, 0.55)',
                }}
              >
                {/* Sized well below the backdrop's diameter — a glyph's artwork reaches its box's corners, and a circle only a little larger than the box still doesn't cover those corners. */}
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
                  // A continuous parchment text-stroke halo (not a background chip or offset shadow copies, which leave gaps a road shows through) keeps the name legible over anything behind it.
                  WebkitTextStroke: `${FONT_FLOW.state * 0.22}px ${PARCHMENT}`,
                  paintOrder: 'stroke',
                }}
              >
                {label}
              </span>
            </div>
          </div>
        </HoverPopoverTrigger>

        <HoverPopoverContent
          side="top"
          className="border border-[#a9761f] bg-[#ece0c3] text-[#3a2d1f]"
        >
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
          {/* Same centering pattern as StateLabelNode — the scale must live on this trigger element itself, not a nested child, or Radix anchors the tooltip to the wrong (untransformed) box. This box, not the node's own layout box, centers on the stacked slot point the connector stem targets. */}
          <div
            className="absolute cursor-help"
            style={{
              left: '50%',
              top: '50%',
              transform: `translate(-50%, -50%) scale(${zoomFloorCompensation(zoom, DETAIL_ZOOM_THRESHOLD)})`,
            }}
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
          </div>
        </HoverPopoverTrigger>

        <HoverPopoverContent
          side="top"
          className="border border-[#a9761f] bg-[#ece0c3] text-[#3a2d1f]"
        >
          <div className="space-y-1">
            <div className="font-semibold">{data.label}</div>
            {data.itemCount != null && <p>{data.itemCount} item(s)</p>}
          </div>
        </HoverPopoverContent>
      </HoverPopover>
    </div>
  );
}

// Draws the connection from a marker's badge back to the real vertex it marks — the vertex end tucks underneath the settlement icon, the same way a road visually terminates at it.
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

// A thin, translucent, unlabeled line — the icon at each end already covers the shared vertex, so overlapping segments at a junction blend together without an explicit hub shape.
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
  'country-label-connector': CountryLabelConnectorNode,
  'state-border': StateBorderNode,
  'state-border-lines': StateBorderLinesNode,
  'state-label': StateLabelNode,
  marker: MarkerNode,
  'marker-connector': MarkerConnectorNode,
};

const edgeTypes = {
  road: RoadEdge,
};
