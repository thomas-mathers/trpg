import {
  FURNITURE as F,
  SOUTH_STOCK,
  workshopStation,
  type Furnishing,
} from './furnishing-catalog';
import type { Interior, PreviewRoom } from './interior-generator';
import type { Rotation } from './preview-population';

export type Placement = { spec: Furnishing; x: number; y: number; rotation: Rotation };
type Room = Pick<PreviewRoom, 'width' | 'depth'>;
type Wall = 'north' | 'east' | 'south' | 'west';
type Run = { from?: number; to?: number; count?: number };

const MARGIN = 0.2;
const CHAIR_GAP = 0.15;
const WALL_ROTATION: Record<Wall, Rotation> = { north: 0, east: 90, south: 180, west: 270 };

export function footprint({ width, depth }: Furnishing, rotation: Rotation) {
  return rotation === 90 || rotation === 270 ? { width: depth, depth: width } : { width, depth };
}

export function roomPlan(room: PreviewRoom, interior: Interior): Placement[] {
  const { name } = room;
  if (/Dormitory/.test(name)) return dormitoryPlan(room);
  if (/Bedroom|Guest Room|Quarters|Chamber|Member Room/.test(name))
    return bedroomPlan(room, room.x > interior.hall.x);
  if (name === 'Sanctuary') return templePlan(room);
  if (name === 'Reading Room' || name === 'Study') return libraryPlan(room);
  if (name === 'Cells') return cellsPlan(room);
  if (name === 'Drill Hall') return drillHallPlan(room);
  if (name === 'Stable') return stablePlan(room);
  if (name === 'Great Hall') return greatHallPlan(room);
  if (name === 'Common Room') return commonRoomPlan(room);
  if (name === 'Living Room') return livingRoomPlan(room);
  if (name === 'Hall') return guildHallPlan(room);
  if (name === 'Lobby') return lobbyPlan(room);
  if (name === 'Guard Station') return guardStationPlan(room);
  return shopPlan(room, interior);
}

function inRoom(
  room: Room,
  spec: Furnishing,
  fx: number,
  fy: number,
  rotation: Rotation = 0,
): Placement {
  const size = footprint(spec, rotation);
  return {
    spec,
    rotation,
    x: MARGIN + fx * (room.width - size.width - 2 * MARGIN),
    y: MARGIN + fy * (room.depth - size.depth - 2 * MARGIN),
  };
}

function around(spec: Furnishing, cx: number, cy: number, rotation: Rotation = 0): Placement {
  const size = footprint(spec, rotation);
  return { spec, rotation, x: cx - size.width / 2, y: cy - size.depth / 2 };
}

function wallRun(room: Room, spec: Furnishing, wall: Wall, run: Run = {}): Placement[] {
  const { from = 0.05, to = 0.95 } = run;
  const rotation = WALL_ROTATION[wall];
  const size = footprint(spec, rotation);
  const horizontal = wall === 'north' || wall === 'south';
  const length = horizontal ? room.width : room.depth;
  const along = horizontal ? size.width : size.depth;
  const count = run.count ?? Math.max(1, Math.floor(((to - from) * length) / (along + 0.15)));
  return Array.from({ length: count }, (_, index) => {
    const t = count === 1 ? (from + to) / 2 : from + ((to - from) * index) / (count - 1);
    const [fx, fy] = {
      north: [t, 0],
      south: [t, 1],
      west: [0, t],
      east: [1, t],
    }[wall];
    return inRoom(room, spec, fx, fy, rotation);
  });
}

function tableSet(cx: number, cy: number): Placement[] {
  const offset = F.table.depth / 2 + CHAIR_GAP + F.chair.depth / 2;
  return [
    around(F.table, cx, cy),
    around(F.chair, cx, cy - offset, 0),
    around(F.chair, cx, cy + offset, 180),
  ];
}

function gridCenters(room: Room, cell: number, top: number, bottom: number, side = 1.5) {
  const cols = Math.max(1, Math.floor((room.width - 2 * side) / cell));
  const rows = Math.max(1, Math.floor((room.depth - top - bottom) / 4));
  return Array.from({ length: cols * rows }, (_, index) => {
    const col = index % cols;
    const row = Math.floor(index / cols);
    return {
      x: side + ((col + 0.5) * (room.width - 2 * side)) / cols,
      y: top + ((row + 0.5) * (room.depth - top - bottom)) / rows,
    };
  });
}

function tableGrid(room: Room, top: number, bottom: number): Placement[] {
  return gridCenters(room, 5.5, top, bottom).flatMap(({ x, y }) => tableSet(x, y));
}

function rugAt(cx: number, cy: number, width: number, depth: number): Placement {
  return around({ ...F.rug, width, depth }, cx, cy);
}

