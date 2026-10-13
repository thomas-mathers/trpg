import type { FootprintWire, PropModel } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { FurniturePartMesh } from './furniture-mesh';
import type { BoxStyle } from './model-styles';
import { seatParts } from './seat-parts';

export function SeatMesh({
  footprint: { width, depth },
  style: { color, height },
  model,
}: {
  footprint: FootprintWire;
  style: BoxStyle;
  model: PropModel;
}) {
  const parts = seatParts(model, { width, depth, height, color });
  return (
    <group position={[0, -height / 2, 0]}>
      {parts.map((part, index) => (
        <FurniturePartMesh key={index} {...part} />
      ))}
    </group>
  );
}
