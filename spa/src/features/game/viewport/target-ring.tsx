import { DoubleSide } from 'three';

const RING_LIFT = 0.03;
const RING_THICKNESS = 0.1;
const RING_SEGMENTS = 48;

export function TargetRing({ radius, color }: { radius: number; color: string }) {
  return (
    <mesh
      position={[0, RING_LIFT, 0]}
      rotation={[-Math.PI / 2, 0, 0]}
      raycast={() => undefined}
      renderOrder={1}
    >
      <ringGeometry args={[radius - RING_THICKNESS, radius, RING_SEGMENTS]} />
      <meshBasicMaterial
        color={color}
        side={DoubleSide}
        transparent
        opacity={0.9}
        depthWrite={false}
        toneMapped={false}
      />
    </mesh>
  );
}
