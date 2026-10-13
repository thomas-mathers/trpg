import { useFrame } from '@react-three/fiber';
import { useRef, type RefObject } from 'react';
import { MathUtils, Vector3, type Group } from 'three';

import { advanceGait, type Gait } from './creature-gait';
import { applyPose, type Stance } from './creature-pose';
import type { CreatureRig } from './creature-rig';

export function turnTowards(current: number, target: number, dt: number) {
  const difference = Math.atan2(Math.sin(target - current), Math.cos(target - current));
  return current + difference * MathUtils.clamp(1 - Math.exp(-10 * dt), 0, 1);
}

export function useCreaturePose(
  group: RefObject<Group | null>,
  rig: CreatureRig,
  {
    stance,
    upperYawTarget,
    height,
    shielded = false,
  }: { stance: Stance; upperYawTarget: number; height: number; shielded?: boolean },
) {
  const gait = useRef<Gait>({ phase: 0, amount: 0 });
  const upperYaw = useRef(0);
  const lastPosition = useRef<Vector3 | null>(null);

  useFrame(({ clock }, dt) => {
    if (!group.current) return;
    const { position } = group.current;
    const last = (lastPosition.current ??= position.clone());
    advanceGait(gait.current, Math.hypot(position.x - last.x, position.z - last.z), dt, height);
    last.copy(position);
    upperYaw.current = turnTowards(upperYaw.current, upperYawTarget, dt);
    applyPose(rig, {
      stance,
      gaitPhase: gait.current.phase,
      gaitAmount: gait.current.amount,
      upperYaw: upperYaw.current,
      idleTime: clock.elapsedTime,
      shielded,
    });
  });
}
