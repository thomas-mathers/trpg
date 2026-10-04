import { describe, expect, it } from 'vitest';

import { DISTRICTS } from './district-catalog';
import { doorOf, generateDistrict, type Rect } from './district-generator';
import { floorNames, generateInterior } from './interior-generator';
import {
  districtPopulation,
  interiorPopulation,
  intersects,
  isSolid,
  type Population,
} from './preview-population';

function occupied({ props, creatures }: Population): Rect[] {
  return [
    ...props.filter(isSolid),
    ...creatures.map(({ x, y }) => ({ x: x - 0.3, y: y - 0.3, width: 0.6, depth: 0.6 })),
  ];
}

function expectClear(population: Population, obstacles: Rect[], width: number, depth: number) {
  const boxes = occupied(population);
  expect(population.creatures.length).toBeGreaterThan(0);
  expect(population.props.length).toBeGreaterThan(0);
  boxes.forEach((box, index) => {
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.y).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(width);
    expect(box.y + box.depth).toBeLessThanOrEqual(depth);
    expect([...obstacles, ...boxes.slice(index + 1)].some((other) => intersects(box, other))).toBe(
      false,
    );
  });
}

describe('preview scale population', () => {
  it.each(DISTRICTS)(
    'keeps %s creatures and props out of buildings and door approaches',
    (kind) => {
      for (let seed = 0; seed < 10; seed++) {
        const district = generateDistrict({ kind, seed, blocks: 2, alley: 1.5, scale: 0.75 });
        const population = districtPopulation(district, seed);
        const doors = district.buildings.map((building) => {
          const { x, y } = doorOf(building);
          return { x: x - 1.5, y: y - 1.5, width: 3, depth: 3 };
        });
        expectClear(population, [...district.buildings, ...doors], district.width, district.depth);
        expect(districtPopulation(district, seed)).toEqual(population);
      }
    },
  );

  it.each(DISTRICTS)('fits furnishings and creatures inside each %s floor', (kind) => {
    const district = generateDistrict({ kind, seed: 42, blocks: 2, alley: 2, scale: 0.75 });
    district.buildings.forEach((building) =>
      floorNames(building, 42).forEach((_, floor) => {
        const interior = generateInterior(building, 42, floor);
        const population = interiorPopulation(interior, 42);
        expectClear(population, [], interior.width, interior.depth);
        if (interior.hall.width > 0)
          expect(population.props.some((prop) => intersects(prop, interior.hall))).toBe(false);
        expect(
          population.props.every((prop) =>
            interior.rooms.some(
              (room) =>
                prop.x >= room.x &&
                prop.y >= room.y &&
                prop.x + prop.width <= room.x + room.width &&
                prop.y + prop.depth <= room.y + room.depth,
            ),
          ),
        ).toBe(true);
      }),
    );
  });
});
