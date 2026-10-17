import { useFrame } from '@react-three/fiber';
import { useRef, type RefObject } from 'react';
import { MathUtils, Vector3, type Group } from 'three';

import { advanceGait, type Gait } from './creature-gait';
import { applyPose, type Stance } from './creature-pose';
import type { CreatureRig } from './creature-rig';

const STRIKE_SECONDS = 0.4;
const TURN_RATE = 10;

export function turnTowards(current: number, target: number, dt: number, rate = TURN_RATE) {
  const difference = Math.atan2(Math.sin(target - current), Math.cos(target - current));
  return current + difference * MathUtils.clamp(1 - Math.exp(-rate * dt), 0, 1);
}

export function useCreaturePose(
  group: RefObject<Group | null>,
  rig: CreatureRig,
  {
    stance,
    upperYawTarget,
    height,
    shielded = false,
    strikeRequested,
  }: {
    stance: Stance;
    upperYawTarget: number;
    height: number;
    shielded?: boolean;
    strikeRequested?: RefObject<boolean>;
  },
) {
  const gait = useRef<Gait>({ phase: 0, amount: 0 });
  const upperYaw = useRef(0);
  const lastPosition = useRef<Vector3 | null>(null);
  const strikeStartedAt = useRef<number | null>(null);

  useFrame(({ clock }, dt) => {
    if (!group.current) return;
    const { position } = group.current;
    const last = (lastPosition.current ??= position.clone());
    advanceGait(gait.current, Math.hypot(position.x - last.x, position.z - last.z), dt, height);
    last.copy(position);
    upperYaw.current = turnTowards(upperYaw.current, upperYawTarget, dt);
    if (strikeRequested?.current) {
      strikeRequested.current = false;
      strikeStartedAt.current = clock.elapsedTime;
    }
    const strike =
      strikeStartedAt.current === null
        ? 0
        : (clock.elapsedTime - strikeStartedAt.current) / STRIKE_SECONDS;
    applyPose(rig, {
      stance,
      gaitPhase: gait.current.phase,
      gaitAmount: gait.current.amount,
      upperYaw: upperYaw.current,
      idleTime: clock.elapsedTime,
      shielded,
      strike,
    });
  });
}
