import type { District, Rect } from './district-generator';
import { FURNITURE as F, SQUARE_FEATURES } from './furnishing-catalog';
import { intersects, type ScaleProp } from './preview-population';
import { footprint } from './room-plans';

const SQUARE_INSET = 1;
const BENCH_FRACTIONS = [0.2, 0.5, 0.8];

function centerpiece(square: Rect, squareName: string): ScaleProp[] {
  const spec = SQUARE_FEATURES[squareName];
  if (!spec) return [];
  return [
    {
      ...spec,
      x: square.x + (square.width - spec.width) / 2,
      y: square.y + (square.depth - spec.depth) / 2,
    },
  ];
}

function squareFurniture(square: Rect): ScaleProp[] {
  const bench = (side: 'west' | 'east', fraction: number): ScaleProp => {
    const rotation = side === 'west' ? 270 : 90;
    const size = footprint(F.bench, rotation);
    return {
      ...F.bench,
      ...size,
      rotation,
      x:
        side === 'west'
          ? square.x + SQUARE_INSET
          : square.x + square.width - SQUARE_INSET - size.width,
      y: square.y + fraction * square.depth - size.depth / 2,
    };
  };
  const board = {
    ...F.board,
    x: square.x + (square.width - F.board.width) / 2,
    y: square.y + SQUARE_INSET,
  };
  return [
    board,
    ...(['west', 'east'] as const).flatMap((side) =>
      BENCH_FRACTIONS.map((fraction) => bench(side, fraction)),
    ),
  ];
}

export function furnishOutdoors(district: District, blocked: Rect[]): ScaleProp[] {
  const candidates: ScaleProp[] = [];
  const square =
    district.square.width > 0
      ? district.square
      : { x: 2, y: 2, width: district.width - 4, depth: district.depth - 4 };
  candidates.push(...centerpiece(square, district.squareName), ...squareFurniture(square));
  district.buildings.forEach((building) => {
    if (
      !['GeneralGoods', 'Bakery', 'Carpenter', 'Blacksmith', 'Stable', 'Inn', 'Tavern'].includes(
        building.type,
      )
    )
      return;
    [F.crate, F.barrel].forEach((spec, index) => {
      const along = -building.frontage / 2 + 1 + index * 1.25;
      const distance = building.length / 2 + 0.9;
      const angle = (building.facing * Math.PI) / 2;
      const x =
        building.x + building.width / 2 + along * Math.cos(angle) + distance * Math.sin(angle);
      const y =
        building.y + building.depth / 2 + along * Math.sin(angle) - distance * Math.cos(angle);
      candidates.push({ ...spec, x: x - spec.width / 2, y: y - spec.depth / 2 });
    });
  });
  const placed: ScaleProp[] = [];
  candidates.forEach((prop) => {
    if (
      prop.x < 0 ||
      prop.y < 0 ||
      prop.x + prop.width > district.width ||
      prop.y + prop.depth > district.depth
    )
      return;
    if (![...blocked, ...placed].some((box) => intersects(prop, box, 0.35))) placed.push(prop);
  });
  return placed;
}
