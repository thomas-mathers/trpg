import type { ThreeElements } from '@react-three/fiber';

import { creatureMaterial } from './creature-resources';

export function Part({ color, ...props }: ThreeElements['mesh'] & { color: string }) {
  return (
    <mesh castShadow receiveShadow material={creatureMaterial(color)} dispose={null} {...props} />
  );
}
