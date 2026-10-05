import type { PropModel } from '@/api/signalr-client/TRPG.GameSessions.Responses';

export type FurnitureModel =
  | Extract<PropModel, `Furniture${string}`>
  | Extract<PropModel, 'ContainerBarrel' | 'ContainerCrate'>;

type Vector = [number, number, number];

export type FurniturePart =
  | {
      shape: 'box';
      position: Vector;
      size: Vector;
      color: string;
      opacity?: number;
      rotation?: Vector;
    }
  | {
      shape: 'cylinder';
      position: Vector;
      size: Vector;
      color: string;
      opacity?: number;
      rotation?: Vector;
    }
  | {
      shape: 'sphere';
      position: Vector;
      size: Vector;
      color: string;
      opacity?: number;
      rotation?: Vector;
    };

export interface FurnitureSize {
  width: number;
  depth: number;
  height: number;
  color: string;
}

const DARK_WOOD = '#5e4529';
const DARK_STONE = '#6e6a63';
const CLOTH = '#c9b99a';
const WATER = '#4a8fb8';
const GLOW = '#e19c51';

// A box whose underside rests at `bottom`, centered on the footprint.
function box(
  [x, bottom, z]: Vector,
  [width, height, depth]: Vector,
  color: string,
  opacity?: number,
  rotation?: Vector,
): FurniturePart {
  return {
    shape: 'box',
    position: [x, bottom + height / 2, z],
    size: [width, height, depth],
    color,
    opacity,
    rotation,
  };
}

// Radii are top then bottom, as in three's CylinderGeometry.
function cylinder(
  [x, bottom, z]: Vector,
  [topRadius, bottomRadius, height]: Vector,
  color: string,
  opacity?: number,
  rotation?: Vector,
): FurniturePart {
  return {
    shape: 'cylinder',
    position: [x, bottom + height / 2, z],
    size: [topRadius, bottomRadius, height],
    color,
    opacity,
    rotation,
  };
}

function sphere([x, bottom, z]: Vector, radius: number, color: string): FurniturePart {
  return {
    shape: 'sphere',
    position: [x, bottom + radius, z],
    size: [radius, radius, radius],
    color,
  };
}

function legs({ width, depth }: FurnitureSize, legHeight: number, color: string): FurniturePart[] {
  return [-1, 1].flatMap((x) =>
    [-1, 1].map((z) =>
      box([x * (width / 2 - 0.06), 0, z * (depth / 2 - 0.06)], [0.1, legHeight, 0.1], color),
    ),
  );
}

function seatOf(size: FurnitureSize, hasBack: boolean): FurniturePart[] {
  const { width, depth, height, color } = size;
  const seatTop = hasBack ? 0.5 : height;
  return [
    box([0, seatTop - 0.1, 0], [width, 0.1, depth], color),
    ...legs(size, seatTop - 0.1, DARK_WOOD),
    ...(hasBack
      ? [box([0, seatTop, depth / 2 - 0.04], [width, height - seatTop, 0.08], color)]
      : []),
  ];
}

function tableOf(size: FurnitureSize): FurniturePart[] {
  const { width, depth, height, color } = size;
  return [
    box([0, height - 0.1, 0], [width, 0.1, depth], color),
    ...legs(size, height - 0.1, DARK_WOOD),
  ];
}

function shelving(
  { width, depth, height, color }: FurnitureSize,
  levels: number,
  goods: string,
): FurniturePart[] {
  const levelGap = (height - 0.06) / levels;
  return [
    box([0, 0, depth / 2 - 0.02], [width, height, 0.04], color),
    ...[-1, 1].map((x) => box([x * (width / 2 - 0.03), 0, 0], [0.06, height, depth], color)),
    ...Array.from({ length: levels }, (_, level) => level).flatMap((level) => [
      box([0, level * levelGap, 0], [width, 0.05, depth], color),
      box([0, level * levelGap + 0.05, 0], [width * 0.7, levelGap * 0.35, depth * 0.6], goods),
    ]),
  ];
}

