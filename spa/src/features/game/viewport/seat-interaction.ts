import type {
  NearbyPropSnapshot,
  PropLayoutWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import type { PlanarPoint } from './layout-math';

export const SEATED_EYE_HEIGHT = 1.25;
export const SEAT_REACH = 1.5;
export type ViewportSeat = PropLayoutWire & {
  name: string;
  isOccupied: boolean;
  isOccupiedByPlayer: boolean;
};

export function buildSeats(
  props: PropLayoutWire[],
  nearbyProps: NearbyPropSnapshot[],
): ViewportSeat[] {
  return props.flatMap((prop) => {
    const seat = nearbyProps.find(({ id, type }) => id === prop.id && type === 'Seat');
    return seat
      ? [
          {
            ...prop,
            name: seat.name,
            isOccupied: seat.isOccupied,
            isOccupiedByPlayer: seat.isOccupiedByPlayer,
          },
        ]
      : [];
  });
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
