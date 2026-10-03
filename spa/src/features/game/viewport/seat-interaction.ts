import type { NearbyPropSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import type { PlanarPoint } from './layout-math';

export const SEATED_EYE_HEIGHT = 1.25;
export const SEAT_REACH = 1.5;
export type ViewportSeat = NearbyPropSnapshot;

export function buildSeats(props: NearbyPropSnapshot[]): ViewportSeat[] {
  return props.filter(({ type }) => type === 'Seat');
}

export function findSeatInRange(
  position: PlanarPoint,
  seats: ViewportSeat[],
): ViewportSeat | undefined {
  let nearest: ViewportSeat | undefined;
  let distance = SEAT_REACH;
  for (const seat of seats) {
    const gap = Math.hypot(seat.placement.x - position.x, seat.placement.y - position.y);
    if (gap <= distance) {
      nearest = seat;
      distance = gap;
    }
  }
  return nearest;
}
