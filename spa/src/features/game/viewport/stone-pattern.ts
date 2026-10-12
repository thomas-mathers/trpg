import { fract, floor, max, mix, step } from 'three/tsl';

import { hash, type Scalar, type colorUniform } from './shader-noise';

const STONE_COURSE = 0.28;
const STONE_BLOCK = 0.55;

export function stoneCourses(across: Scalar, up: Scalar, base: ReturnType<typeof colorUniform>) {
  const course = floor(up.div(STONE_COURSE));
  const staggered = across.div(STONE_BLOCK).add(step(0.5, fract(course.mul(0.5))).mul(0.5));
  const block = floor(staggered);
  const mortar = max(step(fract(staggered), 0.05), step(fract(up.div(STONE_COURSE)), 0.1));
  const tone = hash(block.add(course.mul(7.3)), 3.1)
    .mul(0.3)
    .add(0.85);
  return mix(base.mul(tone), base.mul(0.5), mortar);
}