function stackOf(
  { width, depth }: FurnitureSize,
  layers: number,
  layerHeight: number,
  color: string,
): FurniturePart[] {
  return Array.from({ length: layers }, (_, layer) =>
    box(
      [0, layer * layerHeight, 0],
      [width * (1 - layer * 0.12), layerHeight * 0.95, depth * (1 - layer * 0.12)],
      color,
    ),
  );
}

function fountain({ width, height, color }: FurnitureSize): FurniturePart[] {
  const radius = width / 2;
  return [
    cylinder([0, 0, 0], [radius, radius, height * 0.45], color),
    cylinder([0, height * 0.4, 0], [radius * 0.85, radius * 0.85, 0.06], WATER),
    cylinder([0, 0, 0], [0.12, 0.2, height], color),
    cylinder([0, height * 0.7, 0], [radius * 0.4, radius * 0.25, 0.1], color),
  ];
}

function well({ width, depth, height, color }: FurnitureSize): FurniturePart[] {
  const radius = Math.min(width, depth) / 2;
  return [
    cylinder([0, 0, 0], [radius, radius, height * 0.55], color),
    cylinder([0, height * 0.5, 0], [radius * 0.75, radius * 0.75, 0.06], WATER),
    ...[-1, 1].map((x) => box([x * radius * 0.85, 0, 0], [0.08, height, 0.08], DARK_WOOD)),
    box([0, height - 0.08, 0], [radius * 2, 0.08, 0.1], DARK_WOOD),
  ];
}

function statue({ width, height, color }: FurnitureSize): FurniturePart[] {
  const base = height * 0.3;
  return [
    box([0, 0, 0], [width, base, width], DARK_STONE),
    cylinder([0, base, 0], [width * 0.18, width * 0.28, height - base - width * 0.4], color),
    sphere([0, height - width * 0.4, 0], width * 0.2, color),
  ];
}

function shrine({ width, height, color }: FurnitureSize): FurniturePart[] {
  return [
    box([0, 0, 0], [width, height * 0.25, width], DARK_STONE),
    box([0, height * 0.25, 0], [width * 0.45, height * 0.4, width * 0.45], color),
    sphere([0, height * 0.65, 0], width * 0.2, GLOW),
  ];
}

function mannequin({ width, height, color }: FurnitureSize): FurniturePart[] {
  return [
    cylinder([0, 0, 0], [0.04, 0.04, height * 0.55], DARK_WOOD),
    box([0, 0, 0], [width, 0.05, width], DARK_WOOD),
    cylinder([0, height * 0.5, 0], [width * 0.35, width * 0.25, height * 0.35], color),
    sphere([0, height * 0.86, 0], width * 0.2, color),
  ];
}

function dummy({ width, height, color }: FurnitureSize): FurniturePart[] {
  return [
    box([0, 0, 0], [width * 0.6, 0.06, width * 0.6], DARK_WOOD),
    cylinder([0, 0, 0], [0.07, 0.07, height * 0.9], DARK_WOOD),
    box([0, height * 0.55, 0], [width, 0.08, 0.08], DARK_WOOD),
    cylinder([0, height * 0.4, 0], [width * 0.3, width * 0.3, height * 0.35], color),
    sphere([0, height * 0.78, 0], 0.13, color),
  ];
}

function stall({ width, depth, height, color }: FurnitureSize): FurniturePart[] {
  const counterDepth = depth * 0.3;
  const canopyThickness = 0.12;
  const postHeight = height - canopyThickness + 0.01;
  return [
    box([0, 0, -depth / 2 + counterDepth / 2], [width, 0.9, counterDepth], color),
    ...[-1, 1].flatMap((x) =>
      [-1, 1].map((z) =>
        box([x * (width / 2 - 0.08), 0, z * (depth / 2 - 0.08)], [0.1, postHeight, 0.1], DARK_WOOD),
      ),
    ),
    box([0, height - canopyThickness, 0], [width, canopyThickness, depth], CLOTH),
  ];
}

