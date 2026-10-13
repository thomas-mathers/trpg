import { Vector3 } from 'three';
import {
  abs,
  float,
  floor,
  fract,
  max,
  min,
  mix,
  normalLocal,
  positionLocal,
  round,
  select,
  sqrt,
  step,
  uniform,
  vec2,
  vec3,
} from 'three/tsl';
import { MeshStandardNodeMaterial } from 'three/webgpu';

import { band, colorUniform, hash } from './shader-noise';
import { stoneCourses } from './stone-pattern';
import { grainDirection, plasterSurface, timberSurface } from './wall-surface';
import { windowGlow } from './window-glow';

export interface WallMaterialSpec {
  width: number;
  height: number;
  depth: number;
  storeys: number;
  plinth: number;
  plaster: string;
  stone: string;
  timber: string;
  seed: number;
}

const POST_SPACING = 1.9;
const POST_HALF = 0.09;
const RAIL_HALF = 0.1;
const SILL_HEIGHT = 0.14;
const BRACE_HALF = 0.06;
const WINDOW_PANEL_CHANCE = 0.35;
const BRACE_PANEL_CHANCE = 0.7;
const MIN_WINDOW_PANEL = 1.2;
const WINDOW_CENTER_RATIO = 0.55;
const GLASS_HALF = [0.3, 0.42] as const;
const FRAME_HALF = [0.38, 0.5] as const;
const MULLION_HALF = 0.022;
const DOOR_CLEARANCE = 1.3;
const WARM_LIGHT = [1, 0.55, 0.2] as const;
const GLASS_COLOR = [0.05, 0.07, 0.09] as const;
const GLOW_INTENSITY = 0.35;

export function createWallMaterial(spec: WallMaterialSpec) {
  const size = uniform(new Vector3(spec.width, spec.height, spec.depth));
  const storeys = uniform(spec.storeys);
  const plinth = uniform(spec.plinth);
  const seed = uniform(spec.seed);
  const plaster = colorUniform(spec.plaster);
  const stone = colorUniform(spec.stone);
  const timber = colorUniform(spec.timber);

  const facingX = step(0.5, abs(normalLocal.x));
  const facingZ = step(0.5, abs(normalLocal.z));
  const onWall = max(facingX, facingZ);
  const faceId = select(
    facingX.greaterThan(0.5),
    step(0, normalLocal.x),
    step(0, normalLocal.z).add(2),
  );
  const length = select(facingX.greaterThan(0.5), size.z, size.x);
  const across = select(
    facingX.greaterThan(0.5),
    positionLocal.z.add(size.z.mul(0.5)),
    positionLocal.x.add(size.x.mul(0.5)),
  );
  const up = positionLocal.y.add(size.y.mul(0.5));

  const storeyHeight = size.y.div(storeys);
  const storey = min(floor(up.div(storeyHeight)), storeys.sub(1));
  const inStorey = up.sub(storey.mul(storeyHeight));

  const panels = max(round(length.div(POST_SPACING)), 1);
  const cell = length.div(panels);
  const panelAcross = across.div(cell);
  const panelIndex = floor(panelAcross);
  const panelU = fract(panelAcross);
  const panelV = inStorey.div(storeyHeight);
  const panelSeed = panelIndex.add(faceId.mul(31)).add(storey.mul(7)).add(seed);

  const postDistance = abs(panelAcross.sub(round(panelAcross))).mul(cell);
  const railDistance = min(inStorey, storeyHeight.sub(inStorey));
  const sillBand = step(plinth, up).mul(step(up, plinth.add(SILL_HEIGHT)));

  const hasWindow = step(1 - WINDOW_PANEL_CHANCE, hash(panelSeed, 1.7)).mul(
    step(MIN_WINDOW_PANEL, cell),
  );
  const panelCenter = panelIndex.add(0.5).mul(cell);
  const atDoor = facingZ
    .mul(step(abs(panelCenter.sub(length.mul(0.5))), DOOR_CLEARANCE + FRAME_HALF[0]))
    .mul(step(storey, 0.5));
  const windowAllowed = hasWindow.mul(float(1).sub(atDoor));

  const diagonalFlip = step(0.5, hash(panelSeed, 4.4));
  const diagonalU = mix(panelU, float(1).sub(panelU), diagonalFlip);
  const diagonalScale = cell
    .mul(storeyHeight)
    .div(sqrt(cell.mul(cell).add(storeyHeight.mul(storeyHeight))));
  const brace = step(1 - BRACE_PANEL_CHANCE, hash(panelSeed, 2.9))
    .mul(float(1).sub(hasWindow))
    .mul(band(abs(panelV.sub(diagonalU)).mul(diagonalScale), BRACE_HALF));

  const frame = max(
    max(band(postDistance, POST_HALF), band(railDistance, RAIL_HALF)),
    max(sillBand, brace),
  );

  const windowDx = abs(panelU.sub(0.5)).mul(cell);
  const windowDy = abs(inStorey.sub(storeyHeight.mul(WINDOW_CENTER_RATIO)));
  const glassArea = band(windowDx, GLASS_HALF[0]).mul(band(windowDy, GLASS_HALF[1]));
  const frameArea = band(windowDx, FRAME_HALF[0]).mul(band(windowDy, FRAME_HALF[1]));
  const mullion = max(band(windowDx, MULLION_HALF), band(abs(windowDy.sub(0.05)), MULLION_HALF));
  const glass = glassArea.mul(windowAllowed).mul(float(1).sub(mullion));
  const windowTimber = frameArea.sub(glassArea).max(glassArea.mul(mullion)).mul(windowAllowed);

  const wallAcross = across.add(faceId.mul(17));
  const timberTone = hash(panelIndex.add(storey.mul(5)), faceId)
    .mul(0.35)
    .add(0.8);

  const stoneColor = stoneCourses(wallAcross, up, stone);
  const upright = max(
    band(postDistance, POST_HALF),
    max(step(GLASS_HALF[0], windowDx), band(windowDx, MULLION_HALF)).mul(windowTimber),
  );
  const braceSlope = float(1).sub(diagonalFlip.mul(2)).mul(storeyHeight);
  const direction = grainDirection(upright, brace, vec2(cell, braceSlope).normalize());
  const timberColor = timberSurface(timber, wallAcross, up, direction, seed);
  let wall = plasterSurface(plaster, wallAcross, up, plinth);
  wall = mix(wall, timberColor.mul(timberTone), max(frame, windowTimber));
  wall = mix(wall, vec3(...GLASS_COLOR), glass);
  wall = mix(wall, stoneColor, step(up, plinth));

  const litWindow = step(0.4, hash(panelSeed, 6.1));
  const glow = windowGlow.mul(litWindow).mul(glass);

  const material = new MeshStandardNodeMaterial();
  material.colorNode = mix(plaster, wall, onWall);
  material.emissiveNode = vec3(...WARM_LIGHT)
    .mul(glow)
    .mul(GLOW_INTENSITY);
  material.roughnessNode = mix(float(0.92), float(0.25), glass);
  return material;
}
