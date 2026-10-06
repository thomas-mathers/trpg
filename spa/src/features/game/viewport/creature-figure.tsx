import { useFrame } from '@react-three/fiber';
import { memo, useMemo, useRef, useState } from 'react';
import { MathUtils, type Group } from 'three';

import type {
  CreatureStatusSnapshot,
  PlacementWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { creatureAppearance } from './creature-appearance';
import { resolveGear } from './creature-gear';
import { EntityLabel } from './entity-label';
import { headingToYaw, toScenePosition } from './layout-math';
import { SeatedBody } from './seated-body';
import { StandingBody } from './standing-body';

export const CreatureFigure = memo(function CreatureFigure({
  id,
  placement,
  posture,
  playerId,
  label,
  facing,
  creatureType,
  age,
  equipment,
}: {
  id: string;
  placement: PlacementWire;
  posture?: string;
  playerId: string;
  label?: string;
  facing?: number;
  creatureType: CreatureStatusSnapshot['creatureType'];
  age: number;
  equipment: CreatureStatusSnapshot['equipment'];
}) {
  const appearance = useMemo(
    () => creatureAppearance(id, creatureType, age),
    [id, creatureType, age],
  );
  const gear = useMemo(() => resolveGear(equipment), [equipment]);
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
          appearance={appearance}
          gear={gear}
          perspective={id === playerId ? 'first-person' : 'third-person'}
          upperBodyYaw={upperBodyYaw}
        />
      ) : (
        <StandingBody appearance={appearance} gear={gear} />
      )}
      {id !== playerId && (
        <group position={[0, (seated ? 1.85 : 2.1) * appearance.height, 0]}>
          <EntityLabel text={label} />
        </group>
      )}
    </group>
  );
});

function turnTowards(current: number, target: number, dt: number) {
  const difference = Math.atan2(Math.sin(target - current), Math.cos(target - current));
  return current + difference * MathUtils.clamp(1 - Math.exp(-10 * dt), 0, 1);
}
