import { useFrame } from '@react-three/fiber';
import { useEffect, useMemo, useRef } from 'react';
import { DoubleSide, type MeshStandardMaterial } from 'three';

import type { FootprintWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { furnitureGeometry } from './furniture-geometry';
import { type FurnitureModel, type FurniturePart, furnitureParts } from './furniture-parts';
import { useLampGlow } from './lamp-glow';
import type { BoxStyle } from './model-styles';

const METAL_ROUGHNESS = 0.55;
const METAL_METALNESS = 0.4;
const GLOW_INTENSITY = 1.2;

const WATER_ROUGHNESS = 0.06;
const WATER_SHIMMER_BASE = 0.03;
const WATER_SHIMMER_RANGE = 0.02;
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

function LampMaterial({ color }: Pick<FurniturePart, 'color'>) {
  const material = useRef<MeshStandardMaterial>(null);
  useLampGlow(material);
  return <meshStandardMaterial ref={material} color={color} roughness={1} emissive={color} />;
}

function PartMaterial({
  color,
  opacity,
  finish,
  doubleSided,
}: Pick<FurniturePart, 'color' | 'opacity' | 'finish'> & { doubleSided: boolean }) {
  if (finish === 'water') return <WaterMaterial color={color} opacity={opacity} />;
  if (finish === 'lamp') return <LampMaterial color={color} />;
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

export function FurniturePartMesh({
  shape,
  position,
  size,
  color,
  opacity,
  rotation,
  finish,
  shadows = true,
}: FurniturePart & { shadows?: boolean }) {
  const [first, second, third] = size;
  const geometry = useMemo(
    () => furnitureGeometry({ shape, size: [first, second, third] }),
    [shape, first, second, third],
  );
  useEffect(() => () => geometry.dispose(), [geometry]);
  return (
    <mesh
      position={position}
      rotation={rotation}
      geometry={geometry}
      castShadow={shadows && (opacity === undefined || opacity === 1)}
      receiveShadow
    >
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
        <FurniturePartMesh key={index} {...part} />
      ))}
    </group>
  );
}
