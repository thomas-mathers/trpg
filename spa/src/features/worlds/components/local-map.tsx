import dagre from '@dagrejs/dagre';
import {
  BaseEdge,
  EdgeLabelRenderer,
  Handle,
  Position,
  ReactFlow,
  getStraightPath,
  getSmoothStepPath,
  useReactFlow,
  type Edge,
  type EdgeProps,
  type Node,
  type NodeProps,
} from '@xyflow/react';
import { UserRound, Plus, Minus, Maximize, LocateFixed } from 'lucide-react';
import { useEffect, useMemo, useState, useRef } from 'react';
import { GiDoorway, GiStairs, GiChest, GiLever, GiTombstone } from 'react-icons/gi';

import type {
  LocalMapPassageResponse,
  LocalMapResponse,
  LocalMapRoomResponse,
  PointResponse,
  RoomBoundsResponse,
  LocalMapLockKind,
} from '@/api/client';
import { ROOM_ROLE_ICONS } from '@/features/game/room-role-icons';
import { cn } from '@/lib/utils';

import {
  CORRIDOR_WIDTH,
  CORRIDOR_FLOOR_WIDTH,
  WALL_WIDTH,
  MAP_WALL_COLOR,
  MAP_SCALE,
  corridorGate,
  roundedCorridorPath,
  roomWallPath,
  doorwayJambPath,
  type Doorway,
  type HandleSide,
} from './local-map-geometry';
import { MapLock, MapLockSymbol } from './local-map-lock';
import { RoomMarkers } from './local-map-markers';
import { initialMapViewport } from './local-map-viewport';

import '@xyflow/react/dist/style.css';

interface LocalMapProps {
  map: LocalMapResponse;
}

interface RoomPosition {
  x: number;
  y: number;
}

interface RoomLayout extends RoomPosition {
  width: number;
  height: number;
}

interface FloorTransition {
  floorNumber: number;
  direction: 'up' | 'down';
  isLocked: boolean;
}

const FALLBACK_ROOM_WIDTH = 260;
const FALLBACK_ROOM_HEIGHT = 144;
const ROOM_NODE_TYPE = 'local-room' as const;
const CORRIDOR_WALL_EDGE_TYPE = 'corridor-wall' as const;
const CORRIDOR_FLOOR_EDGE_TYPE = 'corridor-floor' as const;

type RoomNodeData = {
  room: LocalMapRoomResponse;
  isCurrent: boolean;
  doorways: Doorway[];
  floorTransitions: FloorTransition[];
  width: number;
  height: number;
};

type PassageEdgeData = {
  isLocked: boolean;
  isFrontier: boolean;
  lockKind: LocalMapLockKind;
  path: PointResponse[] | null;
};

type RoomFlowNode = Node<RoomNodeData, typeof ROOM_NODE_TYPE>;
type PassageFlowEdge = Edge<
  PassageEdgeData,
  typeof CORRIDOR_WALL_EDGE_TYPE | typeof CORRIDOR_FLOOR_EDGE_TYPE
>;

const nodeTypes = { [ROOM_NODE_TYPE]: RoomNode };
const edgeTypes = {
  [CORRIDOR_WALL_EDGE_TYPE]: CorridorWallEdge,
  [CORRIDOR_FLOOR_EDGE_TYPE]: CorridorFloorEdge,
};

const handlePositions: ReadonlyArray<{ side: HandleSide; position: Position }> = [
  { side: 'top', position: Position.Top },
  { side: 'right', position: Position.Right },
  { side: 'bottom', position: Position.Bottom },
  { side: 'left', position: Position.Left },
];

