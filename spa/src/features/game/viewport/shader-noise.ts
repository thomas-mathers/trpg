import { Color, Vector3 } from 'three';
import { dot, float, floor, fract, mix, sin, step, uniform, vec2 } from 'three/tsl';
import type { Node } from 'three/webgpu';

export type Scalar = Node<'float'>;
export type Planar = Node<'vec2'>;

export function hash(a: Scalar, b: Scalar | number) {
  const salt = typeof b === 'number' ? float(b) : b;
  return fract(sin(a.mul(12.9898).add(salt.mul(78.233))).mul(43758.5453));
}

export function hashPlanar(point: Planar) {
  return fract(sin(dot(point, vec2(12.9898, 78.233))).mul(43758.5453));
}

export function valueNoise(point: Planar) {
  const cell = floor(point);
  const local = fract(point);
  const smooth = local.mul(local).mul(float(3).sub(local.mul(2)));
  const south = mix(hashPlanar(cell), hashPlanar(cell.add(vec2(1, 0))), smooth.x);
  const north = mix(hashPlanar(cell.add(vec2(0, 1))), hashPlanar(cell.add(vec2(1, 1))), smooth.x);
  return mix(south, north, smooth.y);
}

export function band(distance: Scalar, halfWidth: number) {
  return step(distance, halfWidth);
}

export function colorUniform(hex: string) {
  const { r, g, b } = new Color(hex);
  return uniform(new Vector3(r, g, b));
}
