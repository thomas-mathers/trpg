import { useFrame, useThree } from '@react-three/fiber';
import { useLayoutEffect, useMemo, useRef } from 'react';
import type { Group, Mesh } from 'three';

import type { CreatureStatusSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { creatureAppearance } from './creature-appearance';
import { resolveGear } from './creature-gear';
import { JOINTS } from './creature-rig';
import { viewerRig } from './creature-rigs';
import { EYE_HEIGHT } from './layout-math';
import { SkinnedCreature } from './skinned-creature';
import { useCreaturePose } from './use-creature-pose';

const EYES_AHEAD_OF_HEAD = 0.12;
const EYES_ABOVE_HEAD_CENTER = 0.07;
const BODY_HEIGHT = (EYE_HEIGHT - EYES_ABOVE_HEAD_CENTER) / JOINTS.head[1];

function ignoreRaycasts(root: Group) {
  root.traverse((object) => {
    (object as Mesh).raycast = () => {};
  });
}

export function PlayerBody({
  id,
  creatureType,
  age,
  equipment,
}: Pick<CreatureStatusSnapshot, 'id' | 'creatureType' | 'age' | 'equipment'>) {
  const camera = useThree((state) => state.camera);
  const feet = useRef<Group>(null);
  const body = useRef<Group>(null);
  const appearance = useMemo(
    () => creatureAppearance(id, creatureType, age),
    [id, creatureType, age],
  );
  const gear = useMemo(() => resolveGear(equipment), [equipment]);
  const rig = useMemo(() => viewerRig(appearance, gear), [appearance, gear]);

  useLayoutEffect(() => {
    if (body.current) ignoreRaycasts(body.current);
  }, [rig]);

  useFrame(() => {
    if (!feet.current || !body.current) return;
    const yaw = camera.rotation.y;
    feet.current.position.set(camera.position.x, 0, camera.position.z);
    body.current.rotation.y = yaw;
    body.current.position.set(
      Math.sin(yaw) * EYES_AHEAD_OF_HEAD,
      0,
      Math.cos(yaw) * EYES_AHEAD_OF_HEAD,
    );
  });
  useCreaturePose(feet, rig, {
    stance: 'looking',
    upperYawTarget: 0,
    height: BODY_HEIGHT,
    shielded: gear.shields.includes('left'),
  });

  return (
    <group ref={feet}>
      <group ref={body}>
        <SkinnedCreature rig={rig} gear={gear} height={BODY_HEIGHT} lift={0} />
      </group>
    </group>
  );
}
