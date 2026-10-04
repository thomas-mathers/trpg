import { BackSide } from 'three';

import type { StairDirection } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { DOOR_HEIGHT, STAIR_DEPTH, STAIR_WIDTH, WALL_HEIGHT } from './layout-math';

const STEP_COUNT = 10;
const STEP_RISE = (DOOR_HEIGHT - 0.24) / STEP_COUNT;
const CLIMB_RISE = WALL_HEIGHT / STEP_COUNT;
const STEP_RUN = STAIR_DEPTH / STEP_COUNT;
const WELL_DEPTH = STEP_COUNT * STEP_RISE;
const SHAFT_HEIGHT = 1.5;
const STAIR_COLOR = '#8f8576';
const CURB_COLOR = '#65503a';

function RisingSteps() {
  return (
    <>
      <mesh
        castShadow
        receiveShadow
        position={[0, WALL_HEIGHT + SHAFT_HEIGHT / 2, STAIR_DEPTH / 2]}
      >
        <boxGeometry args={[STAIR_WIDTH, SHAFT_HEIGHT, STAIR_DEPTH]} />
        <meshStandardMaterial color="#14110e" roughness={1} side={BackSide} />
      </mesh>
      {Array.from({ length: STEP_COUNT }, (_, index) => {
        const height = (index + 1) * CLIMB_RISE;
        return (
          <mesh
            castShadow
            receiveShadow
            key={index}
            position={[0, height / 2, STAIR_DEPTH - (index + 0.5) * STEP_RUN]}
          >
            <boxGeometry args={[STAIR_WIDTH, height, STEP_RUN]} />
            <meshStandardMaterial color={STAIR_COLOR} roughness={1} />
          </mesh>
        );
      })}
    </>
  );
}

function DescendingSteps() {
  return (
    <>
      <mesh castShadow receiveShadow position={[0, -WELL_DEPTH / 2, STAIR_DEPTH / 2]}>
        <boxGeometry args={[STAIR_WIDTH, WELL_DEPTH, STAIR_DEPTH]} />
        <meshStandardMaterial color="#14110e" roughness={1} side={BackSide} />
      </mesh>
      {Array.from({ length: STEP_COUNT - 1 }, (_, index) => {
        const top = -(index + 1) * STEP_RISE;
        const height = WELL_DEPTH + top;
        return (
          <mesh
            castShadow
            receiveShadow
            key={index}
            position={[0, top - height / 2, STAIR_DEPTH - (index + 0.5) * STEP_RUN]}
          >
            <boxGeometry args={[STAIR_WIDTH - 0.02, height, STEP_RUN]} />
            <meshStandardMaterial color={STAIR_COLOR} roughness={1} />
          </mesh>
        );
      })}
    </>
  );
}

function Curbs() {
  return (
    <>
      {[-1, 1].map((side) => (
        <mesh
          castShadow
          receiveShadow
          key={side}
          position={[side * (STAIR_WIDTH / 2 + 0.04), 0.06, STAIR_DEPTH / 2]}
        >
          <boxGeometry args={[0.08, 0.12, STAIR_DEPTH]} />
          <meshStandardMaterial color={CURB_COLOR} roughness={0.9} />
        </mesh>
      ))}
    </>
  );
}

export function StairConnector({ direction }: { direction: StairDirection }) {
  return (
    <group>
      {direction === 'Up' ? <RisingSteps /> : <DescendingSteps />}
      <Curbs />
    </group>
  );
}