function hearthRug(room: Room, side: 'west' | 'east'): Placement {
  return rugAt(side === 'east' ? room.width - 1.9 : 1.9, room.depth / 2, 1.6, 2.4);
}

function bedroomPlan(room: Room, hallOnLeft: boolean): Placement[] {
  const side = hallOnLeft ? 1 : 0;
  return [
    rugAt(room.width / 2, room.depth * 0.6, 1.5, 1.5),
    inRoom(room, F.bed, side, 0),
    inRoom(room, F.chest, side, 1),
    inRoom(room, F.chair, 1 - side, 0),
  ];
}

function dormitoryPlan(room: Room): Placement[] {
  return [0, 0.35, 0.7]
    .flatMap((y) => [inRoom(room, F.bed, 0, y), inRoom(room, F.bed, 1, y)])
    .concat([inRoom(room, F.chest, 0, 1), inRoom(room, F.chest, 1, 1)]);
}

function cellsPlan(room: Room): Placement[] {
  return [inRoom(room, F.chest, 0, 0), ...[0, 0.35, 0.7].map((y) => inRoom(room, F.cell, 1, y))];
}

function lobbyPlan(room: Room): Placement[] {
  return [
    inRoom(room, { ...F.counter, name: "Innkeeper's counter" }, 0.95, 0),
    inRoom(room, F.hearth, 1, 0.5, 90),
    hearthRug(room, 'east'),
    ...wallRun(room, F.bench, 'west', { from: 0.15, to: 0.8 }),
    ...wallRun(room, F.chest, 'south', { from: 0.05, to: 0.3 }),
    ...tableGrid(room, 4, 5),
  ];
}

function commonRoomPlan(room: Room): Placement[] {
  return [
    inRoom(room, F.hearth, 0, 0.5, 270),
    hearthRug(room, 'west'),
    ...wallRun(room, { ...F.counter, name: 'Bar counter' }, 'east', { from: 0.05, to: 0.6 }),
    ...wallRun(room, F.barrel, 'south', { from: 0.65, to: 0.95 }),
    ...tableGrid(room, 3.5, 4.5),
  ];
}

function livingRoomPlan(room: Room): Placement[] {
  return [
    inRoom(room, F.hearth, 0, 0.5, 270),
    hearthRug(room, 'west'),
    inRoom(room, F.bookcase, 0, 0.1, 270),
    inRoom(room, F.bench, 1, 0.55, 90),
    inRoom(room, F.chest, 0, 1),
    ...tableSet(room.width / 2, room.depth * 0.42),
  ];
}

function guildHallPlan(room: Room): Placement[] {
  const desk = { ...F.counter, name: 'Reception desk' };
  return [
    inRoom(room, desk, 0.05, 0),
    inRoom(room, desk, 0.95, 0),
    ...wallRun(room, F.board, 'west', { from: 0.2, to: 0.8, count: 3 }),
    ...wallRun(room, F.bench, 'east', { from: 0.2, to: 0.8, count: 3 }),
    ...tableGrid(room, 4, 5),
  ];
}

function runner(room: Room, width: number, top: number, bottom: number): Placement {
  return rugAt(room.width / 2, (top + bottom) / 2, width, bottom - top);
}

function greatHallPlan(room: Room): Placement[] {
  return [
    runner(room, 2.4, 2.8, room.depth - 5.3),
    around(F.throne, room.width / 2, room.depth - 4.6, 180),
    ...wallRun(room, F.hearth, 'west', { from: 0.25, to: 0.75, count: 2 }),
    ...wallRun(room, F.hearth, 'east', { from: 0.25, to: 0.75, count: 2 }),
    ...tableGrid(room, 5, 8),
  ];
}

function guardStationPlan(room: Room): Placement[] {
  return [
    inRoom(room, { ...F.counter, name: 'Duty desk' }, 0.95, 0),
    around(F.chair, room.width - 1.1, 1.4, 180),
    ...wallRun(room, F.rack, 'west', { from: 0.2, to: 0.7, count: 3 }),
    ...wallRun(room, F.bench, 'east', { from: 0.3, to: 0.7, count: 2 }),
    inRoom(room, F.chest, 0, 1),
    ...tableSet(room.width / 2, room.depth * 0.45),
  ];
}

function drillHallPlan(room: Room): Placement[] {
  return [
    ...wallRun(room, F.rack, 'west', { from: 0.1, to: 0.9, count: 4 }),
    ...wallRun(room, F.rack, 'east', { from: 0.1, to: 0.9, count: 4 }),
    ...wallRun(room, F.bench, 'north', { from: 0.05, to: 0.95, count: 4 }),
    ...[0.3, 0.5, 0.7].map((fx) => inRoom(room, F.dummy, fx, 0.45)),
    inRoom(room, F.chest, 0, 1),
  ];
}

