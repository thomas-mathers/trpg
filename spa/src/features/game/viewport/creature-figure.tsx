import { useFrame } from '@react-three/fiber';
import { memo, useMemo, useRef } from 'react';
import type { Group } from 'three';

import type {
  CreatureStatusSnapshot,
  CreatureWalkSnapshot,
  PlacementWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { GameClockAnchor } from '@/features/game/game-clock';
import { useCreatureCombatEffects } from '@/features/game/hooks/use-combat-effects';

import { yawToward } from './combat-text';
import { creatureAppearance } from './creature-appearance';
import { resolveGear } from './creature-gear';
import { bystanderRig, viewerRig } from './creature-rigs';
import { EntityLabel } from './entity-label';
import { FloatingCombatText } from './floating-combat-text';
import { headingToYaw } from './layout-math';
import { SkinnedCreature } from './skinned-creature';
import { targetPlate, standingColor } from './target-plate';
import { TargetRing } from './target-ring';
import { useCreaturePose, turnTowards } from './use-creature-pose';
import { useCreaturePosition } from './use-creature-position';

const SEAT_TOP = 0.5;
const WALK_TURN_RATE = 4;
const TARGET_RING_RADIUS = 0.6;

export const CreatureFigure = memo(function CreatureFigure({
  id,
  placement,
  walk,
  clock,
  posture,
  playerId,
  label,
  facing,
  creatureType,
  age,
  equipment,
  reputation,
  target,
  engaged = false,
}: {
  id: string;
  placement: PlacementWire;
  walk?: CreatureWalkSnapshot;
  clock: GameClockAnchor;
  posture?: string;
  playerId: string;
  label?: string;
  facing?: number;
  creatureType: CreatureStatusSnapshot['creatureType'];
  age: number;
  equipment: CreatureStatusSnapshot['equipment'];
  reputation?: number;
  target?: CreatureStatusSnapshot;
  engaged?: boolean;
}) {
  const appearance = useMemo(
    () => creatureAppearance(id, creatureType, age),
    [id, creatureType, age],
  );
  const gear = useMemo(() => resolveGear(equipment), [equipment]);
  const plate = useMemo(() => (target ? targetPlate(target) : undefined), [target]);
  const group = useRef<Group>(null);
  const seated = posture === 'Sitting';
  const isPlayer = id === playerId;
  const base = headingToYaw(placement.angle);
  const rig = useMemo(
    () => (isPlayer ? viewerRig(appearance, gear) : bystanderRig(appearance, gear)),
    [appearance, gear, isPlayer],
  );
  const heading = useCreaturePosition(group, {
    placement,
    walk,
    clock,
    seated,
  });
  const { floaters, strikeRequested } = useCreatureCombatEffects(id);
  useFrame(({ camera }, dt) => {
    if (!group.current) return;
    const pathYaw = heading.current === undefined ? undefined : headingToYaw(heading.current);
    const position = group.current.position;
    const towardPlayer = yawToward(position, camera.position);
    const settled = engaged ? towardPlayer : base;
    const target = seated ? base : (facing ?? pathYaw ?? settled);
    const slow = pathYaw !== undefined;
    group.current.rotation.y = turnTowards(
      group.current.rotation.y,
      target,
      dt,
      slow ? WALK_TURN_RATE : undefined,
    );
  });
  useCreaturePose(group, rig, {
    stance: seated ? 'seated' : 'walking',
    upperYawTarget: seated && facing !== undefined ? facing - base : 0,
    height: appearance.height,
    strikeRequested,
  });
  return (
    <group ref={group} rotation={[0, base, 0]} userData={isPlayer ? {} : { creatureId: id }}>
      <SkinnedCreature
        rig={rig}
        gear={gear}
        height={appearance.height}
        lift={seated ? Math.max(0, SEAT_TOP * (1 - appearance.height)) : 0}
      />
      {!isPlayer && (
        <group position={[0, (seated ? 1.58 : 1.83) * appearance.height, 0]}>
          <EntityLabel
            text={plate?.title ?? label}
            color={standingColor(reputation)}
            plate={plate}
          />
        </group>
      )}
      <FloatingCombatText floaters={floaters} baseHeight={1.9 * appearance.height} />
      {target && (
        <TargetRing
          radius={TARGET_RING_RADIUS * appearance.height}
          color={standingColor(target.reputation)}
        />
      )}
    </group>
  );
});
