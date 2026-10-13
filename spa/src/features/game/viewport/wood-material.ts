import {
  attribute,
  float,
  fract,
  fwidth,
  length,
  mix,
  sin,
  smoothstep,
  vec2,
  vec3,
} from 'three/tsl';
import { MeshStandardNodeMaterial } from 'three/webgpu';

import { type Scalar, valueNoise } from './shader-noise';
import { GRAIN_ATTRIBUTE } from './wood-grain';

const SWEEP_FREQUENCY = 5;
const FIBRE_ALONG = 4;
const FIBRE_ACROSS = 70;
const LINE_FREQUENCY = 70;
const LINE_WOBBLE = 6;
const LINE_FADE_START = 0.35;
const LINE_FADE_END = 0.9;
const RING_DENSITY = 28;

export function sideTone(along: Scalar, across: Scalar) {
  const sweep = valueNoise(vec2(along.mul(1.4), across.mul(SWEEP_FREQUENCY)));
  const fibre = valueNoise(vec2(along.mul(FIBRE_ALONG), across.mul(FIBRE_ACROSS)));
  const phase = across.mul(LINE_FREQUENCY).add(sweep.mul(LINE_WOBBLE));
  const line = smoothstep(0.7, 1, sin(phase).mul(0.5).add(0.5));
  const crisp = float(1).sub(smoothstep(LINE_FADE_START, LINE_FADE_END, fwidth(phase)));
  return sweep.mul(0.18).add(fibre.mul(0.14)).sub(line.mul(crisp).mul(0.16)).add(0.87);
}

function endTone(first: Scalar, second: Scalar) {
  const point = vec2(first, second);
  const radius = length(point).add(valueNoise(point.mul(24)).mul(0.012));
  const ring = smoothstep(0.55, 0.95, fract(radius.mul(RING_DENSITY)));
  return float(0.94).sub(ring.mul(0.2));
}

function grainTone() {
  const grain = attribute(GRAIN_ATTRIBUTE, 'vec4');
  const across = grain.y.add(grain.z.mul(0.85));
  return mix(sideTone(grain.x, across), endTone(grain.y, grain.z), grain.w);
}

let shared: MeshStandardNodeMaterial | undefined;

export function woodMaterial() {
  if (shared) return shared;
  shared = new MeshStandardNodeMaterial({ vertexColors: true, roughness: 1 });
  shared.colorNode = vec3(grainTone());
  return shared;
}
