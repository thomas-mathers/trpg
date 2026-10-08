import type {
  BoundarySegmentKind,
  FootprintWire,
  LocationBoundarySnapshot,
  NearbyBuildingSnapshot,
  NearbyExitSnapshot,
  NearbyPropSnapshot,
  PlacementWire,
  PropModel,
  SceneSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { PROP_STYLES } from './model-styles';

export type ScenePosition = [x: number, y: number, z: number];

export interface PlanarPoint {
  x: number;
  y: number;
}

export interface Obstacle {
  placement: PlacementWire;
  footprint: FootprintWire;
}

export interface MovementInput {
  heading: number;
  forward: number;
  strafe: number;
  speed: number;
  deltaSeconds: number;
}

export const EYE_HEIGHT = 1.7;
export const WALK_SPEED = 1.4;
export const BASE_MOVEMENT_SPEED = 50;
export const walkSpeedFor = (movementSpeed: number) =>
  WALK_SPEED * (movementSpeed / BASE_MOVEMENT_SPEED);
export const PLAYER_RADIUS = 0.35;
export const INTERACT_RANGE = 2.5;
const BOUNDS_MARGIN = 0.3;
const WALKABLE_HEIGHT = 0.3;
const OVERHEAD_MODELS = new Set<PropModel>(['FurnitureChandelier', 'FurnitureWallSconce']);
const RESOLVE_PASSES = 2;
const WALL_THICKNESS = 0.2;
export const WALL_HEIGHT = 3;
export const DOOR_HEIGHT = 2.64;
const DOOR_WIDTH = 1.5;
const DOOR_SNAP = 0.6;
const MIN_WALL_SPAN = 0.1;
export const STAIR_WIDTH = 1.2;
export const STAIR_DEPTH = 2;

export function toScenePosition(x: number, y: number, height = 0): ScenePosition {
  return [x, height, y];
}

export function headingToYaw(heading: number): number {
  return -heading;
}

export function yawToHeading(yaw: number): number {
  return -yaw;
}

export function computeMovement({
  heading,
  forward,
  strafe,
  speed,
  deltaSeconds,
}: MovementInput): PlanarPoint {
  const distance = speed * deltaSeconds;
  const sin = Math.sin(heading);
  const cos = Math.cos(heading);

  return {
    x: (forward * sin + strafe * cos) * distance,
    y: (-forward * cos + strafe * sin) * distance,
  };
}

export function clampToBounds(point: PlanarPoint, size: FootprintWire): PlanarPoint {
  return {
    x: clamp(point.x, BOUNDS_MARGIN, size.width - BOUNDS_MARGIN),
    y: clamp(point.y, BOUNDS_MARGIN, size.depth - BOUNDS_MARGIN),
  };
}

export function buildObstacles(
  props: NearbyPropSnapshot[],
  buildings: NearbyBuildingSnapshot[],
  connectors: NearbyExitSnapshot[],
): Obstacle[] {
  const solidProps = props.filter(
    ({ model }) => !OVERHEAD_MODELS.has(model) && PROP_STYLES[model].height >= WALKABLE_HEIGHT,
  );
  return [...solidProps, ...buildings, ...connectors.filter(isStairs).map(stairObstacle)];
}

export function isStairs({ stairs }: NearbyExitSnapshot): boolean {
  return stairs !== undefined;
}

export function stairObstacle({ placement }: NearbyExitSnapshot): Obstacle {
  const { x, y } = pointAhead(placement, STAIR_DEPTH / 2);
  return {
    placement: { x, y, angle: placement.angle },
    footprint: { width: STAIR_WIDTH, depth: STAIR_DEPTH },
  };
}

export function boundaryWalls(boundary: LocationBoundarySnapshot | undefined): Obstacle[] {
  return boundarySegments(boundary, 'Wall');
}

export function boundaryTowers(boundary: LocationBoundarySnapshot | undefined): Obstacle[] {
  return boundarySegments(boundary, 'Tower');
}

export function isRoomScene({ roomName }: SceneSnapshot): boolean {
  return Boolean(roomName);
}

export function buildWalls(size: FootprintWire, connectors: NearbyExitSnapshot[]): Obstacle[] {
  return wallSides(size, connectors).flatMap(({ horizontal, line, length, doors }) =>
    wallSpans(length, doors).map(([start, end]) =>
      wallSegment(horizontal, line, (start + end) / 2, end - start),
    ),
  );
}

export function buildDoorHeaders(
  size: FootprintWire,
  connectors: NearbyExitSnapshot[],
): Obstacle[] {
  return wallSides(size, connectors).flatMap(({ horizontal, line, doors }) =>
    doors.map((centre) => wallSegment(horizontal, line, centre, DOOR_WIDTH)),
  );
}

export function pushOutOfObstacles(
  point: PlanarPoint,
  obstacles: Obstacle[],
  radius = PLAYER_RADIUS,
): PlanarPoint {
  let resolved = point;
  for (let pass = 0; pass < RESOLVE_PASSES; pass++) {
    for (const obstacle of obstacles) {
      resolved = pushOutOfObstacle(resolved, obstacle, radius);
    }
  }
  return resolved;
}

export function findConnectorInRange(
  position: PlanarPoint,
  connectors: NearbyExitSnapshot[],
  range = INTERACT_RANGE,
): NearbyExitSnapshot | undefined {
  let nearest: NearbyExitSnapshot | undefined;
  let nearestDistance = range;
  for (const connector of connectors) {
    const reach = interactPoint(connector);
    const distance = Math.hypot(reach.x - position.x, reach.y - position.y);
    if (distance <= nearestDistance) {
      nearest = connector;
      nearestDistance = distance;
    }
  }
  return nearest;
}

export function buildEntityNames(scene: SceneSnapshot): ReadonlyMap<string, string> {
  const names = new Map<string, string>();
  scene.nearbyCreatures.forEach((creature) => names.set(creature.id, creature.name));
  scene.nearbyBuildings.forEach((building) => names.set(building.id, building.name));
  scene.nearbyProps.forEach((prop) => names.set(prop.id, prop.name));
  scene.exits.forEach((exit) => names.set(exit.connectorId, exit.destination.name));
  return names;
}

export function findPlayerPlacement(scene: SceneSnapshot) {
  return scene.playerStatus.placement;
}

export function stairCorners({ placement }: NearbyExitSnapshot): PlanarPoint[] {
  const { x, y } = pointAhead(placement, STAIR_DEPTH / 2);
  const acrossX = Math.cos(placement.angle) * (STAIR_WIDTH / 2);
  const acrossY = Math.sin(placement.angle) * (STAIR_WIDTH / 2);
  const alongX = Math.sin(placement.angle) * (STAIR_DEPTH / 2);
  const alongY = -Math.cos(placement.angle) * (STAIR_DEPTH / 2);
  return [
    { x: x - acrossX - alongX, y: y - acrossY - alongY },
    { x: x + acrossX - alongX, y: y + acrossY - alongY },
    { x: x + acrossX + alongX, y: y + acrossY + alongY },
    { x: x - acrossX + alongX, y: y - acrossY + alongY },
  ];
}

function interactPoint(connector: NearbyExitSnapshot): PlanarPoint {
  return isStairs(connector) ? pointAhead(connector.placement, STAIR_DEPTH) : connector.placement;
}

function pointAhead({ x, y, angle }: PlacementWire, distance: number): PlanarPoint {
  return { x: x + Math.sin(angle) * distance, y: y - Math.cos(angle) * distance };
}

function boundarySegments(
  boundary: LocationBoundarySnapshot | undefined,
  kind: BoundarySegmentKind,
): Obstacle[] {
  return (boundary?.segments ?? [])
    .filter((segment) => segment.kind === kind)
    .map(({ placement, footprint }) => ({ placement, footprint }));
}

function wallSides(size: FootprintWire, exits: NearbyExitSnapshot[]) {
  const { width, depth } = size;
  const connectors = exits.filter((exit) => !isStairs(exit));
  return [
    { horizontal: true, line: 0, length: width, doors: doorsOnLine(connectors, 'y', 0) },
    { horizontal: true, line: depth, length: width, doors: doorsOnLine(connectors, 'y', depth) },
    { horizontal: false, line: 0, length: depth, doors: doorsOnLine(connectors, 'x', 0) },
    { horizontal: false, line: width, length: depth, doors: doorsOnLine(connectors, 'x', width) },
  ];
}

function wallSegment(horizontal: boolean, line: number, middle: number, span: number): Obstacle {
  return horizontal
    ? {
        placement: { x: middle, y: line, angle: 0 },
        footprint: { width: span, depth: WALL_THICKNESS },
      }
    : {
        placement: { x: line, y: middle, angle: 0 },
        footprint: { width: WALL_THICKNESS, depth: span },
      };
}

function doorsOnLine(connectors: NearbyExitSnapshot[], axis: 'x' | 'y', line: number): number[] {
  return connectors
    .filter((connector) => {
      const onAxis = connector.placement[axis];
      return Math.abs(onAxis - line) <= DOOR_SNAP;
    })
    .map((connector) => connector.placement[axis === 'x' ? 'y' : 'x']);
}

function wallSpans(length: number, doorCentres: number[]): [start: number, end: number][] {
  const spans: [number, number][] = [];
  let cursor = 0;
  for (const centre of [...doorCentres].sort((a, b) => a - b)) {
    spans.push([cursor, centre - DOOR_WIDTH / 2]);
    cursor = Math.max(cursor, centre + DOOR_WIDTH / 2);
  }
  spans.push([cursor, length]);
  return spans.filter(([start, end]) => end - start >= MIN_WALL_SPAN);
}

function pushOutOfObstacle(
  point: PlanarPoint,
  { placement, footprint }: Obstacle,
  radius: number,
): PlanarPoint {
  const cos = Math.cos(placement.angle);
  const sin = Math.sin(placement.angle);
  const dx = point.x - placement.x;
  const dy = point.y - placement.y;
  const local = { x: dx * cos + dy * sin, y: -dx * sin + dy * cos };
  const pushed = pushOutOfRect(local, footprint.width / 2, footprint.depth / 2, radius);

  if (pushed === local) {
    return point;
  }
  return {
    x: placement.x + pushed.x * cos - pushed.y * sin,
    y: placement.y + pushed.x * sin + pushed.y * cos,
  };
}

function pushOutOfRect(
  point: PlanarPoint,
  halfWidth: number,
  halfDepth: number,
  radius: number,
): PlanarPoint {
  const gapX = point.x - clamp(point.x, -halfWidth, halfWidth);
  const gapY = point.y - clamp(point.y, -halfDepth, halfDepth);
  const distance = Math.hypot(gapX, gapY);

  if (distance >= radius) {
    return point;
  }
  if (distance > 0) {
    const scale = radius / distance;
    return {
      x: point.x - gapX + gapX * scale,
      y: point.y - gapY + gapY * scale,
    };
  }

  const toRight = halfWidth - point.x;
  const toLeft = point.x + halfWidth;
  const toBottom = halfDepth - point.y;
  const toTop = point.y + halfDepth;
  const nearest = Math.min(toRight, toLeft, toBottom, toTop);

  if (nearest === toRight) {
    return { x: halfWidth + radius, y: point.y };
  }
  if (nearest === toLeft) {
    return { x: -halfWidth - radius, y: point.y };
  }
  if (nearest === toBottom) {
    return { x: point.x, y: halfDepth + radius };
  }
  return { x: point.x, y: -halfDepth - radius };
}

function clamp(value: number, minimum: number, maximum: number): number {
  if (maximum < minimum) {
    return (minimum + maximum) / 2;
  }
  return Math.min(Math.max(value, minimum), maximum);
}
