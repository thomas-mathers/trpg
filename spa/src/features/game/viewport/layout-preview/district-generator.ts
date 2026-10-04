import { type DistrictKind, type PreviewBuildingType } from './district-catalog';
import { generatePrecinct } from './precinct-generator';

export type Rect = { x: number; y: number; width: number; depth: number };
export type Building = Rect & {
  id: number;
  type: PreviewBuildingType;
  name: string;
  color: string;
  height: number;
  facing: number;
  frontage: number;
  length: number;
};
export type District = {
  width: number;
  depth: number;
  buildings: Building[];
  courts: Rect[];
  streets: Rect[];
  square: Rect;
  squareName: string;
};
export type LayoutOptions = {
  seed: number;
  blocks: number;
  alley: number;
  scale: number;
  kind?: DistrictKind;
  guildHall?: boolean;
};
type Random = () => number;

export function seededRandom(seed: number): Random {
  let state = seed | 0;
  return () => {
    state = (Math.imul(state, 1664525) + 1013904223) | 0;
    return (state >>> 0) / 4294967296;
  };
}

function divisions(count: number, random: Random, scale: number) {
  return Array.from({ length: count }, () => Math.round((42 + random() * 20) * scale));
}

export function generateDistrict(options: LayoutOptions): District {
  if (options.kind && options.kind !== 'Residential')
    return generatePrecinct(options, options.kind);
  return generateResidential(options);
}

function generateResidential({ seed, blocks, alley, scale }: LayoutOptions): District {
  const random = seededRandom(seed);
  const widths = divisions(blocks, random, scale);
  const depths = divisions(blocks, random, scale);
  const street = 7;
  const width = widths.reduce((sum, value) => sum + value + street, street);
  const depth = depths.reduce((sum, value) => sum + value + street, street);
  const district: District = {
    width,
    depth,
    buildings: [],
    courts: [],
    streets: [],
    square: { x: 0, y: 0, width: 0, depth: 0 },
    squareName: 'Shared green',
  };
  const squareIndex = Math.floor(random() * blocks * blocks);
  let y = street;
  depths.forEach((blockDepth, row) => {
    let x = street;
    widths.forEach((blockWidth, column) => {
      const rect = { x, y, width: blockWidth, depth: blockDepth };
      if (row * blocks + column === squareIndex) district.square = rect;
      else fillBlock(district, rect, alley, scale, random);
      x += blockWidth + street;
    });
    district.streets.push({ x: 0, y: y - street, width, depth: street });
    y += blockDepth + street;
  });
  district.streets.push({ x: 0, y: depth - street, width, depth: street });
  let x = 0;
  [...widths, 0].forEach((blockWidth) => {
    district.streets.push({ x, y: 0, width: street, depth });
    x += blockWidth + street;
  });
  return district;
}

function fillBlock(district: District, block: Rect, alley: number, scale: number, random: Random) {
  const band = Math.min(block.width, block.depth) * (0.23 + random() * 0.04);
  const { x, y, width, depth } = block;
  district.courts.push({
    x: x + band,
    y: y + band,
    width: width - band * 2,
    depth: depth - band * 2,
  });
  fillFrontage(district, { x, y, width, depth: band }, 0, alley, scale, random);
  fillFrontage(district, { x, y: y + depth - band, width, depth: band }, 2, alley, scale, random);
  const middle = depth - 2 * band - 2 * alley;
  fillFrontage(
    district,
    { x, y: y + band + alley, width: band, depth: middle },
    3,
    alley,
    scale,
    random,
  );
  fillFrontage(
    district,
    { x: x + width - band, y: y + band + alley, width: band, depth: middle },
    1,
    alley,
    scale,
    random,
  );
}

function fillFrontage(
  district: District,
  strip: Rect,
  facing: number,
  alley: number,
  scale: number,
  random: Random,
) {
  const vertical = facing % 2 === 1;
  const available = vertical ? strip.depth : strip.width;
  const count = Math.max(1, Math.floor((available + alley) / (10 * scale + alley)));
  const weights = Array.from({ length: count }, () => 0.8 + random() * 0.5);
  const total = weights.reduce((sum, value) => sum + value, 0);
  let cursor = 0;
  weights.forEach((weight) => {
    const frontage = ((available - (count - 1) * alley) * weight) / total;
    const length = (vertical ? strip.width : strip.depth) * (0.78 + random() * 0.22);
    const type = { name: 'House', color: '#d4b990', height: 7.5 };
    const width = vertical ? length : frontage;
    const depth = vertical ? frontage : length;
    district.buildings.push({
      ...type,
      type: 'House',
      id: district.buildings.length + 1,
      facing,
      frontage,
      length,
      height: type.height * (0.85 + random() * 0.3),
      width,
      depth,
      x: strip.x + (vertical ? (facing === 1 ? strip.width - width : 0) : cursor),
      y: strip.y + (vertical ? cursor : facing === 2 ? strip.depth - depth : 0),
    });
    cursor += frontage + alley;
  });
}

export function doorOf({ x, y, width, depth, facing }: Building) {
  return [
    { x: x + width / 2, y },
    { x: x + width, y: y + depth / 2 },
    { x: x + width / 2, y: y + depth },
    { x, y: y + depth / 2 },
  ][facing];
}

export function rowComparison(district: District): District {
  const buildings: Building[] = [];
  const cursors = [7, 7];
  const maxDepth = Math.max(6, ...district.buildings.map(({ length }) => length));
  const center = maxDepth + 7;
  district.buildings.forEach((building, index) => {
    const side = index % 2;
    buildings.push({
      ...building,
      x: cursors[side],
      y: side ? center + 3 : center - 3 - building.length,
      width: building.frontage,
      depth: building.length,
      facing: side ? 0 : 2,
    });
    cursors[side] += building.frontage + 3;
  });
  const width = Math.max(...cursors) + 4;
  return {
    width,
    depth: 2 * center,
    buildings,
    courts: [],
    square: { x: 0, y: 0, width: 0, depth: 0 },
    squareName: '',
    streets: [{ x: 0, y: center - 3, width, depth: 6 }],
  };
}
