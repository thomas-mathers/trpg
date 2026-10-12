import { abs, normalLocal, positionLocal, positionWorld, select } from 'three/tsl';
import { MeshLambertNodeMaterial } from 'three/webgpu';

import { colorUniform } from './shader-noise';
import { stoneCourses } from './stone-pattern';

export function createStoneWallMaterial(hex: string) {
  const stone = colorUniform(hex);
  const across = select(abs(normalLocal.x).greaterThan(0.5), positionLocal.z, positionLocal.x);

  const material = new MeshLambertNodeMaterial();
  material.colorNode = stoneCourses(across, positionWorld.y, stone);
  return material;
}
