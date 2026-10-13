import { useFrame } from '@react-three/fiber';
import type { RefObject } from 'react';
import type { MeshStandardMaterial } from 'three';

import { windowGlow } from './window-glow';

const LAMP_INTENSITY = 1.2;

export function useLampGlow(material: RefObject<MeshStandardMaterial | null>) {
  useFrame(() => {
    if (material.current) material.current.emissiveIntensity = LAMP_INTENSITY * windowGlow.value;
  });
}
