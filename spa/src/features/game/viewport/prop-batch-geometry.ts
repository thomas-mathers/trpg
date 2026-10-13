import { BufferGeometry, Color, Euler, Float32BufferAttribute, Matrix4 } from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

import type {
  NearbyPropSnapshot,
  PlacementWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { furnitureGeometry } from './furniture-geometry';
import type { FurniturePart } from './furniture-parts';
import { headingToYaw } from './layout-math';
import { canBatchProp, propParts } from './prop-parts';
import { addGrainAttribute } from './wood-grain';

const CELL_SIZE = 12;

export interface PropBatch {
  key: string;
  geometry: BufferGeometry;
  finish: FurniturePart['finish'];
  emissive: string;
  doubleSided: boolean;
}

export interface MovingPropPart {
  key: string;
  placement: PlacementWire;
  part: FurniturePart;
}

function partGeometry(part: FurniturePart, { x, y, angle }: PlacementWire) {
  const geometry = furnitureGeometry(part);
  if (part.finish === 'wood') addGrainAttribute(geometry, part, x, y);
  if (part.rotation)
    geometry.applyMatrix4(new Matrix4().makeRotationFromEuler(new Euler(...part.rotation)));
  geometry.translate(...part.position);
  geometry.rotateY(headingToYaw(angle));
  geometry.translate(x, 0, y);
  const color = new Color(part.color);
  const colors = new Float32Array(geometry.attributes.position.count * 3);
  for (let index = 0; index < colors.length; index += 3) color.toArray(colors, index);
  geometry.setAttribute('color', new Float32BufferAttribute(colors, 3));
  return geometry;
}

function isEmissive({ finish }: FurniturePart) {
  return finish === 'glow' || finish === 'lamp';
}

function batchKey(part: FurniturePart, { x, y }: PlacementWire) {
  const finish = part.finish ?? 'matte';
  const color = isEmissive(part) ? part.color : '';
  return `${Math.floor(x / CELL_SIZE)}:${Math.floor(y / CELL_SIZE)}:${finish}:${part.shape === 'basin'}:${color}`;
}

function mergeBatch(key: string, parts: BufferGeometry[], part: FurniturePart): PropBatch {
  const geometry = mergeGeometries(parts);
  parts.forEach((piece) => piece.dispose());
  if (!geometry) throw new Error('Unable to merge prop geometry');
  geometry.computeBoundingBox();
  geometry.computeBoundingSphere();
  return {
    key,
    geometry,
    finish: part.finish,
    emissive: isEmissive(part) ? part.color : '#000000',
    doubleSided: part.shape === 'basin',
  };
}

export function buildPropBatches(props: NearbyPropSnapshot[]) {
  const groups = new Map<string, { geometries: BufferGeometry[]; part: FurniturePart }>();
  const moving: MovingPropPart[] = [];
  for (const prop of props.filter(canBatchProp)) {
    propParts(prop).forEach((part, index) => {
      if (part.opacity !== undefined || part.finish === 'water') {
        moving.push({ key: `${prop.id}:${index}`, placement: prop.placement, part });
        return;
      }
      const key = batchKey(part, prop.placement);
      const group = groups.get(key) ?? { geometries: [], part };
      group.geometries.push(partGeometry(part, prop.placement));
      groups.set(key, group);
    });
  }
  const batches = [...groups].map(([key, { geometries, part }]) =>
    mergeBatch(key, geometries, part),
  );
  return { batches, moving };
}
