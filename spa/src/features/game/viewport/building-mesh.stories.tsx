import { OrbitControls } from '@react-three/drei';
import { Canvas } from '@react-three/fiber';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { useEffect, useMemo } from 'react';

import type { BuildingType, RoadSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { Roads } from './boundary-scene';
import { BuildingMesh } from './building-mesh';
import { createGrassMaterial } from './ground-material';
import { buildingStyle } from './model-styles';
import { Walls } from './viewport-scene';
import { createWebGpuRenderer } from './webgpu-renderer';
import { windowGlow } from './window-glow';

const ROW: { type: BuildingType; width: number; depth: number; floors: number }[] = [
  { type: 'House', width: 8, depth: 6, floors: 2 },
  { type: 'Inn', width: 12, depth: 8, floors: 2 },
  { type: 'Tavern', width: 10, depth: 8, floors: 2 },
  { type: 'Blacksmith', width: 9, depth: 7, floors: 1 },
  { type: 'Castle', width: 12, depth: 12, floors: 1 },
  { type: 'Temple', width: 10, depth: 14, floors: 1 },
];
const GAP = 6;
const ROADS: RoadSnapshot[] = [
  {
    class: 'Avenue',
    width: 4,
    points: [
      { x: -60, y: 14 },
      { x: 60, y: 14 },
    ],
  },
  {
    class: 'Street',
    width: 3,
    points: [
      { x: -60, y: 21 },
      { x: 60, y: 21 },
    ],
  },
  {
    class: 'Lane',
    width: 2,
    points: [
      { x: -60, y: 27 },
      { x: 60, y: 27 },
    ],
  },
];
const WALLS = [{ placement: { x: 0, y: -16, angle: 0 }, footprint: { width: 60, depth: 1 } }];

function Grass() {
  const material = useMemo(() => createGrassMaterial('#6f7658'), []);
  useEffect(() => () => material.dispose(), [material]);
  return (
    <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, -0.01, 0]} material={material}>
      <planeGeometry args={[200, 120]} />
    </mesh>
  );
}

function BuildingRow({ night }: { night: boolean }) {
  useEffect(() => {
    windowGlow.value = night ? 1 : 0;
  }, [night]);
  const total = ROW.reduce((sum, { width }) => sum + width + GAP, 0);
  let cursor = -total / 2;
  return (
    <>
      {ROW.map(({ type, width, depth, floors }) => {
        const x = cursor + width / 2;
        cursor += width + GAP;
        return (
          <group key={type} position={[x, 0, 0]}>
            <BuildingMesh
              id={`story-${type}`}
              type={type}
              footprint={{ width, depth }}
              style={buildingStyle(type, floors)}
            />
          </group>
        );
      })}
    </>
  );
}

function BuildingPreview({ night }: { night: boolean }) {
  return (
    <div style={{ width: '100vw', height: '100vh' }}>
      <Canvas gl={createWebGpuRenderer} camera={{ position: [0, 14, 52], fov: 42 }}>
        <color attach="background" args={[night ? '#0b1020' : '#9bb7d4']} />
        <hemisphereLight args={['#bcd0ea', '#3b3a30', night ? 0.25 : 0.7]} />
        <directionalLight position={[20, 30, 25]} intensity={night ? 0.3 : 1.6} />
        <BuildingRow night={night} />
        <Grass />
        <Roads roads={ROADS} />
        <Walls walls={WALLS} />
        <OrbitControls target={[0, 4, 0]} />
      </Canvas>
    </div>
  );
}

const meta = {
  title: 'Game/Viewport/Buildings',
  component: BuildingPreview,
  parameters: { layout: 'fullscreen' },
} satisfies Meta<typeof BuildingPreview>;
export default meta;
type Story = StoryObj<typeof meta>;
export const Day: Story = { args: { night: false } };
export const Night: Story = { args: { night: true } };
