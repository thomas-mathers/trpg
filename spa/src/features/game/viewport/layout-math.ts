import type {
  BuildingLayoutWire,
  ConnectorLayoutWire,
  FootprintWire,
  PlacementWire,
  PropLayoutWire,
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
export const WALK_SPEED = 3;
export const BASE_MOVEMENT_SPEED = 50;

export const walkSpeedFor = (movementSpeed: number) =>
  WALK_SPEED * (movementSpeed / BASE_MOVEMENT_SPEED);
export const PLAYER_RADIUS = 0.35;
export const INTERACT_RANGE = 2.5;
const BOUNDS_MARGIN = 0.3;
const WALKABLE_HEIGHT = 0.3;
const RESOLVE_PASSES = 2;
const WALL_THICKNESS = 0.2;
export const WALL_HEIGHT = 3;
const DOOR_WIDTH = 1.5;
const DOOR_SNAP = 0.6;
const MIN_WALL_SPAN = 0.1;
const DOOR_BUILDING_REACH = 1.5;

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
  props: PropLayoutWire[],
  buildings: BuildingLayoutWire[],
): Obstacle[] {
  const solidProps = props.filter(({ model }) => PROP_STYLES[model].height >= WALKABLE_HEIGHT);
  return [...solidProps, ...buildings];
}

export function buildWalls(size: FootprintWire, connectors: ConnectorLayoutWire[]): Obstacle[] {
  const { width, depth } = size;
  const sides = [
    { horizontal: true, line: 0, length: width, doors: doorsOnLine(connectors, 'y', 0) },
    { horizontal: true, line: depth, length: width, doors: doorsOnLine(connectors, 'y', depth) },
    { horizontal: false, line: 0, length: depth, doors: doorsOnLine(connectors, 'x', 0) },
    { horizontal: false, line: width, length: depth, doors: doorsOnLine(connectors, 'x', width) },
  ];

  return sides.flatMap(({ horizontal, line, length, doors }) =>
    wallSpans(length, doors).map(([start, end]) => {
      const middle = (start + end) / 2;
      const span = end - start;
      return horizontal
        ? {
            placement: { x: middle, y: line, angle: 0 },
            footprint: { width: span, depth: WALL_THICKNESS },
          }
        : {
            placement: { x: line, y: middle, angle: 0 },
            footprint: { width: WALL_THICKNESS, depth: span },
          };
    }),
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
  connectors: ConnectorLayoutWire[],
  range = INTERACT_RANGE,
): ConnectorLayoutWire | undefined {
  let nearest: ConnectorLayoutWire | undefined;
  let nearestDistance = range;
  for (const connector of connectors) {
    const distance = Math.hypot(connector.exitX - position.x, connector.exitY - position.y);
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
  nameBuildingDoors(scene, names);
  return names;
}

// Outdoors the server omits a building's front door from the exits, so its name comes from the building the door sits on.
function nameBuildingDoors(scene: SceneSnapshot, names: Map<string, string>) {
  for (const connector of scene.layout.connectors) {
    if (names.has(connector.connectorId)) {
      continue;
    }
    const building = nearestBuilding(connector, scene.layout.buildings);
    const name = building && names.get(building.id);
    if (name) {
      names.set(connector.connectorId, name);
    }
  }
}

function nearestBuilding(
  connector: ConnectorLayoutWire,
  buildings: BuildingLayoutWire[],
): BuildingLayoutWire | undefined {
  const point = { x: connector.exitX, y: connector.exitY };
  let nearest: BuildingLayoutWire | undefined;
  let nearestDistance = DOOR_BUILDING_REACH;
  for (const building of buildings) {
    const distance = distanceToObstacle(point, building);
    if (distance <= nearestDistance) {
      nearest = building;
      nearestDistance = distance;
    }
  }
  return nearest;
}

function distanceToObstacle(point: PlanarPoint, { placement, footprint }: Obstacle): number {
  const cos = Math.cos(placement.angle);
  const sin = Math.sin(placement.angle);
  const dx = point.x - placement.x;
  const dy = point.y - placement.y;
  const localX = dx * cos + dy * sin;
  const localY = -dx * sin + dy * cos;
  const gapX = Math.max(Math.abs(localX) - footprint.width / 2, 0);
  const gapY = Math.max(Math.abs(localY) - footprint.depth / 2, 0);
  return Math.hypot(gapX, gapY);
}

export function findPlayerPlacement(scene: SceneSnapshot) {
  return scene.layout.creatures.find((creature) => creature.id === scene.playerStatus.id)
    ?.placement;
}

function doorsOnLine(connectors: ConnectorLayoutWire[], axis: 'x' | 'y', line: number): number[] {
  return connectors
    .filter((connector) => {
      const onAxis = axis === 'x' ? connector.exitX : connector.exitY;
      return Math.abs(onAxis - line) <= DOOR_SNAP;
    })
    .map((connector) => (axis === 'x' ? connector.exitY : connector.exitX));
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
