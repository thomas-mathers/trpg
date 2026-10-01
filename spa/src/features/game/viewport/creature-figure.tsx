import { useFrame } from '@react-three/fiber';
import { useRef, useState } from 'react';
import { MathUtils, type Group } from 'three';

import type { PlacementWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { EntityLabel } from './entity-label';
import { headingToYaw, toScenePosition } from './layout-math';
import { SeatedBody } from './seated-body';
import { StandingBody } from './standing-body';

export function CreatureFigure({
  id,
  placement,
  posture,
  playerId,
  label,
  facing,
}: {
  id: string;
  placement: PlacementWire;
  posture?: string;
  playerId: string;
  label?: string;
  facing?: number;
}) {
  const group = useRef<Group>(null);
  const upper = useRef(0);
  const [upperBodyYaw, setUpperBodyYaw] = useState(0);
  const seated = posture === 'Sitting';
  const base = headingToYaw(placement.angle);
  useFrame((_, dt) => {
    if (!group.current) return;
    const target = seated ? base : (facing ?? base);
    group.current.rotation.y = turnTowards(group.current.rotation.y, target, dt);
    const desired = seated && facing !== undefined ? facing - base : 0;
    upper.current = turnTowards(upper.current, desired, dt);
    if (Math.abs(upper.current - upperBodyYaw) > 0.001) setUpperBodyYaw(upper.current);
  });
  return (
    <group
      ref={group}
      position={toScenePosition(placement.x, placement.y)}
      rotation={[0, base, 0]}
      userData={id === playerId ? {} : { creatureId: id }}
    >
      {seated ? (
        <SeatedBody
          color={id === playerId ? '#4f8091' : '#b9503f'}
          perspective={id === playerId ? 'first-person' : 'third-person'}
          upperBodyYaw={upperBodyYaw}
        />
      ) : (
        <StandingBody color="#b9503f" />
      )}
      {id !== playerId && (
        <group position={[0, seated ? 1.85 : 2.1, 0]}>
          <EntityLabel text={label} />
        </group>
      )}
    </group>
  );
}

function turnTowards(current: number, target: number, dt: number) {
  const difference = Math.atan2(Math.sin(target - current), Math.cos(target - current));
  return current + difference * MathUtils.clamp(1 - Math.exp(-10 * dt), 0, 1);
}
