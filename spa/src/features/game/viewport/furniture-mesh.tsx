import { useFrame } from '@react-three/fiber';
import { useMemo, useRef } from 'react';
import { DoubleSide, type MeshStandardMaterial, Vector2 } from 'three';

import type { FootprintWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import {
  BASIN_FLOOR_RATIO,
  type FurnitureModel,
  type FurniturePart,
  furnitureParts,
} from './furniture-parts';
import type { BoxStyle } from './model-styles';

const METAL_ROUGHNESS = 0.55;
const METAL_METALNESS = 0.4;
const GLOW_INTENSITY = 1.2;

const WATER_ROUGHNESS = 0.06;
const WATER_SHIMMER_BASE = 0.16;
const WATER_SHIMMER_RANGE = 0.1;
const WATER_SHIMMER_SPEED = 1.6;

function WaterMaterial({ color, opacity }: Pick<FurniturePart, 'color' | 'opacity'>) {
  const material = useRef<MeshStandardMaterial>(null);
  const phase = useMemo(() => Math.random() * Math.PI * 2, []);
  useFrame(({ clock }) => {
    if (!material.current) return;
    material.current.emissiveIntensity =
      WATER_SHIMMER_BASE +
      WATER_SHIMMER_RANGE * Math.sin(clock.elapsedTime * WATER_SHIMMER_SPEED + phase);
  });
  return (
    <meshStandardMaterial
      ref={material}
      color={color}
      transparent
      depthWrite={false}
      opacity={opacity ?? 1}
      metalness={0.1}
      roughness={WATER_ROUGHNESS}
      emissive={color}
      emissiveIntensity={WATER_SHIMMER_BASE}
    />
  );
}

function PartMaterial({
  color,
  opacity,
  finish,
  doubleSided,
}: Pick<FurniturePart, 'color' | 'opacity' | 'finish'> & { doubleSided: boolean }) {
  if (finish === 'water') return <WaterMaterial color={color} opacity={opacity} />;
  return (
    <meshStandardMaterial
      color={color}
      transparent={opacity !== undefined}
      side={doubleSided ? DoubleSide : undefined}
      opacity={opacity ?? 1}
      metalness={finish === 'metal' ? METAL_METALNESS : 0}
      roughness={finish === 'metal' ? METAL_ROUGHNESS : 1}
      emissive={finish === 'glow' ? color : '#000000'}
      emissiveIntensity={finish === 'glow' ? GLOW_INTENSITY : 0}
    />
  );
}

function BasinGeometry({ size: [outerRadius, innerRadius, height] }: Pick<FurniturePart, 'size'>) {
  const profile = useMemo(() => {
    const half = height / 2;
    const floor = -half + height * BASIN_FLOOR_RATIO;
    return [
      new Vector2(0, -half),
      new Vector2(outerRadius, -half),
      new Vector2(outerRadius, half),
      new Vector2(innerRadius, half),
      new Vector2(innerRadius, floor),
      new Vector2(0, floor),
    ];
  }, [outerRadius, innerRadius, height]);
  return <latheGeometry args={[profile, 16]} />;
}

function PartMesh({ shape, position, size, color, opacity, rotation, finish }: FurniturePart) {
  const [first, second, third] = size;
  return (
    <mesh
      position={position}
      rotation={rotation}
      castShadow={opacity === undefined || opacity === 1}
      receiveShadow
    >
      {shape === 'box' && <boxGeometry args={[first, second, third]} />}
      {shape === 'cylinder' && <cylinderGeometry args={[first, second, third, 16]} />}
      {shape === 'sphere' && <sphereGeometry args={[first, 12, 8]} />}
      {shape === 'basin' && <BasinGeometry size={size} />}
      <PartMaterial
        color={color}
        opacity={opacity}
        finish={finish}
        doubleSided={shape === 'basin'}
      />
    </mesh>
  );
}

export function FurnitureMesh({
  footprint: { width, depth },
  style: { color, height },
  model,
  id,
}: {
  id?: string;
  footprint: FootprintWire;
  style: BoxStyle;
  model: FurnitureModel;
}) {
  const parts = furnitureParts(model, { width, depth, height, color, id });
  return (
    <group position={[0, -height / 2, 0]}>
      {parts.map((part, index) => (
        <PartMesh key={index} {...part} />
      ))}
    </group>
  );
}