export function LocalMap({ map }: LocalMapProps) {
  const floors = useMemo(
    () => [...new Set(map.rooms.map((room) => room.floorNumber))].sort((a, b) => a - b),
    [map.rooms],
  );
  const currentFloor = map.rooms.find((room) => room.id === map.currentRoomId)?.floorNumber ?? 0;
  const [selectedFloor, setSelectedFloor] = useState(currentFloor);
  const rooms = useMemo(
    () => map.rooms.filter((room) => room.floorNumber === selectedFloor),
    [map.rooms, selectedFloor],
  );
  const roomIds = useMemo(() => new Set(rooms.map((room) => room.id)), [rooms]);
  const passages = useMemo(
    () =>
      map.passages.filter(
        (passage) => roomIds.has(passage.originRoomId) && roomIds.has(passage.destinationRoomId),
      ),
    [map.passages, roomIds],
  );
  const floorTransitions = useMemo(
    () => getFloorTransitions(map.rooms, map.passages, selectedFloor),
    [map.rooms, map.passages, selectedFloor],
  );
  const { nodes, edges } = useMemo(
    () => buildLocalMap(rooms, passages, floorTransitions, map.currentRoomId),
    [rooms, passages, floorTransitions, map.currentRoomId],
  );
  const { fitView, setViewport, zoomIn, zoomOut } = useReactFlow();
  const canvas = useRef<HTMLDivElement>(null);
  const initializedFloor = useRef('');
  const floorKey = `${map.buildingId}:${selectedFloor}`;

  useEffect(() => setSelectedFloor(currentFloor), [currentFloor]);

  useEffect(() => {
    if (nodes.length === 0 || initializedFloor.current === floorKey) return;

    const animationFrame = requestAnimationFrame(() => {
      const element = canvas.current;
      if (!element) return;
      const { width, height } = element.getBoundingClientRect();
      if (width <= 0 || height <= 0) return;
      void setViewport(
        initialMapViewport(
          nodes.map(({ position, data }) => ({
            ...position,
            width: data.width,
            height: data.height,
            isCurrent: data.isCurrent,
            hasCorpse: data.room.markers.some((marker) => marker.kind === 'PlayerCorpse'),
          })),
          { width, height },
        ),
      );
      initializedFloor.current = floorKey;
    });
    return () => cancelAnimationFrame(animationFrame);
  }, [nodes, fitView, setViewport, floorKey]);

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="parchment-surface flex min-h-0 flex-1 flex-col overflow-hidden">
        <MapHeading
          map={map}
          floors={floors}
          selectedFloor={selectedFloor}
          onFloor={setSelectedFloor}
        />
        <div ref={canvas} className="relative min-h-0 flex-1">
          {nodes.length === 0 ? (
            <div className="flex h-full items-center justify-center">
              <p className="text-muted-foreground text-sm">No rooms are known on this floor yet.</p>
            </div>
          ) : (
            <ReactFlow
              nodes={nodes}
              edges={edges}
              nodeTypes={nodeTypes}
              edgeTypes={edgeTypes}
              colorMode="light"
              minZoom={0.2}
              maxZoom={2}
              nodesFocusable={false}
              nodesConnectable={false}
              nodesDraggable={false}
              elementsSelectable={false}
              zoomOnDoubleClick={false}
              proOptions={{ hideAttribution: true }}
            />
          )}
          <div
            className="bg-card absolute right-3 bottom-3 z-10 flex gap-1 rounded border p-1"
            aria-label="Map controls"
          >
            <button
              type="button"
              className="p-2"
              aria-label="Zoom in"
              onClick={() => void zoomIn()}
            >
              <Plus className="size-4" />
            </button>
            <button
              type="button"
              className="p-2"
              aria-label="Zoom out"
              onClick={() => void zoomOut()}
            >
              <Minus className="size-4" />
            </button>
            <button
              type="button"
              className="p-2"
              aria-label="Fit floor"
              onClick={() => void fitView({ padding: 0.12, minZoom: 0.2, maxZoom: 1.1 })}
            >
              <Maximize className="size-4" />
            </button>
            <button
              type="button"
              className="p-2"
              aria-label="Find player"
              onClick={() => {
                setSelectedFloor(currentFloor);
                void fitView({ nodes: [{ id: map.currentRoomId }], minZoom: 1, maxZoom: 1 });
              }}
            >
              <LocateFixed className="size-4" />
            </button>
          </div>
        </div>
      </div>
      <MapLegend />
    </div>
  );
}

