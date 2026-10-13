import { type BufferGeometry, Float32BufferAttribute } from 'three';

import type { FurniturePart } from './furniture-parts';

export const GRAIN_ATTRIBUTE = 'grain';

const ALONG_SHIFT = 60;
const CROSS_SHIFT = 0.06;
const AXES = [0, 1, 2] as const;

function pseudoRandom(seed: number) {
  const scrambled = Math.sin(seed * 12.9898) * 43758.5453;
  return scrambled - Math.floor(scrambled);
}

function longAxis({ shape, size }: FurniturePart) {
  if (shape === 'cylinder') return 1;
  return size.indexOf(Math.max(...size));
}

function grainShift(part: FurniturePart, x: number, y: number) {
  const seed =
    x * 7.31 + y * 3.77 + part.position[0] * 5.13 + part.position[1] * 2.9 + part.position[2];
  return [
    (pseudoRandom(seed) - 0.5) * 2 * ALONG_SHIFT,
    (pseudoRandom(seed + 1) - 0.5) * 2 * CROSS_SHIFT,
    (pseudoRandom(seed + 2) - 0.5) * 2 * CROSS_SHIFT,
  ] as const;
}

// Packs (along, cross, cross, isEndFace) in part-local space so the grain follows each board's long axis.
export function addGrainAttribute(
  geometry: BufferGeometry,
  part: FurniturePart,
  x: number,
  y: number,
) {
  const axis = longAxis(part);
  const [first, second] = AXES.filter((candidate) => candidate !== axis);
  const [alongShift, firstShift, secondShift] = grainShift(part, x, y);
  const { position, normal } = geometry.attributes;
  const data = new Float32Array(position.count * 4);
  for (let index = 0; index < position.count; index++) {
    data[index * 4] = position.getComponent(index, axis) + alongShift;
    data[index * 4 + 1] = position.getComponent(index, first) + firstShift;
    data[index * 4 + 2] = position.getComponent(index, second) + secondShift;
    data[index * 4 + 3] = Math.abs(normal.getComponent(index, axis)) > 0.5 ? 1 : 0;
  }
  geometry.setAttribute(GRAIN_ATTRIBUTE, new Float32BufferAttribute(data, 4));
}
