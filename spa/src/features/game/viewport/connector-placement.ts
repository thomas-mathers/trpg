import type { NearbyExitSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { headingToYaw } from './layout-math';

export function connectorYaw({ placement }: NearbyExitSnapshot): number {
  return headingToYaw(placement.angle) + Math.PI;
}
