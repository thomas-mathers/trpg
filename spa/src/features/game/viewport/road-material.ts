import {
  abs,
  attribute,
  floor,
  fract,
  fwidth,
  max,
  min,
  mix,
  positionWorld,
  smoothstep,
  vec2,
} from 'three/tsl';
import { MeshLambertNodeMaterial } from 'three/webgpu';

import { colorUniform, hashPlanar, valueNoise } from './shader-noise';

export type RoadSurface = 'cobble' | 'gravel' | 'dirt';

export const ROAD_EDGE_ATTRIBUTE = 'roadEdge';

const RAGGED_REACH = { cobble: 0.2, gravel: 0.3, dirt: 0.45 } as const;
const FRINGE_WIDTH = { cobble: 0.25, gravel: 0.4, dirt: 0.6 } as const;
const CELL_SIZE = { cobble: 0.42, gravel: 0.3 } as const;
const GAP_WIDTH = { cobble: 0.12, gravel: 0.08 } as const;
const GRID_COS = 0.866;
const GRID_SIN = 0.5;
const WEAR_SCALE = 0.35;
const FADE_START = 0.25;
const FADE_END = 0.6;

function paved(surface: 'cobble' | 'gravel', hex: string) {
  const base = colorUniform(hex);
  const world = positionWorld.xz;
  const rotated = vec2(
    world.x.mul(GRID_COS).sub(world.y.mul(GRID_SIN)),
    world.x.mul(GRID_SIN).add(world.y.mul(GRID_COS)),
  );
  const grid = rotated.div(CELL_SIZE[surface]);
  const local = fract(grid).sub(0.5);

  const roundness = max(abs(local.x), abs(local.y)).mul(2);
  const gap = smoothstep(1 - GAP_WIDTH[surface] * 2, 1, roundness);
  const stoneTone = hashPlanar(floor(grid)).mul(0.4).add(0.8);
  const wear = valueNoise(world.mul(WEAR_SCALE)).mul(0.25).add(0.85);

  const detailed = mix(base.mul(stoneTone).mul(wear), base.mul(0.45), gap);
  const averaged = base.mul(wear).mul(1 - GAP_WIDTH[surface]);
  const distant = smoothstep(FADE_START, FADE_END, fwidth(grid.x));
  return mix(detailed, averaged, distant);
}

function packedDirt(hex: string) {
  const base = colorUniform(hex);
  const world = positionWorld.xz;
  const coarse = valueNoise(world.mul(0.5));
  const grain = valueNoise(world.mul(4));
  const pebbles = valueNoise(world.mul(13));
  const dust = valueNoise(world.mul(31));
  return base.mul(
    coarse.mul(0.3).add(grain.mul(0.15)).add(pebbles.mul(0.1)).add(dust.mul(0.08)).add(0.64),
  );
}

function edgeInset(surface: RoadSurface) {
  const edge = attribute(ROAD_EDGE_ATTRIBUTE, 'vec4');
  const world = positionWorld.xz;
  const wander = valueNoise(world.mul(2.5))
    .mul(0.6)
    .add(valueNoise(world.mul(9)).mul(0.4));
  const inset = min(edge.y.sub(abs(edge.x)), min(edge.z, edge.w));
  return inset.sub(wander.mul(RAGGED_REACH[surface]));
}

export function createRoadMaterial(surface: RoadSurface, hex: string) {
  const material = new MeshLambertNodeMaterial();
  const inset = edgeInset(surface);
  const surfaceColor = surface === 'dirt' ? packedDirt(hex) : paved(surface, hex);
  material.colorNode = surfaceColor;
  material.transparent = true;
  material.opacityNode = smoothstep(0, FRINGE_WIDTH[surface], inset);
  material.maskNode = inset.greaterThan(0);
  return material;
}
