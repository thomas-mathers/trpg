import { useFrame } from '@react-three/fiber';
import { useEffect, useLayoutEffect, useRef, type RefObject } from 'react';
import type { Group } from 'three';

import { glidePosition, glideTo, settledAt, type Glide } from './glide';
import { toScenePosition } from './layout-math';

export function useGlidingPosition(
  group: RefObject<Group | null>,
  x: number,
  y: number,
  snap: boolean,
) {
  const glide = useRef<Glide>(settledAt(x, y, performance.now()));
  const lastUpdateAt = useRef(performance.now());
  const wasSnapping = useRef(snap);

  useLayoutEffect(() => {
    group.current?.position.set(...toScenePosition(x, y));
    // Initial placement only; useFrame drives all later movement.
  }, []);

  useEffect(() => {
    const now = performance.now();
    const snapped = snap || wasSnapping.current !== snap;
    wasSnapping.current = snap;
    glide.current = snapped
      ? settledAt(x, y, now)
      : glideTo(glide.current, x, y, now, lastUpdateAt.current);
    lastUpdateAt.current = now;
  }, [x, y, snap]);

  useFrame(() => {
    if (!group.current) return;
    const position = glidePosition(glide.current, performance.now());
    group.current.position.set(...toScenePosition(position.x, position.y));
  });
}
