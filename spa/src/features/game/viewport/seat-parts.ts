import type { PropModel } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import type { FurniturePart, FurnitureSize } from './furniture-parts';

export function seatParts(
  model: PropModel,
  { width, depth, height, color }: FurnitureSize,
): FurniturePart[] {
  const hasBack = model === 'SeatChair' || model === 'SeatPew' || model === 'SeatThrone';
  return [
    { shape: 'box', position: [0, 0.45, 0], size: [width, 0.1, depth], color },
    ...[-1, 1].flatMap((x) =>
      [-1, 1].map(
        (z): FurniturePart => ({
          shape: 'box',
          position: [x * width * 0.36, 0.2, z * depth * 0.36],
          size: [0.08, 0.4, 0.08],
          color,
        }),
      ),
    ),
    ...(hasBack
      ? [
          {
            shape: 'box' as const,
            position: [0, (height + 0.5) / 2, depth / 2 - 0.04] as [number, number, number],
            size: [width, height - 0.5, 0.08] as [number, number, number],
            color,
          },
        ]
      : []),
  ];
}