function MapHeading({
  map,
  floors,
  selectedFloor,
  onFloor,
}: {
  map: LocalMapResponse;
  floors: number[];
  selectedFloor: number;
  onFloor: (floor: number) => void;
}) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-3 px-4 py-3">
      <div>
        <h3 className="font-fell text-lg font-medium">{map.buildingName}</h3>
        <p className="text-muted-foreground text-sm">{formatBuildingType(map.buildingType)}</p>
      </div>
      {floors.length > 1 && (
        <div className="flex gap-1" aria-label="Map floor">
          {floors.map((floor) => (
            <button
              key={floor}
              type="button"
              aria-pressed={floor === selectedFloor}
              className={`rounded-md px-3 py-1.5 text-sm ${floor === selectedFloor ? 'bg-foreground text-background' : 'bg-transparent'}`}
              onClick={() => onFloor(floor)}
            >
              {formatFloor(floor)}
              {map.rooms.some(
                (room) =>
                  room.floorNumber === floor &&
                  room.markers.some((marker) => marker.kind === 'PlayerCorpse'),
              ) && (
                <GiTombstone
                  className="ml-1 inline size-4"
                  aria-label="Your corpse on this floor"
                />
              )}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

function RoomNode({ data }: NodeProps<RoomFlowNode>) {
  const { room, isCurrent, floorTransitions, width, height } = data;
  const Icon = room.role ? ROOM_ROLE_ICONS[room.role] : GiDoorway;
  const stackedDetails = floorTransitions.length > 0 && room.markers.length > 0 && width < 300;

  return (
    <div
      className={cn(
        'text-parchment-foreground relative flex flex-col items-center justify-center gap-1.5 px-3 text-center',
        room.isFrontier && 'text-muted-foreground',
      )}
      style={{
        width,
        height,
      }}
    >
      <RoomArchitecture data={data} />
      {handlePositions.flatMap(({ side, position }) => [
        <Handle
          key={`target-${side}`}
          id={`target-${side}`}
          type="target"
          position={position}
          className="pointer-events-none opacity-0"
        />,
        <Handle
          key={`source-${side}`}
          id={`source-${side}`}
          type="source"
          position={position}
          className="pointer-events-none opacity-0"
        />,
      ])}
      {height >= (stackedDetails ? 150 : 120) && (
        <Icon className="relative size-5 shrink-0" aria-hidden="true" />
      )}
      <span className={cn('font-fell relative max-w-full leading-tight', 'text-base')}>
        {room.name}
      </span>
      <div
        className={cn(
          'relative flex items-center justify-center gap-2',
          (floorTransitions.length > 0 || room.markers.length > 0) && 'mt-2',
          stackedDetails && 'flex-col gap-1',
        )}
      >
        <div className="flex items-center justify-center gap-2">
          <RoomMarkers markers={room.markers} />
        </div>
        <FloorTransitions transitions={floorTransitions} />
      </div>
      {isCurrent && (
        <span className="border-background bg-primary text-primary-foreground absolute top-2 right-2 flex size-7 items-center justify-center rounded-full border-2">
          <UserRound className="size-4" aria-label="You are here" />
        </span>
      )}
    </div>
  );
}

function RoomArchitecture({ data }: { data: RoomNodeData }) {
  const { room, width, height, doorways, isCurrent } = data;
  const hatchId = `room-hatch-${room.id}`;
  const outline = { width, height, doorways };
  return (
    <svg
      aria-hidden="true"
      className="pointer-events-none absolute inset-0 overflow-visible"
      width={width}
      height={height}
    >
      <defs>
        <pattern
          id={hatchId}
          width="12"
          height="12"
          patternUnits="userSpaceOnUse"
          patternTransform="rotate(45)"
        >
          <path
            d="M 0 0 V 12"
            stroke="var(--parchment-foreground)"
            strokeOpacity="0.16"
            strokeWidth="3"
          />
        </pattern>
      </defs>
      <rect width={width} height={height} fill={room.isFrontier ? 'var(--muted)' : 'var(--card)'} />
      {room.isFrontier && <rect width={width} height={height} fill={`url(#${hatchId})`} />}
      <path
        d={roomWallPath(outline)}
        fill="none"
        stroke={isCurrent ? 'var(--primary)' : MAP_WALL_COLOR}
        strokeWidth={isCurrent ? 9 : WALL_WIDTH}
        strokeDasharray={room.isFrontier ? '7 5' : undefined}
      />
      <path
        d={roomWallPath({ ...outline, inset: 5 })}
        fill="none"
        stroke={MAP_WALL_COLOR}
        strokeWidth="1"
        strokeOpacity="0.35"
      />
      <path d={doorwayJambPath(outline)} fill="none" stroke={MAP_WALL_COLOR} strokeWidth="2" />
    </svg>
  );
}

function FloorTransitions({ transitions }: { transitions: FloorTransition[] }) {
  if (transitions.length === 0) return null;

  return (
    <div className="flex gap-2">
      {transitions.map((transition) => {
        const arrow = transition.direction === 'up' ? '↑' : '↓';
        const label = `${arrow} ${formatFloor(transition.floorNumber)}`;
        return (
          <span
            key={transition.floorNumber}
            className={cn(
              'text-parchment-foreground flex items-center gap-1 text-xs leading-4',
              transition.isLocked && 'text-destructive',
            )}
            aria-label={`Stairs to ${formatFloor(transition.floorNumber)}${transition.isLocked ? ', locked' : ''}`}
            title={`Stairs to ${formatFloor(transition.floorNumber)}${transition.isLocked ? ' · Locked' : ''}`}
          >
            <svg className="shrink-0" width="24" height="24" viewBox="0 0 28 24" aria-hidden="true">
              <path
                d="M 2 20 H 8 V 16 H 14 V 12 H 20 V 8 H 26 V 20 Z"
                fill="currentColor"
                fillOpacity="0.18"
                stroke="currentColor"
                strokeWidth="1.75"
                strokeLinejoin="round"
              />
            </svg>
            {label}
          </span>
        );
      })}
    </div>
  );
}

function CorridorWallEdge(props: EdgeProps<PassageFlowEdge>) {
  const { path } = getCorridorPath(props);
  return (
    <BaseEdge
      id={props.id}
      path={path}
      style={{
        stroke: MAP_WALL_COLOR,
        strokeWidth: CORRIDOR_WIDTH,
        strokeLinecap: 'butt',
        strokeLinejoin: 'round',
      }}
    />
  );
}

function CorridorFloorEdge(props: EdgeProps<PassageFlowEdge>) {
  const { path, label } = getCorridorPath(props);
  return (
    <>
      <BaseEdge
        id={props.id}
        path={path}
        style={{
          stroke: props.data?.isFrontier ? 'var(--muted)' : 'var(--card)',
          strokeWidth: CORRIDOR_FLOOR_WIDTH,
          strokeLinecap: 'butt',
          strokeLinejoin: 'round',
        }}
      />
      {props.data?.isLocked && (
        <EdgeLabelRenderer>
          <span
            className="nodrag nopan pointer-events-auto absolute z-20 flex size-16 items-center justify-center"
            style={{
              transform: `translate(-50%, -50%) translate(${label.x}px, ${label.y}px)`,
            }}
          >
            <MapLock kind={props.data.lockKind} angle={label.angle} />
          </span>
        </EdgeLabelRenderer>
      )}
    </>
  );
}

function getCorridorPath(props: EdgeProps<PassageFlowEdge>): {
  path: string;
  label: PointResponse & { angle: number };
} {
  if (isUsablePath(props.data?.path)) {
    return { path: roundedCorridorPath(props.data.path), label: corridorGate(props.data.path) };
  }

  if (Math.abs(props.sourceY - props.targetY) < 1) {
    const [path, labelX, labelY] = getStraightPath({
      sourceX: props.sourceX,
      sourceY: props.sourceY,
      targetX: props.targetX,
      targetY: props.targetY,
    });
    return { path, label: { x: labelX, y: labelY, angle: 0 } };
  }

  const [path, labelX, labelY] = getSmoothStepPath({
    sourceX: props.sourceX,
    sourceY: props.sourceY,
    sourcePosition: props.sourcePosition,
    targetX: props.targetX,
    targetY: props.targetY,
    targetPosition: props.targetPosition,
    borderRadius: 18,
    offset: 24,
  });
  return {
    path,
    label: {
      x: labelX,
      y: labelY,
      angle:
        Math.abs(props.targetX - props.sourceX) >= Math.abs(props.targetY - props.sourceY) ? 0 : 90,
    },
  };
}

function MapLegend() {
  return (
    <div className="text-muted-foreground bg-muted/50 flex flex-wrap gap-x-4 gap-y-1.5 border-t px-4 py-3 text-xs">
      <span className="flex items-center gap-1.5">
        <span className="bg-card size-3 border-2" />
        Explored
      </span>
      <span className="flex items-center gap-1.5">
        <span className="bg-muted size-3 border-2 border-dashed" />
        Visible, unexplored
      </span>
      <span className="flex items-center gap-1.5">
        <span className="text-destructive size-4">
          <MapLockSymbol kind="KeyLockedDoor" />
        </span>
        Locked door
      </span>
      <span className="flex items-center gap-1.5">
        <span className="text-destructive size-4">
          <MapLockSymbol kind="Portcullis" />
        </span>
        Portcullis
      </span>
      <span className="flex items-center gap-1.5">
        <GiStairs className="size-3.5" />
        Stairs
      </span>
      <span className="flex items-center gap-1.5">
        <GiChest className="size-4" /> Chest
      </span>
      <span className="flex items-center gap-1.5">
        <GiLever className="size-4" /> Lever
      </span>
      <span className="flex items-center gap-1.5">
        <GiTombstone className="size-4" /> Your unlooted remains
      </span>
    </div>
  );
}

function buildLocalMap(
  rooms: LocalMapRoomResponse[],
  passages: LocalMapPassageResponse[],
  floorTransitions: ReadonlyMap<string, FloorTransition[]>,
  currentRoomId: string,
): { nodes: RoomFlowNode[]; edges: PassageFlowEdge[] } {
  const layouts = layoutRooms(rooms, passages);
  const roomsById = new Map(rooms.map((room) => [room.id, room]));
  const doorways = new Map(rooms.map((room) => [room.id, [] as Doorway[]]));
  const passageLayouts = passages.map((passage) => {
    const handles = passageHandles(passage, roomsById, layouts);
    doorways.get(passage.originRoomId)!.push(handles.sourceDoorway);
    doorways.get(passage.destinationRoomId)!.push(handles.targetDoorway);
    return { passage, handles };
  });
  const nodes = rooms.map<RoomFlowNode>((room) =>
    makeRoomNode(
      room,
      layouts.get(room.id)!,
      uniqueDoorways(doorways.get(room.id)!),
      floorTransitions.get(room.id) ?? [],
      currentRoomId,
    ),
  );
  return { nodes, edges: passageEdges(passageLayouts, roomsById) };
}

function makeRoomNode(
  room: LocalMapRoomResponse,
  layout: RoomLayout,
  doorways: Doorway[],
  floorTransitions: FloorTransition[],
  currentRoomId: string,
): RoomFlowNode {
  return {
    id: room.id,
    type: ROOM_NODE_TYPE,
    position: { x: layout.x, y: layout.y },
    data: {
      room,
      isCurrent: room.id === currentRoomId,
      doorways,
      floorTransitions,
      width: layout.width,
      height: layout.height,
    },
    zIndex: room.id === currentRoomId ? 3 : 2,
  };
}

function passageEdges(
  passageLayouts: Array<{ passage: LocalMapPassageResponse; handles: PassageHandles }>,
  roomsById: ReadonlyMap<string, LocalMapRoomResponse>,
): PassageFlowEdge[] {
  const makeEdges = (
    edgeType: typeof CORRIDOR_WALL_EDGE_TYPE | typeof CORRIDOR_FLOOR_EDGE_TYPE,
    zIndex: number,
  ) =>
    passageLayouts.map<PassageFlowEdge>(({ passage, handles }) => ({
      id: `${passage.id}-${edgeType}`,
      source: passage.originRoomId,
      target: passage.destinationRoomId,
      sourceHandle: `source-${handles.sourceDoorway.side}`,
      targetHandle: `target-${handles.targetDoorway.side}`,
      type: edgeType,
      data: {
        isLocked: passage.isLocked,
        lockKind: passage.lockKind,
        isFrontier:
          roomsById.get(passage.originRoomId)!.isFrontier ||
          roomsById.get(passage.destinationRoomId)!.isFrontier,
        path: isUsablePath(passage.path) ? passage.path.map(scalePoint) : null,
      },
      selectable: false,
      zIndex,
    }));
  return [...makeEdges(CORRIDOR_WALL_EDGE_TYPE, 0), ...makeEdges(CORRIDOR_FLOOR_EDGE_TYPE, 1)];
}

interface PassageHandles {
  sourceDoorway: Doorway;
  targetDoorway: Doorway;
}

function passageHandles(
  passage: LocalMapPassageResponse,
  roomsById: ReadonlyMap<string, LocalMapRoomResponse>,
  layouts: ReadonlyMap<string, RoomLayout>,
): PassageHandles {
  const originRoom = roomsById.get(passage.originRoomId)!;
  const destinationRoom = roomsById.get(passage.destinationRoomId)!;
  if (isUsablePath(passage.path) && originRoom.bounds && destinationRoom.bounds) {
    return {
      sourceDoorway: doorwayAt(originRoom.bounds, passage.path[0]),
      targetDoorway: doorwayAt(destinationRoom.bounds, passage.path.at(-1)!),
    };
  }

  const sides = selectHandles(layouts.get(originRoom.id)!, layouts.get(destinationRoom.id)!);
  return {
    sourceDoorway: { side: sides.source, offset: 0.5 },
    targetDoorway: { side: sides.target, offset: 0.5 },
  };
}

function isUsablePath(path: PointResponse[] | null | undefined): path is PointResponse[] {
  return Boolean(
    path &&
    path.length >= 2 &&
    path.every((point) => point != null && Number.isFinite(point.x) && Number.isFinite(point.y)),
  );
}

function doorwayAt(bounds: RoomBoundsResponse, point: PointResponse): Doorway {
  const distances = [
    {
      side: 'left' as const,
      distance: Math.abs(point.x - bounds.left),
      offset: ratio(point.y, bounds.top, bounds.bottom),
    },
    {
      side: 'right' as const,
      distance: Math.abs(point.x - bounds.right),
      offset: ratio(point.y, bounds.top, bounds.bottom),
    },
    {
      side: 'top' as const,
      distance: Math.abs(point.y - bounds.top),
      offset: ratio(point.x, bounds.left, bounds.right),
    },
    {
      side: 'bottom' as const,
      distance: Math.abs(point.y - bounds.bottom),
      offset: ratio(point.x, bounds.left, bounds.right),
    },
  ];
  return distances.sort((first, second) => first.distance - second.distance)[0];
}

function ratio(value: number, minimum: number, maximum: number): number {
  return Math.max(0.12, Math.min(0.88, (value - minimum) / (maximum - minimum)));
}

function uniqueDoorways(doorways: Doorway[]): Doorway[] {
  return doorways.filter(
    (doorway, index) =>
      doorways.findIndex(
        (candidate) =>
          candidate.side === doorway.side && Math.abs(candidate.offset - doorway.offset) < 0.03,
      ) === index,
  );
}

function layoutRooms(
  rooms: LocalMapRoomResponse[],
  passages: LocalMapPassageResponse[],
): Map<string, RoomLayout> {
  return rooms.length > 0 && rooms.every((room) => room.bounds != null)
    ? layoutBoundedRooms(rooms)
    : layoutRoomsWithDagre(rooms, passages);
}

function layoutBoundedRooms(rooms: LocalMapRoomResponse[]): Map<string, RoomLayout> {
  return new Map(
    rooms.map((room) => {
      const bounds = room.bounds!;
      return [
        room.id,
        {
          x: bounds.left * MAP_SCALE,
          y: bounds.top * MAP_SCALE,
          width: (bounds.right - bounds.left) * MAP_SCALE,
          height: (bounds.bottom - bounds.top) * MAP_SCALE,
        },
      ];
    }),
  );
}

function layoutRoomsWithDagre(
  rooms: LocalMapRoomResponse[],
  passages: LocalMapPassageResponse[],
): Map<string, RoomLayout> {
  const graph = new dagre.graphlib.Graph().setDefaultEdgeLabel(() => ({}));
  graph.setGraph({ rankdir: 'LR', nodesep: 70, ranksep: 110, marginx: 40, marginy: 40 });
  rooms.forEach((room) =>
    graph.setNode(room.id, { width: FALLBACK_ROOM_WIDTH, height: FALLBACK_ROOM_HEIGHT }),
  );
  passages.forEach((passage) => graph.setEdge(passage.originRoomId, passage.destinationRoomId));
  dagre.layout(graph);

  return new Map(
    rooms.map((room) => {
      const node = graph.node(room.id);
      return [
        room.id,
        {
          x: node.x - FALLBACK_ROOM_WIDTH / 2,
          y: node.y - FALLBACK_ROOM_HEIGHT / 2,
          width: FALLBACK_ROOM_WIDTH,
          height: FALLBACK_ROOM_HEIGHT,
        },
      ];
    }),
  );
}

function getFloorTransitions(
  rooms: LocalMapRoomResponse[],
  passages: LocalMapPassageResponse[],
  selectedFloor: number,
): ReadonlyMap<string, FloorTransition[]> {
  const roomsById = new Map(rooms.map((room) => [room.id, room]));
  const transitions = new Map<string, FloorTransition[]>();
  for (const passage of passages) {
    const origin = roomsById.get(passage.originRoomId);
    const destination = roomsById.get(passage.destinationRoomId);
    if (!origin || !destination || origin.floorNumber === destination.floorNumber) continue;
    if (origin.floorNumber === selectedFloor) {
      addTransition(
        transitions,
        origin.id,
        destination.floorNumber,
        destination.floorNumber > selectedFloor ? 'up' : 'down',
        passage.isLocked,
      );
    }
    if (destination.floorNumber === selectedFloor) {
      addTransition(
        transitions,
        destination.id,
        origin.floorNumber,
        origin.floorNumber > selectedFloor ? 'up' : 'down',
        passage.isLocked,
      );
    }
  }
  return transitions;
}

function addTransition(
  transitions: Map<string, FloorTransition[]>,
  roomId: string,
  floorNumber: number,
  direction: FloorTransition['direction'],
  isLocked: boolean,
) {
  const existing = transitions.get(roomId) ?? [];
  const duplicate = existing.find((transition) => transition.floorNumber === floorNumber);
  if (duplicate) duplicate.isLocked ||= isLocked;
  else existing.push({ floorNumber, direction, isLocked });
  transitions.set(roomId, existing);
}

function scalePoint(point: PointResponse): PointResponse {
  return { x: point.x * MAP_SCALE, y: point.y * MAP_SCALE };
}

function selectHandles(
  source: RoomPosition,
  target: RoomPosition,
): { source: HandleSide; target: HandleSide } {
  const horizontalDistance = target.x - source.x;
  const verticalDistance = target.y - source.y;
  if (Math.abs(horizontalDistance) >= Math.abs(verticalDistance)) {
    return horizontalDistance >= 0
      ? { source: 'right', target: 'left' }
      : { source: 'left', target: 'right' };
  }
  return verticalDistance >= 0
    ? { source: 'bottom', target: 'top' }
    : { source: 'top', target: 'bottom' };
}

function formatFloor(floor: number): string {
  return floor === 0 ? 'Ground' : floor > 0 ? `Upper ${floor}` : `Lower ${Math.abs(floor)}`;
}

function formatBuildingType(buildingType: string): string {
  return buildingType.replace(/([a-z])([A-Z])/g, '$1 $2');
}
