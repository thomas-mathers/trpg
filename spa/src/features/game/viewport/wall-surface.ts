import { abs, dot, float, fwidth, mix, smoothstep, step, vec2, vec3 } from 'three/tsl';

import { type colorUniform, type Planar, type Scalar, valueNoise } from './shader-noise';
import { sideTone } from './wood-material';

type Color = ReturnType<typeof colorUniform>;

const DAMP_HEIGHT = 0.9;
const DAMP_TINT = [0.72, 0.78, 0.7] as const;
const GRAIN_SHIFT = 13;

function crackMask(point: Planar) {
  const jitter = valueNoise(point.mul(17)).sub(0.5).mul(0.35);
  const line = valueNoise(point.mul(3.2).add(jitter));
  const thin = float(1).sub(smoothstep(0.003, 0.009, abs(line.sub(0.5))));
  const region = step(0.68, valueNoise(point.mul(0.45).add(vec2(31, 7))));
  const resolvable = float(1).sub(smoothstep(0.006, 0.02, fwidth(line)));
  return thin.mul(region).mul(resolvable);
}

function dampness(point: Planar, up: Scalar, plinth: Scalar) {
  const rising = float(1).sub(smoothstep(0, DAMP_HEIGHT, up.sub(plinth)));
  return rising.mul(valueNoise(point.mul(3)).mul(0.5).add(0.5));
}

export function plasterSurface(base: Color, across: Scalar, up: Scalar, plinth: Scalar) {
  const point = vec2(across, up);
  const mottle = valueNoise(point.mul(0.6))
    .mul(0.06)
    .add(valueNoise(point.mul(2.7)).mul(0.04))
    .add(0.94);
  const streak = valueNoise(vec2(across.mul(5), up.mul(0.35)));
  const stain = float(1).sub(smoothstep(0.55, 0.85, streak).mul(0.14));
  const damp = mix(vec3(1, 1, 1), vec3(...DAMP_TINT), dampness(point, up, plinth));
  const crack = float(1).sub(crackMask(point).mul(0.35));
  return base.mul(mottle).mul(stain).mul(damp).mul(crack);
}

export function grainDirection(upright: Scalar, diagonal: Scalar, diagonalDirection: Planar) {
  const slanted = mix(vec2(1, 0), diagonalDirection, diagonal);
  return mix(slanted, vec2(0, 1), upright);
}

export function timberSurface(
  base: Color,
  across: Scalar,
  up: Scalar,
  direction: Planar,
  seed: Scalar,
) {
  const point = vec2(across, up);
  const along = dot(point, direction).add(seed.mul(GRAIN_SHIFT));
  const cross = dot(point, vec2(direction.y.mul(-1), direction.x));
  return base.mul(sideTone(along, cross));
}
