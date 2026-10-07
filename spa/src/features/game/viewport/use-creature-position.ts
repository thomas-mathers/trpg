import { useFrame } from '@react-three/fiber';
import { useLayoutEffect, useRef, type RefObject } from 'react';
import { MathUtils, type Group } from 'three';

import type {
  CreatureWalkSnapshot,
  PlacementWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { gameTimeMillisecondsAt, type GameClockAnchor } from '@/features/game/game-clock';

import { toScenePosition } from './layout-math';
import { blendOffset, walkedDistance, walkPoseAt, walkSignature } from './walk-playback';

const BLEND_MILLISECONDS = 200;

interface Planar {
  x: number;
  y: number;
}

interface Target extends Planar {
  heading?: number;
  hidden: boolean;
}

interface Inputs {
  placement: PlacementWire;
  walk?: CreatureWalkSnapshot;
  clock: GameClockAnchor;
  seated: boolean;
  debugName: string;
}

function targetAt({ placement, walk, clock, seated }: Inputs, nowUnixMilliseconds: number): Target {
  if (seated || !walk) return { x: placement.x, y: placement.y, hidden: false };

  const distance = walkedDistance(walk, gameTimeMillisecondsAt(clock, nowUnixMilliseconds));
  const pose = walkPoseAt(walk.points, distance);
  return {
    x: pose.x,
    y: pose.y,
    heading: pose.finished ? undefined : pose.angle,
    hidden: pose.finished && walk.leavesAtEnd,
  };
}

function signatureOf({ placement, walk, seated }: Inputs): string {
  return !seated && walk ? walkSignature(walk) : `static:${placement.x}:${placement.y}`;
}

function logWalkChange(
  name: string,
  previous: CreatureWalkSnapshot | undefined,
  next: CreatureWalkSnapshot | undefined,
  gapMeters: number,
) {
  const describe = (walk: CreatureWalkSnapshot | undefined) =>
    walk
      ? {
          startedAt: walk.startedAtGameTimeMilliseconds,
          pausedAt: walk.pausedAtGameTimeMilliseconds,
          pace: walk.metersPerGameSecond,
          from: walk.points[0],
          to: walk.points[walk.points.length - 1],
          leavesAtEnd: walk.leavesAtEnd,
        }
      : null;
  console.debug(`[walk] ${name} changed, gap ${gapMeters.toFixed(2)} m`, {
    previous: describe(previous),
    next: describe(next),
  });
}

export function useCreaturePosition(
  group: RefObject<Group | null>,
  inputs: Inputs,
): RefObject<number | undefined> {
  const latest = useRef(inputs);
  const heading = useRef<number | undefined>(undefined);
  const displayed = useRef<Planar | null>(null);
  const offset = useRef<Planar>({ x: 0, y: 0 });
  const blendStartedAt = useRef(0);
  const signature = useRef(signatureOf(inputs));
  const wasSeated = useRef(inputs.seated);
  const shownWalk = useRef(inputs.walk);

  useLayoutEffect(() => {
    latest.current = inputs;
  });

  useLayoutEffect(() => {
    const target = targetAt(latest.current, Date.now());
    group.current?.position.set(...toScenePosition(target.x, target.y));
    displayed.current = target;
    // Initial placement only; useFrame drives all later movement.
  }, []);

  useFrame(() => {
    if (!group.current) return;
    const now = Date.now();
    const current = latest.current;
    const target = targetAt(current, now);

    const nextSignature = signatureOf(current);
    if (nextSignature !== signature.current) {
      signature.current = nextSignature;
      const from = displayed.current;
      offset.current =
        wasSeated.current === current.seated ? blendOffset(from, target) : { x: 0, y: 0 };
      blendStartedAt.current = now;
      if (import.meta.env.DEV) {
        const gap = from ? Math.hypot(from.x - target.x, from.y - target.y) : 0;
        logWalkChange(current.debugName, shownWalk.current, current.walk, gap);
      }
      shownWalk.current = current.walk;
    }
    wasSeated.current = current.seated;

    const remaining =
      1 - MathUtils.clamp((now - blendStartedAt.current) / BLEND_MILLISECONDS, 0, 1);
    const x = target.x + offset.current.x * remaining;
    const y = target.y + offset.current.y * remaining;
    displayed.current = { x, y };
    heading.current = target.heading;
    group.current.position.set(...toScenePosition(x, y));
    group.current.visible = !target.hidden;
  });

  return heading;
}
