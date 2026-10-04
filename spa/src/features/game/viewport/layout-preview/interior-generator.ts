import { BUILDING_SPECS, upperRoomArea, type PreviewBuildingType } from './district-catalog';
import { seededRandom, type Building, type Rect } from './district-generator';

export type PreviewRoom = Rect & { name: string; color: string };
export type Interior = {
  buildingType: PreviewBuildingType;
  width: number;
  depth: number;
  rooms: PreviewRoom[];
  hall: Rect;
  stairs: Rect;
};

const STAIRS_WIDTH = 1.3;
const STAIRS_DEPTH = 2.3;
const ROOM_ASPECT = 1.25;

export function floorNames(building: Building, seed: number): string[][] {
  if (building.type !== 'House') return BUILDING_SPECS[building.type].floors;
  const count = 1 + Math.floor(seededRandom(seed + building.id * 7919)() * 4);
  return [['Living Room'], Array.from({ length: count }, (_, index) => `Bedroom ${index + 1}`)];
}

export function generateInterior(building: Building, seed: number, floor = 0): Interior {
  const names = floorNames(building, seed)[floor] ?? floorNames(building, seed)[0];
  const sized = floor > 0 && names.every((name) => upperRoomArea(name) !== undefined);
  return sized ? hallwayFloor(building, names) : openFloor(building, names[0]);
}

function openFloor(building: Building, name: string): Interior {
  const { frontage: width, length: depth } = building;
  return {
    buildingType: building.type,
    width,
    depth,
    hall: { x: 0, y: 0, width: 0, depth: 0 },
    rooms: [{ x: 0, y: 0, width, depth, name, color: building.color }],
    stairs: stairsRect(width, depth),
  };
}

function stairsRect(width: number, depth: number): Rect {
  return {
    x: (width - STAIRS_WIDTH) / 2,
    y: depth - STAIRS_DEPTH - 0.2,
    width: STAIRS_WIDTH,
    depth: STAIRS_DEPTH,
  };
}

function hallwayFloor(building: Building, names: string[]): Interior {
  const { frontage: width, length: depth } = building;
  const hallWidth = Math.min(2.4, width * 0.22);
  const sideWidth = (width - hallWidth) / 2;
  const hall = { x: sideWidth, y: 0, width: hallWidth, depth };
  const areas = names.map((name) => upperRoomArea(name) ?? 0);
  const depths = roomDepths(areas, sideWidth, depth);
  const gaps = sideGaps(depths, depth);
  const cursors = [gaps[0], gaps[1]];
  const rooms = names.map((name, index) => {
    const side = index % 2;
    const roomWidth = Math.min(sideWidth, areas[index] / depths[index]);
    const room = {
      name,
      color: building.color,
      x: side ? hall.x + hallWidth : hall.x - roomWidth,
      y: cursors[side],
      width: roomWidth,
      depth: depths[index],
    };
    cursors[side] += depths[index] + gaps[side];
    return room;
  });
  return {
    buildingType: building.type,
    width,
    depth,
    hall,
    rooms,
    stairs: stairsRect(width, depth),
  };
}

function roomDepths(areas: number[], sideWidth: number, depth: number): number[] {
  const roomWidth = Math.min(sideWidth, ...areas.map((area) => Math.sqrt(area * ROOM_ASPECT)));
  const depths = areas.map((area) => area / roomWidth);
  const perSide = [0, 1].map((side) =>
    depths.filter((_, index) => index % 2 === side).reduce((sum, value) => sum + value, 0),
  );
  const scale = Math.min(1, depth / Math.max(...perSide));
  return depths.map((value) => value * scale);
}

function sideGaps(depths: number[], depth: number): number[] {
  return [0, 1].map((side) => {
    const sideDepths = depths.filter((_, index) => index % 2 === side);
    const used = sideDepths.reduce((sum, value) => sum + value, 0);
    return Math.max(0, (depth - used) / (sideDepths.length + 1));
  });
}
