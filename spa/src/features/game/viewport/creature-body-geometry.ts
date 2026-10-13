import { BufferGeometry, Color, Float32BufferAttribute, Uint16BufferAttribute } from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

import type { CreatureAppearance } from './creature-appearance';
import { bodyParts, type BodyPart } from './creature-body-parts';
import type { Gear } from './creature-gear';
import { headParts } from './creature-head-parts';
import { BONE_NAMES } from './creature-rig';

const geometries = new Map<string, BufferGeometry>();

function bakedPart({ bone, geometry, matrix, color }: BodyPart) {
  const baked = geometry.clone().applyMatrix4(matrix);
  const count = baked.attributes.position.count;
  const colors = new Float32Array(count * 3);
  const skinIndex = new Uint16Array(count * 4);
  const skinWeight = new Float32Array(count * 4);
  const rgb = new Color(color);
  const index = BONE_NAMES.indexOf(bone);
  for (let vertex = 0; vertex < count; vertex++) {
    rgb.toArray(colors, vertex * 3);
    skinIndex[vertex * 4] = index;
    skinWeight[vertex * 4] = 1;
  }
  baked.setAttribute('color', new Float32BufferAttribute(colors, 3));
  baked.setAttribute('skinIndex', new Uint16BufferAttribute(skinIndex, 4));
  baked.setAttribute('skinWeight', new Float32BufferAttribute(skinWeight, 4));
  return baked;
}

function bake(parts: BodyPart[]) {
  const pieces = parts.map(bakedPart);
  const merged = mergeGeometries(pieces);
  pieces.forEach((piece) => piece.dispose());
  if (!merged) throw new Error('Unable to merge creature geometry');
  merged.computeBoundingBox();
  merged.computeBoundingSphere();
  return merged;
}

export type BodyScope = 'whole' | 'body' | 'head';

function scopedParts(appearance: CreatureAppearance, gear: Gear, scope: BodyScope) {
  const body = bodyParts(appearance, gear);
  const head = headParts(appearance, gear.armor.Helm);
  if (scope === 'body') return body;
  return scope === 'head' ? head : [...body, ...head];
}

function geometryKey(appearance: CreatureAppearance, gear: Gear, scope: BodyScope) {
  const { skin, garment, feature, elderly } = appearance;
  const { Helm, Chest, Boots, Gloves } = gear.armor;
  return [scope, skin, garment, feature, elderly, Helm, Chest, Boots, Gloves].join('|');
}

export function creatureBodyGeometry(
  appearance: CreatureAppearance,
  gear: Gear,
  scope: BodyScope = 'whole',
) {
  const key = geometryKey(appearance, gear, scope);
  let geometry = geometries.get(key);
  if (!geometry) {
    geometry = bake(scopedParts(appearance, gear, scope));
    geometries.set(key, geometry);
  }
  return geometry;
}