function fireplace({ width, depth, height, color }: FurnitureSize): FurniturePart[] {
  const openingWidth = width * 0.55;
  const openingHeight = height * 0.45;
  const jambWidth = (width - openingWidth) / 2;
  const backZ = depth / 2 - 0.04;
  return [
    ...[-1, 1].map((side) =>
      box([side * (width / 2 - jambWidth / 2), 0, 0], [jambWidth, height, depth], color),
    ),
    box([0, openingHeight, 0], [openingWidth, height - openingHeight, depth], color),
    box([0, 0, backZ], [openingWidth, openingHeight, 0.04], '#2e2a26'),
    box([0, 0.04, backZ - 0.06], [width * 0.3, height * 0.2, 0.03], GLOW),
  ];
}

function noticeBoard({ width, depth, height, color }: FurnitureSize): FurniturePart[] {
  return [
    ...[-1, 1].map((x) => box([x * (width / 2 - 0.08), 0, 0], [0.1, height, depth], DARK_WOOD)),
    box([0, height * 0.35, 0.01], [width - 0.2, height * 0.55, depth - 0.02], color),
    box([-width * 0.2, height * 0.5, -depth / 2 + 0.01], [0.3, 0.4, 0.02], CLOTH),
    box([width * 0.15, height * 0.45, -depth / 2 + 0.01], [0.35, 0.3, 0.02], CLOTH),
  ];
}

function lectern({ width, depth, height, color }: FurnitureSize): FurniturePart[] {
  return [
    box([0, 0, 0], [width * 0.6, height * 0.8, depth * 0.5], color),
    box([0, height * 0.8, 0], [width, 0.08, depth], DARK_WOOD),
    box([0, height * 0.88, 0], [width * 0.7, 0.04, depth * 0.7], CLOTH),
  ];
}

function cauldron({ width, depth, height, color }: FurnitureSize): FurniturePart[] {
  const radius = Math.min(width, depth) / 2;
  return [
    cylinder([0, 0.1, 0], [radius, radius * 0.8, height - 0.1], color),
    cylinder([0, height - 0.08, 0], [radius * 0.85, radius * 0.85, 0.06], '#4f7a4a'),
    ...[-1, 1].flatMap((x) =>
      [-1, 1].map((z) => box([x * radius * 0.55, 0, z * radius * 0.55], [0.08, 0.1, 0.08], color)),
    ),
  ];
}

function displayCase({ width, depth, height, color }: FurnitureSize): FurniturePart[] {
  return [
    box([0, 0, 0], [width, height * 0.5, depth], color),
    box([0, height * 0.5, 0], [width - 0.04, height * 0.5, depth - 0.04], '#bfe3ee', 0.35),
    box([0, height * 0.5, 0], [width * 0.4, 0.12, depth * 0.4], '#c9a227'),
  ];
}

function sacks({ width, depth, height, color }: FurnitureSize): FurniturePart[] {
  const sack = height * 0.5;
  return [
    box([-width * 0.22, 0, 0], [width * 0.5, sack, depth * 0.9], color),
    box([width * 0.25, 0, 0], [width * 0.45, sack, depth * 0.9], color),
    box([0, sack, 0], [width * 0.5, sack * 0.9, depth * 0.8], color),
  ];
}

function barrel({ width, depth, height, color }: FurnitureSize): FurniturePart[] {
  const radius = Math.min(width, depth) / 2;
  const half = height / 2;
  const hoop = radius * 0.93;
  return [
    cylinder([0, 0, 0], [radius, radius * 0.85, half], color),
    cylinder([0, half, 0], [radius * 0.85, radius, half - 0.03], color),
    cylinder([0, height - 0.03, 0], [radius * 0.8, radius * 0.8, 0.03], DARK_WOOD),
    cylinder([0, height * 0.2, 0], [hoop, hoop, 0.06], DARK_STONE),
    cylinder([0, height * 0.75, 0], [hoop, hoop, 0.06], DARK_STONE),
  ];
}