function stablePlan(room: Room): Placement[] {
  return [
    inRoom(room, { ...F.counter, name: 'Tack counter' }, 0.75, 0),
    ...wallRun(room, F.stall, 'west', { from: 0.1, to: 0.95 }),
    ...wallRun(room, F.stall, 'east', { from: 0.1, to: 0.95 }),
    ...wallRun(room, F.barrel, 'south', { from: 0.1, to: 0.3 }),
    ...wallRun(room, { ...F.crate, name: 'Hay bale' }, 'south', { from: 0.7, to: 0.9 }),
  ];
}

function templePlan(room: Room): Placement[] {
  const rows = Math.max(0, Math.floor((room.depth - 12) / 1.8));
  const offsets = [2.2, 4.4, 6.6].filter((offset) => offset < room.width / 2 - 1.5);
  const pews = offsets.flatMap((offset) =>
    Array.from({ length: rows }, (_, row) =>
      [-1, 1].map((side) => around(F.pew, room.width / 2 + side * offset, 4.5 + row * 1.8)),
    ).flat(),
  );
  return [
    runner(room, 1.4, 2.8, room.depth - 5),
    around(F.altar, room.width / 2, room.depth - 4.2),
    ...pews,
  ];
}

function libraryPlan(room: Room): Placement[] {
  const stacked = room.width > 14;
  return [
    ...(['west', 'east'] as const).flatMap((wall) => wallRun(room, F.bookcase, wall)),
    ...[0.05, 0.65].flatMap((from) => wallRun(room, F.bookcase, 'north', { from, to: from + 0.3 })),
    ...(stacked ? [0.3, 0.7] : []).flatMap((fx) => bookStack(room, room.width * fx)),
    ...readingTables(room, stacked ? [0.15, 0.5, 0.85] : room.width > 9 ? [0.3, 0.7] : [0.5]),
  ];
}

function readingTables(room: Room, columns: number[]): Placement[] {
  const spacing = 3.6;
  const first = 4.5;
  const rows = Math.max(1, Math.floor((room.depth - 5.2 - first) / spacing) + 1);
  return columns.flatMap((fx) =>
    Array.from({ length: rows }, (_, row) =>
      tableSet(room.width * fx, rows === 1 ? room.depth * 0.4 : first + row * spacing),
    ).flat(),
  );
}

function bookStack(room: Room, cx: number): Placement[] {
  const count = Math.max(1, Math.floor((room.depth * 0.5) / 1.15));
  return Array.from({ length: count }, (_, index) => {
    const cy = room.depth * 0.25 + (room.depth * 0.5 * (index + 0.5)) / count;
    return [around(F.bookcase, cx - 0.4, cy, 90), around(F.bookcase, cx + 0.4, cy, 270)];
  }).flat();
}

function shopPlan(room: Room, interior: Interior): Placement[] {
  const type = interior.buildingType;
  return [
    inRoom(room, workshopStation(type), 0, 1),
    inRoom(room, F.counter, 0.75, 0.3),
    inRoom(room, F.chest, 1, 0),
    ...tradeExtras(room, type),
    ...wallRun(room, F.shelf, 'west', { from: 0.05, to: 0.8 }),
    ...wallRun(room, F.shelf, 'east', { from: 0.35, to: 0.95 }),
  ];
}

function tradeExtras(room: Room, type: Interior['buildingType']): Placement[] {
  if (type === 'GeneralGoods')
    return [
      ...wallRun(room, F.crate, 'south', { from: 0.05, to: 0.3 }),
      ...wallRun(room, F.barrel, 'south', { from: 0.7, to: 0.95 }),
    ];
  if (type === 'Blacksmith')
    return [
      inRoom(room, { name: 'Anvil', width: 1.4, depth: 1.2, shape: 'anvil' }, 0.25, 0.75),
      ...wallRun(room, F.rack, 'east', { from: 0.3, to: 0.7, count: 2 }),
    ];
  if (type === 'Carpenter' || type === 'Bakery')
    return [
      ...wallRun(room, F.workTable, 'east', { from: 0.5, to: 0.95 }),
      ...southStock(room, type),
    ];
  return southStock(room, type);
}

function southStock(room: Room, type: Interior['buildingType']): Placement[] {
  const stock = SOUTH_STOCK[type];
  if (!stock) return [];
  const { left, right } = stock;
  const south = (spec: Furnishing, cx: number) =>
    around(spec, cx, room.depth - MARGIN - spec.depth / 2, 180);
  const rightSlots = [0, 1, 2].map((slot) =>
    south(right, room.width - MARGIN - right.width / 2 - slot * (right.width + 0.15)),
  );
  return [...(left ? [south(left, 2 + left.width / 2)] : []), ...rightSlots];
}
