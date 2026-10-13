import { float, fract, floor, max, mix, smoothstep, step, vec2, vec3 } from 'three/tsl';

import { hash, type Scalar, type colorUniform, valueNoise } from './shader-noise';

const STONE_COURSE = 0.28;
const STONE_BLOCK = 0.55;
const MOSS_HEIGHT = 0.4;
const MOSS_COLOR = [0.2, 0.26, 0.13] as const;
const MOSS_STRENGTH = 0.5;

export function stoneCourses(across: Scalar, up: Scalar, base: ReturnType<typeof colorUniform>) {
  const course = floor(up.div(STONE_COURSE));
  const staggered = across.div(STONE_BLOCK).add(step(0.5, fract(course.mul(0.5))).mul(0.5));
  const block = floor(staggered);
  const mortar = max(step(fract(staggered), 0.05), step(fract(up.div(STONE_COURSE)), 0.1));
  const tone = hash(block.add(course.mul(7.3)), 3.1)
    .mul(0.3)
    .add(0.85);
  const point = vec2(across, up);
  const grit = valueNoise(point.mul(16)).mul(0.2).add(0.9);
  const patchy = valueNoise(point.mul(2.2)).mul(0.5).add(0.5);
  const groundContact = float(1).sub(smoothstep(0, MOSS_HEIGHT, up));
  const moss = groundContact.mul(patchy).mul(MOSS_STRENGTH);
  const stone = mix(base.mul(tone).mul(grit), base.mul(0.5), mortar);
  return mix(stone, vec3(...MOSS_COLOR), moss);
}
