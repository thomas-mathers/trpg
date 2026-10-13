import { BoxGeometry, ExtrudeGeometry, Float32BufferAttribute, Shape } from 'three';
import type { BufferGeometry } from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

import type { RoofShape } from './building-roof';

export const ROOF_PART = 0;
export const CHIMNEY_PART = 1;

const CHIMNEY_WIDTH = 0.8;
const CHIMNEY_ABOVE_RIDGE = 0.9;

function tagged(geometry: BufferGeometry, part: number) {
  const flat = geometry.index ? geometry.toNonIndexed() : geometry;
  const { count } = flat.getAttribute('position');
  flat.setAttribute('part', new Float32BufferAttribute(new Array<number>(count).fill(part), 1));
  return flat;
}

function gableGeometry({ span, length, rise }: RoofShape) {
  const outline = new Shape();
  outline.moveTo(-span / 2, 0);
  outline.lineTo(span / 2, 0);
  outline.lineTo(0, rise);
  outline.closePath();
  const geometry = new ExtrudeGeometry(outline, { depth: length, bevelEnabled: false });
  geometry.translate(0, 0, -length / 2);
  return geometry;
}

function chimneyGeometry({
  rise,
  chimney,
}: RoofShape & { chimney: NonNullable<RoofShape['chimney']> }) {
  const height = rise + CHIMNEY_ABOVE_RIDGE;
  const geometry = new BoxGeometry(CHIMNEY_WIDTH, height, CHIMNEY_WIDTH);
  geometry.translate(chimney.x, height / 2, chimney.z);
  return geometry;
}

export function roofGeometry(roof: RoofShape) {
  const parts = [tagged(gableGeometry(roof), ROOF_PART)];
  if (roof.chimney)
    parts.push(tagged(chimneyGeometry({ ...roof, chimney: roof.chimney }), CHIMNEY_PART));
  return parts.length === 1 ? parts[0] : mergeGeometries(parts);
}
