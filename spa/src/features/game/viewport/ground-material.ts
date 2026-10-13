import { mix, positionWorld, smoothstep, vec3 } from 'three/tsl';
import { MeshLambertNodeMaterial } from 'three/webgpu';

import { colorUniform, valueNoise } from './shader-noise';

const DIRT_COLOR = [0.4, 0.33, 0.24] as const;
const PATCH_SCALE = 0.22;
const PATCH_OFFSET = 17.3;

export function createGrassMaterial(hex: string) {
  const grass = colorUniform(hex);
  const ground = positionWorld.xz;
  const broad = valueNoise(ground.mul(0.07));
  const medium = valueNoise(ground.mul(0.6));
  const fine = valueNoise(ground.mul(3.5));
  const tone = broad.mul(0.3).add(medium.mul(0.15)).add(fine.mul(0.1)).add(0.75);
  const ragged = valueNoise(ground.mul(2.5)).mul(0.12);
  const patchField = valueNoise(ground.mul(PATCH_SCALE).add(PATCH_OFFSET)).add(ragged);
  const dirt = smoothstep(0.74, 0.8, patchField).mul(0.85);

  const material = new MeshLambertNodeMaterial();
  material.colorNode = mix(grass.mul(tone), vec3(...DIRT_COLOR).mul(tone), dirt);
  return material;
}
