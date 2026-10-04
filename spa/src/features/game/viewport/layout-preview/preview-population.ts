import { doorOf, seededRandom, type District, type Rect } from './district-generator';
import type { Interior } from './interior-generator';
import { furnishOutdoors } from './outdoor-furnisher';
import { furnishRoom } from './room-furnisher';

// Degrees clockwise from the unrotated glyph, whose back sits on the north edge.
export type Rotation = 0 | 90 | 180 | 270;
export type ScaleProp = Rect & {
  name: string;
  rotation?: Rotation;
  shape:
    | 'bed'
    | 'barrel'
    | 'seat'
    | 'box'
    | 'table'
    | 'shelf'
    | 'hearth'
    | 'altar'
    | 'alchemy'
    | 'anvil'
    | 'cell'
    | 'rug'
    | 'fountain'
    | 'pillar'
    | 'fire';
};
export const isSolid = ({ shape }: ScaleProp) => shape !== 'rug';
export type ScaleCreature = { x: number; y: number; angle: number };
export type Population = { props: ScaleProp[]; creatures: ScaleCreature[] };
export function intersects(a: Rect, b: Rect, margin = 0) {
  return (
    a.x < b.x + b.width + margin &&
    a.x + a.width + margin > b.x &&
    a.y < b.y + b.depth + margin &&
    a.y + a.depth + margin > b.y
  );
}

export function districtPopulation(district: District, seed: number): Population {
  const random = seededRandom(seed + 1009);
  const doors = district.buildings.map((building) => {
    const door = doorOf(building);
    return { x: door.x - 1.5, y: door.y - 1.5, width: 3, depth: 3 };
  });
  const blocked = [...district.buildings, ...doors];
  const zones = [...district.streets, ...district.courts].filter(
    (zone) => zone.width > 3 && zone.depth > 3,
  );
  const props = furnishOutdoors(district, blocked);
  const count = Math.min(60, Math.max(8, district.buildings.length * 2));
  return { props, creatures: placeCreatures(count, zones, [...blocked, ...props], random) };
}

export function interiorPopulation(interior: Interior, seed: number): Population {
  const random = seededRandom(seed + 2027);
  const { width, depth, rooms, hall } = interior;
  const fixtures = fixtureClearances(interior);
  const blocked = [...fixtures, ...doorClearances(interior)];
  const count = Math.min(16, Math.max(2, Math.round((width * depth) / 35)));
  const props: ScaleProp[] = [];
  rooms.forEach((room) => props.push(...furnishRoom(room, interior, [...blocked, hall, ...props])));
  const creatures = placeCreatures(
    count,
    [...rooms, hall],
    [...fixtures, ...props.filter(isSolid)],
    random,
  );
  return { props, creatures };
}

function fixtureClearances({ width, stairs }: Interior): Rect[] {
  return [
    { x: width / 2 - 1, y: 0, width: 2, depth: 2.5 },
    {
      x: stairs.x - 0.45,
      y: stairs.y - 0.45,
      width: stairs.width + 0.9,
      depth: stairs.depth + 0.9,
    },
  ];
}

function doorClearances({ rooms, hall }: Interior): Rect[] {
  if (hall.width === 0) return [];
  return rooms.map((room, index) => ({
    x: (index % 2 ? room.x : room.x + room.width) - 1,
    y: room.y + room.depth / 2 - 0.8,
    width: 2,
    depth: 1.6,
  }));
}

function placeCreatures(
  count: number,
  zones: Rect[],
  blocked: Rect[],
  random: () => number,
): ScaleCreature[] {
  const occupied = [...blocked];
  const creatures: ScaleCreature[] = [];
  for (let i = 0; i < count; i++) {
    const box = findSpace({ width: 0.6, depth: 0.6 }, zones, occupied, random);
    if (!box) continue;
    occupied.push(box);
    creatures.push({ x: box.x + 0.3, y: box.y + 0.3, angle: random() * 360 });
  }
  return creatures;
}

function findSpace(
  size: Pick<Rect, 'width' | 'depth'>,
  zones: Rect[],
  blocked: Rect[],
  random: () => number,
): Rect | undefined {
  const candidates = zones.filter(
    (zone) => zone.width > size.width + 1 && zone.depth > size.depth + 1,
  );
  if (!candidates.length) return undefined;
  for (let attempt = 0; attempt < 200; attempt++) {
    const zone = candidates[Math.floor(random() * candidates.length)];
    const rect = {
      ...size,
      x: zone.x + 0.5 + random() * (zone.width - size.width - 1),
      y: zone.y + 0.5 + random() * (zone.depth - size.depth - 1),
    };
    if (
      !blocked.some(
        (obstacle) => obstacle.width > 0 && obstacle.depth > 0 && intersects(rect, obstacle, 0.35),
      )
    )
      return rect;
  }
  return undefined;
}