function crate({ width, depth, height, color }: FurnitureSize): FurniturePart[] {
  return [
    box([0, 0, 0], [width - 0.04, height - 0.05, depth - 0.04], color),
    box([0, height - 0.05, 0], [width, 0.05, depth], DARK_WOOD),
    ...[-1, 1].flatMap((x) =>
      [-1, 1].map((z) =>
        box(
          [x * (width / 2 - 0.035), 0, z * (depth / 2 - 0.035)],
          [0.07, height - 0.05, 0.07],
          DARK_WOOD,
        ),
      ),
    ),
    ...[-1, 1].map((z) =>
      box([0, height * 0.4, z * (depth / 2 - 0.01)], [width - 0.14, 0.07, 0.02], DARK_WOOD),
    ),
  ];
}

function waystone({ width, depth, height, color }: FurnitureSize): FurniturePart[] {
  return [
    box([0, 0, 0], [width * 0.8, height * 0.15, depth * 0.8], DARK_STONE),
    box([0, height * 0.15, 0], [width * 0.45, height * 0.85, depth * 0.35], color),
    sphere([0, height * 0.55, -depth * 0.18], 0.08, GLOW),
  ];
}

function monument({ width, height, color }: FurnitureSize): FurniturePart[] {
  return [
    box([0, 0, 0], [width, height * 0.12, width], DARK_STONE),
    cylinder([0, height * 0.12, 0], [width * 0.15, width * 0.3, height * 0.88], color),
  ];
}

function firePit({ width, height, color }: FurnitureSize): FurniturePart[] {
  const radius = width / 2;
  return [
    cylinder([0, 0, 0], [radius, radius, height], color),
    cylinder([0, height * 0.7, 0], [radius * 0.75, radius * 0.75, height * 0.3], GLOW),
  ];
}

function tree({ width, height, color }: FurnitureSize): FurniturePart[] {
  return [
    cylinder([0, 0, 0], [0.18, 0.25, height * 0.65], DARK_WOOD),
    sphere([0, height * 0.43, 0], width * 0.42, color),
    sphere([width * 0.12, height * 0.6, 0], width * 0.33, color),
  ];
}

function shrub({ width, height, color }: FurnitureSize): FurniturePart[] {
  return [
    sphere([-width * 0.05, 0, 0], height * 0.45, color),
    sphere([width * 0.05, 0, 0], height * 0.45, color),
  ];
}

function flowerBed({ width, depth, height }: FurnitureSize): FurniturePart[] {
  return [
    box([0, 0, 0], [width, height * 0.35, depth], DARK_WOOD),
    box([0, height * 0.35, 0], [width * 0.85, 0.08, depth * 0.7], '#48764b'),
    ...[-1, 0, 1].map((x) => sphere([x * width * 0.26, height * 0.42, 0], 0.12, '#d79492')),
  ];
}

function herbTub({ width, depth, height }: FurnitureSize): FurniturePart[] {
  return [
    box([0, 0, 0], [width, height * 0.45, depth], DARK_WOOD),
    sphere([0, height * 0.4, 0], width * 0.22, '#528b5d'),
  ];
}

function streetLantern({ height }: FurnitureSize): FurniturePart[] {
  return [
    cylinder([0, 0, 0], [0.12, 0.16, height * 0.82], DARK_STONE),
    box([0, height * 0.8, 0], [0.38, 0.4, 0.38], GLOW),
    box([0, height - 0.08, 0], [0.5, 0.08, 0.5], DARK_STONE),
  ];
}

function wallLantern({ height }: FurnitureSize): FurniturePart[] {
  return [
    box([0, height * 0.65, 0], [0.12, 0.1, 0.4], DARK_STONE),
    box([0, height * 0.68, -0.15], [0.3, 0.35, 0.3], GLOW),
  ];
}

function hitchingRail({ width, height }: FurnitureSize): FurniturePart[] {
  return [
    ...[-1, 1].map((x) => box([x * width * 0.42, 0, 0], [0.12, height, 0.12], DARK_WOOD)),
    box([0, height * 0.72, 0], [width, 0.15, 0.14], DARK_WOOD),
  ];
}

