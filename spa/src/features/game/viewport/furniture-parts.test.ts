import { describe, expect, it } from 'vitest';

import { FURNITURE_BUILDERS, type FurnitureModel, type FurniturePart } from './furniture-parts';
import { PROP_STYLES } from './model-styles';

const FOOTPRINTS: Record<FurnitureModel, [number, number]> = {
  FurnitureTable: [1.8, 1],
  FurnitureWorkTable: [2.4, 1.2],
  FurnitureFireplace: [1.4, 0.8],
  FurnitureStall: [2.4, 2.4],
  FurnitureNoticeBoard: [2, 0.25],
  FurnitureDisplayShelf: [1.2, 0.5],
  FurnitureTrainingDummy: [0.7, 0.7],
  FurnitureRug: [1.6, 2.4],
  FurnitureCauldron: [0.9, 0.9],
  FurnitureHerbRack: [1.4, 0.4],
  FurnitureLectern: [0.7, 0.6],
  FurnitureStaffRack: [1.2, 0.4],
  FurnitureMannequin: [0.5, 0.5],
  FurnitureClothShelf: [1.4, 0.5],
  FurnitureDisplayCase: [1.4, 0.6],
  FurnitureFlourSacks: [0.8, 0.6],
  FurnitureBreadRack: [1.4, 0.5],
  FurnitureLumberStack: [1.8, 0.7],
  FurnitureTimberRack: [1.4, 0.4],
  FurnitureBookcase: [1, 0.7],
  FurnitureChair: [0.5, 0.5],
  FurnitureBench: [1.5, 0.5],
  FurniturePew: [2, 0.6],
  FurnitureFountain: [3.2, 3.2],
  FurnitureWell: [1.4, 1.4],
  FurnitureFirePit: [1.6, 1.6],
  FurnitureStatue: [1.2, 1.2],
  FurnitureMonument: [1.8, 1.8],
  FurnitureShrine: [1.4, 1.4],
  FurnitureWaystone: [1.2, 1.2],
};

const TOLERANCE = 1e-6;

function extent({ shape, size }: FurniturePart): [number, number, number] {
  const [first, second, third] = size;
  if (shape === 'box') return [first / 2, second / 2, third / 2];
  if (shape === 'cylinder') return [Math.max(first, second), third / 2, Math.max(first, second)];
  return [first, first, first];
}

const MODELS = Object.keys(FURNITURE_BUILDERS) as FurnitureModel[];

describe('furniture parts', () => {
  it('leaves the fireplace opening clear and puts the fire in front of its back panel', () => {
    const model = 'FurnitureFireplace';
    const [width, depth] = FOOTPRINTS[model];
    const { height, color } = PROP_STYLES[model];
    const parts = FURNITURE_BUILDERS[model]({ width, depth, height, color });
    const middleOfOpening = [0, height * 0.2, -depth / 2 + 0.1];
    const stoneAtOpening = parts
      .filter((part) => part.color === color)
      .some(({ position, size }) =>
        position.every(
          (coordinate, axis) => Math.abs(coordinate - middleOfOpening[axis]) < size[axis] / 2,
        ),
      );
    const back = parts.find((part) => part.color === '#2e2a26')!;
    const fire = parts.find((part) => part.color === '#e19c51')!;

    expect(stoneAtOpening).toBe(false);
    expect(fire.position[2] + fire.size[2] / 2).toBeLessThan(back.position[2] - back.size[2] / 2);
  });

  it('lists a footprint for every furniture model', () => {
    expect(Object.keys(FOOTPRINTS).sort()).toEqual([...MODELS].sort());
  });

  it.each(MODELS)('styles %s', (model) => {
    expect(PROP_STYLES[model]).toBeDefined();
  });

  it.each(MODELS)('keeps every %s part inside its footprint and height', (model) => {
    const [width, depth] = FOOTPRINTS[model];
    const { height, color } = PROP_STYLES[model];

    const parts = FURNITURE_BUILDERS[model]({ width, depth, height, color });

    expect(parts.length).toBeGreaterThan(0);
    parts.forEach((part) => {
      const [halfX, halfY, halfZ] = extent(part);
      const [x, y, z] = part.position;
      expect(Math.abs(x) + halfX).toBeLessThanOrEqual(width / 2 + TOLERANCE);
      expect(Math.abs(z) + halfZ).toBeLessThanOrEqual(depth / 2 + TOLERANCE);
      expect(y - halfY).toBeGreaterThanOrEqual(-TOLERANCE);
      expect(y + halfY).toBeLessThanOrEqual(height + TOLERANCE);
    });
  });
});
