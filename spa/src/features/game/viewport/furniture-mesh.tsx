import type { FootprintWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { type FurnitureModel, type FurniturePart, furnitureParts } from './furniture-parts';
import type { BoxStyle } from './model-styles';

function PartMesh({ shape, position, size, color, opacity }: FurniturePart) {
  const [first, second, third] = size;
  return (
    <mesh position={position} castShadow={opacity === undefined || opacity === 1} receiveShadow>
      {shape === 'box' && <boxGeometry args={[first, second, third]} />}
      {shape === 'cylinder' && <cylinderGeometry args={[first, second, third, 16]} />}
      {shape === 'sphere' && <sphereGeometry args={[first, 12, 8]} />}
      <meshStandardMaterial
        color={color}
        transparent={opacity !== undefined}
        opacity={opacity ?? 1}
      />
    </mesh>
  );
}

export function FurnitureMesh({
  footprint: { width, depth },
  style: { color, height },
  model,
}: {
  footprint: FootprintWire;
  style: BoxStyle;
  model: FurnitureModel;
}) {
  const parts = furnitureParts(model, { width, depth, height, color });
  return (
    <group position={[0, -height / 2, 0]}>
      {parts.map((part, index) => (
        <PartMesh key={index} {...part} />
      ))}
    </group>
  );
}