function cart({ width, depth, height, color }: FurnitureSize): FurniturePart[] {
  const bedHeight = height * 0.2;
  const railHeight = height * 0.1;
  return [
    box([0, height * 0.25, 0], [width * 0.82, bedHeight, depth * 0.78], color),
    box([0, bedHeight + height * 0.2, -depth * 0.34], [width * 0.82, railHeight, 0.1], color),
    box([0, bedHeight + height * 0.2, depth * 0.34], [width * 0.82, railHeight, 0.1], color),
    ...[-1, 1].map((z) =>
      cylinder([0, 0.24, z * depth * 0.42], [0.3, 0.3, 0.12], DARK_WOOD, undefined, [
        Math.PI / 2,
        0,
        0,
      ]),
    ),
  ];
}

function trough({ width, depth, height, color }: FurnitureSize): FurniturePart[] {
  const basinHeight = height * 0.72;
  return [
    box([0, 0, 0], [width, basinHeight, depth], color),
    box([0, basinHeight, 0], [width * 0.78, 0.04, depth * 0.65], WATER),
  ];
}

function banner({ width, depth, height, color }: FurnitureSize): FurniturePart[] {
  return [
    cylinder([-width * 0.42, 0, 0], [0.05, 0.05, height], DARK_WOOD),
    box([0, height * 0.42, 0], [width * 0.8, height * 0.5, depth * 0.5], color),
  ];
}

export const FURNITURE_BUILDERS: Record<FurnitureModel, (size: FurnitureSize) => FurniturePart[]> =
  {
    ContainerBarrel: barrel,
    ContainerCrate: crate,
    FurnitureTable: tableOf,
    FurnitureWorkTable: tableOf,
    FurnitureFireplace: fireplace,
    FurnitureStall: stall,
    FurnitureNoticeBoard: noticeBoard,
    FurnitureDisplayShelf: (size) => shelving(size, 3, '#c9a86a'),
    FurnitureTrainingDummy: dummy,
    FurnitureRug: ({ width, depth, height, color }) => [
      box([0, 0, 0], [width, height * 0.75, depth], color),
      box([0, height * 0.75, 0], [width - 0.3, height * 0.25, depth - 0.3], '#d9b87a'),
    ],
    FurnitureCauldron: cauldron,
    FurnitureHerbRack: (size) => shelving(size, 4, '#5f8a6b'),
    FurnitureLectern: lectern,
    FurnitureStaffRack: (size) => shelving(size, 2, '#7a5fa8'),
    FurnitureMannequin: mannequin,
    FurnitureClothShelf: (size) => shelving(size, 4, '#a85f8a'),
    FurnitureDisplayCase: displayCase,
    FurnitureFlourSacks: sacks,
    FurnitureBreadRack: (size) => shelving(size, 3, '#c19a5a'),
    FurnitureLumberStack: (size) => stackOf(size, 3, size.height / 3, size.color),
    FurnitureTimberRack: (size) => shelving(size, 3, '#a58355'),
    FurnitureBookcase: (size) => shelving(size, 4, '#8a6a45'),
    FurnitureChair: (size) => seatOf(size, true),
    FurnitureBench: (size) => seatOf(size, false),
    FurniturePew: (size) => seatOf(size, true),
    FurnitureFountain: fountain,
    FurnitureWell: well,
    FurnitureFirePit: firePit,
    FurnitureStatue: statue,
    FurnitureMonument: monument,
    FurnitureShrine: shrine,
    FurnitureWaystone: waystone,
    FurnitureTree: tree,
    FurnitureShrub: shrub,
    FurnitureFlowerBed: flowerBed,
    FurnitureHerbTub: herbTub,
    FurnitureStreetLantern: streetLantern,
    FurnitureWallLantern: wallLantern,
    FurnitureHitchingRail: hitchingRail,
    FurnitureCart: cart,
    FurnitureTrough: trough,
    FurnitureBanner: banner,
  };

export function isFurnitureModel(model: PropModel): model is FurnitureModel {
  return model in FURNITURE_BUILDERS;
}

export function furnitureParts(model: FurnitureModel, size: FurnitureSize): FurniturePart[] {
  return FURNITURE_BUILDERS[model](size);
}
