import type { FootprintWire, PropModel } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import type { BoxStyle } from './model-styles';

export function SeatMesh({
  footprint: { width, depth },
  style: { color, height },
  model,
}: {
  footprint: FootprintWire;
  style: BoxStyle;
  model: PropModel;
}) {
  const hasBack = model === 'SeatChair' || model === 'SeatPew' || model === 'SeatThrone';
  return (
    <group position={[0, -height / 2, 0]}>
      <mesh position={[0, 0.45, 0]}>
        <boxGeometry args={[width, 0.1, depth]} />
        <meshStandardMaterial color={color} />
      </mesh>
      {[-1, 1].flatMap((x) =>
        [-1, 1].map((z) => (
          <mesh key={`${x}:${z}`} position={[x * width * 0.36, 0.2, z * depth * 0.36]}>
            <boxGeometry args={[0.08, 0.4, 0.08]} />
            <meshStandardMaterial color={color} />
          </mesh>
        )),
      )}
      {hasBack && (
        <mesh position={[0, (height + 0.5) / 2, depth / 2 - 0.04]}>
          <boxGeometry args={[width, height - 0.5, 0.08]} />
          <meshStandardMaterial color={color} />
        </mesh>
      )}
    </group>
  );
}
