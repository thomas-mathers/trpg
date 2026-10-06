import { useEffect } from 'react';
import type { Camera } from 'three';

import type { PlacementWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { headingToYaw } from './layout-math';
import { SEATED_EYE_HEIGHT } from './seat-interaction';

export function useSeatedCamera(
  camera: Camera,
  seated: boolean,
  seatedPlacement: PlacementWire | undefined,
) {
  useEffect(() => {
    if (!seated) return;
    if (seatedPlacement) {
      camera.position.set(seatedPlacement.x, SEATED_EYE_HEIGHT, seatedPlacement.y);
      camera.rotation.set(0, headingToYaw(seatedPlacement.angle), 0);
    }
    camera.position.y = SEATED_EYE_HEIGHT;
  }, [camera, seated, seatedPlacement]);
}
