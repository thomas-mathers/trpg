import {
  BUILDING_SPECS,
  ROSTERS,
  type DistrictKind,
  type PreviewBuildingType,
} from './district-catalog';
import {
  seededRandom,
  type Building,
  type District,
  type LayoutOptions,
} from './district-generator';

const SQUARE_NAMES: Record<DistrictKind, string> = {
  Residential: 'Shared green',
  CityCenter: 'Market square',
  Encampment: 'Muster yard',
  Scientific: 'Scholars’ court',
  Governmental: 'Castle forecourt',
  HolySite: 'Temple court',
  CityEntrance: 'Arrival square',
};

export function generatePrecinct(
  options: LayoutOptions,
  kind: Exclude<DistrictKind, 'Residential'>,
): District {
  const random = seededRandom(options.seed);
  const buildings = ROSTERS[kind]
    .filter((type) => type !== 'GuildHall' || options.guildHall !== false)
    .map((type, index) => sizeBuilding(type, index + 1, options.scale, random));
  const topCount =
    kind === 'CityCenter'
      ? buildings.filter((b) => ['GuildHall', 'Inn', 'Tavern'].includes(b.type)).length
      : 1;
  const north = buildings.slice(0, topCount);
  const west = buildings.slice(topCount).filter((_, index) => index % 2 === 0);
  const east = buildings.slice(topCount).filter((_, index) => index % 2 === 1);
  return arrangePrecinct(north, west, east, options, SQUARE_NAMES[kind]);
}

function sizeBuilding(
  type: PreviewBuildingType,
  id: number,
  scale: number,
  random: () => number,
): Building {
  const spec = BUILDING_SPECS[type];
  const frontage = Math.round(spec.width * scale * (0.88 + random() * 0.24));
  const length = Math.round(spec.depth * scale * (0.88 + random() * 0.24));
  return {
    id,
    type,
    name: spec.name,
    color: spec.color,
    height: spec.floors.length * 3.5,
    frontage,
    length,
    width: frontage,
    depth: length,
    x: 0,
    y: 0,
    facing: 2,
  };
}

function extent(buildings: Building[], gap: number) {
  return (
    buildings.reduce((sum, b) => sum + b.frontage, 0) + Math.max(0, buildings.length - 1) * gap
  );
}

function arrangePrecinct(
  north: Building[],
  west: Building[],
  east: Building[],
  options: LayoutOptions,
  squareName: string,
): District {
  const gap = options.alley + 2;
  const margin = 7;
  const sideDepth = Math.max(8, ...west.concat(east).map((b) => b.length));
  const topDepth = Math.max(0, ...north.map((b) => b.length));
  const courtWidth = Math.max(28 * options.scale, extent(north, gap) + 8);
  const courtDepth = Math.max(26 * options.scale, extent(west, gap) + 8, extent(east, gap) + 8);
  const square = {
    x: margin + sideDepth,
    y: margin + topDepth,
    width: courtWidth,
    depth: courtDepth,
  };
  placeNorth(north, square.x, square.y, courtWidth, gap);
  placeFlank(west, square.x, square.y, courtDepth, gap, 1);
  placeFlank(east, square.x + courtWidth, square.y, courtDepth, gap, 3);
  const width = courtWidth + 2 * (margin + sideDepth);
  const depth = square.y + courtDepth + 14;
  const avenue = {
    x: square.x + courtWidth / 2 - 4,
    y: square.y + courtDepth,
    width: 8,
    depth: 14,
  };
  const courts = [
    { x: margin, y: square.y, width: sideDepth, depth: courtDepth },
    { x: square.x + courtWidth, y: square.y, width: sideDepth, depth: courtDepth },
  ];
  return {
    width,
    depth,
    buildings: [...north, ...west, ...east],
    courts,
    square,
    squareName,
    streets: [square, avenue],
  };
}

function placeNorth(buildings: Building[], x: number, y: number, width: number, gap: number) {
  let cursor = x + (width - extent(buildings, gap)) / 2;
  buildings.forEach((building) => {
    building.x = cursor;
    building.y = y - building.length;
    cursor += building.frontage + gap;
  });
}

function placeFlank(
  buildings: Building[],
  x: number,
  y: number,
  depth: number,
  gap: number,
  facing: number,
) {
  let cursor = y + (depth - extent(buildings, gap)) / 2;
  buildings.forEach((building) => {
    Object.assign(building, {
      x: facing === 1 ? x - building.length : x,
      y: cursor,
      width: building.length,
      depth: building.frontage,
      facing,
    });
    cursor += building.frontage + gap;
  });
}
