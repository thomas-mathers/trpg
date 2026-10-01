import type {
  BuildingLayoutWire,
  ConnectorLayoutWire,
  FootprintWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { headingToYaw } from './layout-math';

export function connectorYaw(
  { exitX: x, exitY: y }: ConnectorLayoutWire,
  size: FootprintWire,
  buildings: BuildingLayoutWire[],
): number {
  const edges = [
    { distance: Math.abs(x), yaw: Math.PI / 2 },
    { distance: Math.abs(x - size.width), yaw: -Math.PI / 2 },
    { distance: Math.abs(y), yaw: 0 },
    { distance: Math.abs(y - size.depth), yaw: Math.PI },
  ];
  for (const building of buildings) {
    edges.push(...buildingEdges(x, y, building));
  }
  return edges.reduce((closest, edge) => (edge.distance < closest.distance ? edge : closest)).yaw;
}

function buildingEdges(x: number, y: number, { placement, footprint }: BuildingLayoutWire) {
  const cos = Math.cos(placement.angle);
  const sin = Math.sin(placement.angle);
  const dx = x - placement.x;
  const dy = y - placement.y;
  const localX = dx * cos + dy * sin;
  const localY = -dx * sin + dy * cos;
  const halfWidth = footprint.width / 2;
  const halfDepth = footprint.depth / 2;
  const yaw = headingToYaw(placement.angle);
  const gapX = Math.max(Math.abs(localX) - halfWidth, 0);
  const gapY = Math.max(Math.abs(localY) - halfDepth, 0);
  return [
    { distance: Math.hypot(localX - halfWidth, gapY), yaw: yaw + Math.PI / 2 },
    { distance: Math.hypot(localX + halfWidth, gapY), yaw: yaw - Math.PI / 2 },
    { distance: Math.hypot(localY - halfDepth, gapX), yaw },
    { distance: Math.hypot(localY + halfDepth, gapX), yaw: yaw + Math.PI },
  ];
}
