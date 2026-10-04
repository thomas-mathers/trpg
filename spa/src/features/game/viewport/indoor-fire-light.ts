import { PointLight } from 'three/webgpu';

import type {
  FootprintWire,
  NearbyPropSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { WALL_HEIGHT } from './layout-math';

export const isFireSource = ({ model }: NearbyPropSnapshot) =>
  model === 'FurnitureFireplace' || model === 'FurnitureFirePit';

export function createFireLight(
  { model, placement, footprint }: NearbyPropSnapshot,
  { width, depth }: FootprintWire,
) {
  const front = model === 'FurnitureFireplace' ? footprint.depth / 2 + 0.15 : 0;
  const x = placement.x + Math.sin(placement.angle) * front;
  const z = placement.y - Math.cos(placement.angle) * front;
  const light = new PointLight('#ffb56a', 10);
  light.position.set(x, model === 'FurnitureFireplace' ? 0.45 : 0.4, z);
  light.decay = 1;
  light.castShadow = true;
  light.shadow.mapSize.set(512, 512);
  light.shadow.camera.near = 0.1;
  light.shadow.camera.far =
    Math.max(
      ...[0, width].flatMap((edgeX) =>
        [0, depth].map((edgeZ) => Math.hypot(edgeX - x, edgeZ - z, WALL_HEIGHT)),
      ),
    ) + 1;
  light.shadow.camera.updateProjectionMatrix();
  light.shadow.normalBias = 0.02;
  light.shadow.radius = 3;
  return light;
}
