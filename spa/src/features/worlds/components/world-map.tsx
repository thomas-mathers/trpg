import {
  Background,
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
import { cn } from '@/lib/utils';

import '@xyflow/react/dist/style.css';

const COUNTRY_PALETTE = [
  '#b45309',
  '#0f766e',
  '#7c3aed',
  '#be123c',
  '#1d4ed8',
  '#a16207',
  '#15803d',
  '#a21caf',
];

const ROAD_COLOR = '#78350f';
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
const COUNTRY_BORDER_COLOR = '#44403c';
const COUNTRY_BORDER_OPACITY = 0.35;

// A settlement icon's own artwork isn't necessarily centered in its viewBox, and it sits over
// varying backgrounds (a road, another state's fill, open map) — a stroke outline (not a blur)
// keeps every icon legible regardless of what's behind it, most noticeably the wilderness
// mountain glyph against the road's similarly dark tone.
const ICON_OUTLINE_COLOR = '#1c1917';

// Banner-style country name plaque — a solid, controlled background of its own, so it needs its
// own text/border colors rather than reusing map-surface tokens.
const BANNER_BORDER_COLOR = '#ca8a04';
const BANNER_TEXT_COLOR = '#fdf6e3';

const MIN_ZOOM = 0.02;
const MAX_ZOOM = 6;

// Icons (settlements, wilderness markers, player/corpse/quest pins) are always visible, at
// whatever zoom the map is at — they're floor-clamped (see ZoomFloorConstant below) to a
// constant on-screen size below this zoom, then grow naturally with the map above it. Text
// labels are noisier at a glance (dozens of state/city names at once is illegible clutter) and
// only appear once the player has zoomed in this far to pick out a specific region.
const DETAIL_ZOOM_THRESHOLD = 0.35;

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
    <div className="trpg-world-map min-h-0 flex-1">
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
      >
        <Background />
      </ReactFlow>
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
    // Thinner and quieter (was 0.0032) — a wide, near-opaque band read as a labeled road sign
    // rather than a quiet line connecting two places, especially once road names were dropped.
    roadStrokeWidth: extent * 0.0011,
    markerSize: extent * 0.022,
    markerGap: extent * 0.006,
  };
}

function colorForCountry(countryId: string): string {
  let hash = 0;
  for (const char of countryId) {
    hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  }
  return COUNTRY_PALETTE[hash % COUNTRY_PALETTE.length];
}

// z-index tiers keep every border below roads below marker connector stems below every label
// below every marker badge, regardless of draw order — otherwise a later, spatially-overlapping
// border can paint over an earlier label, country/state fills would otherwise sit on top of the
// roads crossing them, and a marker's connector stem would paint over the settlement icon it's
// meant to visually tuck underneath instead.
const Z_INDEX = {
  countryBorder: 0,
  stateBorder: 1,
  road: 2,
  markerConnector: 3,
  countryLabel: 4,
  stateLabel: 5,
  marker: 6,
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
    const color = colorForCountry(country.id);

    countryBorders.push({
      id: `${country.id}-border`,
      type: 'country-border',
      position: { x: box.minX, y: box.minY },
      style: { width: box.width, height: box.height, zIndex: Z_INDEX.countryBorder },
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
    const labelCenter = capitalState
      ? // Offset above the capital's own icon — which is centered on the point itself, so this
        // only needs to clear half its size plus a small gap, not the icon+name stack below it.
        { x: capitalState.center.x, y: capitalState.center.y - ICON_FLOW.state * 0.75 }
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
      style: { width: LABEL_ANCHOR_SIZE, height: LABEL_ANCHOR_SIZE, zIndex: Z_INDEX.countryLabel },
      draggable: false,
      selectable: false,
      data: { name: country.name, color },
    });
  }

  const stateBorders: StateBorderFlowNode[] = [];
  const stateLabels: StateLabelFlowNode[] = [];

  for (const state of states) {
    const box = boundingBoxOf(state.boundary);
    const color = colorForCountry(state.countryId);
    const city = cityByStateId.get(state.id) ?? null;

    stateBorders.push({
      id: `${state.id}-border`,
      type: 'state-border',
      position: { x: box.minX, y: box.minY },
      style: { width: box.width, height: box.height, zIndex: Z_INDEX.stateBorder },
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
      style: { width: LABEL_ANCHOR_SIZE, height: LABEL_ANCHOR_SIZE, zIndex: Z_INDEX.stateLabel },
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
        style: { width: scale.markerSize, height: scale.markerSize, zIndex: Z_INDEX.marker },
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
          zIndex: Z_INDEX.markerConnector,
        },
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
  const compensation = zoom < floor ? floor / zoom : 1;
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

  return (
    <ZoomFloorConstant
      zoom={zoom}
      floor={DETAIL_ZOOM_THRESHOLD}
      className="pointer-events-none"
      style={{ transform: 'translate(-50%, -50%)' }}
    >
      <span
        className="rounded-sm font-semibold tracking-wide whitespace-nowrap uppercase"
        style={{
          display: 'inline-block',
          padding: '0.2em 0.7em',
          fontSize: FONT_FLOW.country,
          color: BANNER_TEXT_COLOR,
          background: data.color,
          border: `${FONT_FLOW.country * 0.08}px solid ${BANNER_BORDER_COLOR}`,
        }}
      >
        {data.name}
      </span>
    </ZoomFloorConstant>
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
  const showDetail = zoom >= DETAIL_ZOOM_THRESHOLD;
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
            <ZoomFloorConstant zoom={zoom} floor={DETAIL_ZOOM_THRESHOLD}>
              {/* This box, not the label below it, is what centers on the vertex — the label is
                  positioned absolute, so it hangs below without affecting this box's own size
                  (and so without dragging the icon's center off the point, the old "banner
                  stacked above the vertex" bug this replaces). */}
              <div
                className="relative"
                style={{ width: iconSize, height: iconSize, transform: 'translate(-50%, -50%)' }}
              >
                <Icon
                  className={city ? 'text-foreground' : 'text-muted-foreground'}
                  style={{
                    width: iconSize,
                    height: iconSize,
                    stroke: ICON_OUTLINE_COLOR,
                    strokeWidth: iconSize * 0.045,
                    paintOrder: 'stroke',
                  }}
                />
                {showDetail && (
                  <span
                    className="bg-card/90 absolute rounded px-1 font-semibold whitespace-nowrap"
                    style={{
                      top: '100%',
                      left: '50%',
                      transform: 'translateX(-50%)',
                      marginTop: iconSize * 0.12,
                      fontSize: FONT_FLOW.state,
                    }}
                  >
                    {label}
                  </span>
                )}
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
    <HoverPopover>
      <HoverPopoverTrigger asChild>
        <div className="cursor-help">
          <ZoomFloorConstant zoom={zoom} floor={DETAIL_ZOOM_THRESHOLD}>
            <div
              className={cn(
                'flex items-center justify-center rounded-full border shadow',
                data.kind === 'player'
                  ? 'bg-primary border-primary-foreground/50 text-primary-foreground'
                  : 'bg-card border-border text-foreground',
              )}
              style={{ width: ICON_FLOW.marker, height: ICON_FLOW.marker }}
            >
              <Icon style={{ width: ICON_FLOW.markerIcon, height: ICON_FLOW.markerIcon }} />
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
