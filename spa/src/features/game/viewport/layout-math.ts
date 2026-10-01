import type {
  FootprintWire,
  SceneSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

export type ScenePosition = [x: number, y: number, z: number];

export interface PlanarPoint {
  x: number;
  y: number;
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
const BOUNDS_MARGIN = 0.3;

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

export function buildEntityNames(scene: SceneSnapshot): ReadonlyMap<string, string> {
  const names = new Map<string, string>();
  scene.nearbyCreatures.forEach((creature) => names.set(creature.id, creature.name));
  scene.nearbyBuildings.forEach((building) => names.set(building.id, building.name));
  scene.nearbyProps.forEach((prop) => names.set(prop.id, prop.name));
  scene.exits.forEach((exit) => names.set(exit.connectorId, exit.destination.name));
  return names;
}

export function findPlayerPlacement(scene: SceneSnapshot) {
  return scene.layout.creatures.find((creature) => creature.id === scene.playerStatus.id)
    ?.placement;
}

function clamp(value: number, minimum: number, maximum: number): number {
  if (maximum < minimum) {
    return (minimum + maximum) / 2;
  }
  return Math.min(Math.max(value, minimum), maximum);
}
