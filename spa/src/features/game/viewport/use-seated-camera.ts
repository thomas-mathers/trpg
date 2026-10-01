import { useEffect, useRef } from 'react';
import type { Camera } from 'three';

import type {
  FootprintWire,
  PlacementWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import {
  clampToBounds,
  EYE_HEIGHT,
  headingToYaw,
  type Obstacle,
  pushOutOfObstacles,
} from './layout-math';
import { SEATED_EYE_HEIGHT } from './seat-interaction';

export function useSeatedCamera(
  camera: Camera,
  seated: boolean,
  seatedPlacement: PlacementWire | undefined,
  start: PlacementWire,
  obstacles: Obstacle[],
  size: FootprintWire,
) {
  const standingPosition = useRef<{ x: number; y: number } | undefined>(undefined);
  const { x, y, angle } = start;
  useEffect(() => {
    if (seated) {
      standingPosition.current ??= { x: camera.position.x, y: camera.position.z };
      if (seatedPlacement) {
        camera.position.set(seatedPlacement.x, SEATED_EYE_HEIGHT, seatedPlacement.y);
        camera.rotation.set(0, headingToYaw(seatedPlacement.angle), 0);
      }
      camera.position.y = SEATED_EYE_HEIGHT;
    } else if (standingPosition.current) {
      const next = clampToBounds(pushOutOfObstacles(standingPosition.current, obstacles), size);
      camera.position.set(next.x, EYE_HEIGHT, next.y);
      standingPosition.current = undefined;
    }
  }, [camera, x, y, angle, seated, seatedPlacement?.x, seatedPlacement?.y, seatedPlacement?.angle]);
}
