import {
  abs,
  attribute,
  float,
  floor,
  fract,
  max,
  mix,
  normalLocal,
  positionLocal,
  select,
  smoothstep,
  step,
  vec3,
} from 'three/tsl';
import { MeshStandardNodeMaterial } from 'three/webgpu';

import { band, colorUniform, hash } from './shader-noise';

export interface RoofMaterialSpec {
  span: number;
  length: number;
  rise: number;
  tile: string;
  plaster: string;
  timber: string;
}

const TILE_ROW = 0.24;
const TILE_WIDTH = 0.32;
const BRICK_COURSE = 0.1;
const BRICK_WIDTH = 0.24;
const BRICK_COLOR = '#8a4a3a';
const SOOT_COLOR = '#2a2522';
const GABLE_TIMBER_HALF = 0.13;
const KING_POST_HALF = 0.09;
const COLLAR_BEAM_HALF = 0.07;
const COLLAR_BEAM_RATIO = 0.38;

export function createRoofMaterial(spec: RoofMaterialSpec) {
  const half = spec.span / 2;
  const slopeLength = Math.hypot(half, spec.rise);
  const tile = colorUniform(spec.tile);
  const plaster = colorUniform(spec.plaster);
  const timber = colorUniform(spec.timber);
  const brick = colorUniform(BRICK_COLOR);
  const soot = colorUniform(SOOT_COLOR);

  const { x, y, z } = positionLocal;
  const isChimney = step(0.5, attribute('part', 'float'));
  const isCap = step(0.5, abs(normalLocal.z));
  const isUnder = step(normalLocal.y, -0.5);
  const isTop = step(0.5, normalLocal.y);

  const edgeGap = float(spec.rise)
    .mul(float(1).sub(abs(x).div(half)))
    .sub(y);
  const gableTimber = max(
    max(band(edgeGap.mul(half / slopeLength), GABLE_TIMBER_HALF), band(abs(x), KING_POST_HALF)),
    band(abs(y.sub(spec.rise * COLLAR_BEAM_RATIO)), COLLAR_BEAM_HALF),
  );
  const gable = mix(plaster, timber, gableTimber);

  const along = float(spec.rise)
    .sub(y)
    .mul(slopeLength / spec.rise);
  const row = floor(along.div(TILE_ROW));
  const column = z
    .add(spec.length / 2)
    .div(TILE_WIDTH)
    .add(step(0.5, fract(row.mul(0.5))).mul(0.5));
  const tileTone = hash(floor(column), row).mul(0.3).add(0.8);
  const rowShade = mix(float(0.6), float(1), smoothstep(0, 0.45, fract(along.div(TILE_ROW))));
  const gapShade = mix(float(1), float(0.7), step(fract(column), 0.06));
  const slope = tile.mul(tileTone).mul(rowShade).mul(gapShade);

  const alongFace = select(abs(normalLocal.x).greaterThan(0.5), z, x);
  const course = floor(y.div(BRICK_COURSE));
  const brickAcross = alongFace.div(BRICK_WIDTH).add(step(0.5, fract(course.mul(0.5))).mul(0.5));
  const mortar = max(step(fract(brickAcross), 0.06), step(fract(y.div(BRICK_COURSE)), 0.12));
  const brickTone = hash(floor(brickAcross), course).mul(0.35).add(0.75);
  const brickWall = mix(brick.mul(brickTone), vec3(0.62, 0.58, 0.52), mortar);
  const chimney = mix(brickWall, soot, isTop);

  const roof = mix(slope, timber.mul(0.6), isUnder);
  const surface = mix(roof, gable, isCap);

  const material = new MeshStandardNodeMaterial();
  material.colorNode = mix(surface, chimney, isChimney);
  material.roughness = 0.95;
  return material;
}
