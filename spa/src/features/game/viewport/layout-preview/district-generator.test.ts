import { describe, expect, it } from 'vitest';

import { DISTRICTS } from './district-catalog';
import { doorOf, generateDistrict, rowComparison, type Rect } from './district-generator';
import { floorNames, generateInterior } from './interior-generator';

function overlaps(a: Rect, b: Rect) {
  return (
    a.x < b.x + b.width - 1e-6 &&
    a.x + a.width > b.x + 1e-6 &&
    a.y < b.y + b.depth - 1e-6 &&
    a.y + a.depth > b.y + 1e-6
  );
}

describe('district layout prototype', () => {
  it.each(DISTRICTS.flatMap((kind) => [0.75, 1, 1.3].map((scale) => ({ kind, scale }))))(
    'keeps $kind buildings clear at scale $scale',
    ({ kind, scale }) => {
      for (let seed = 0; seed < 50; seed++) {
        const district = generateDistrict({ seed, blocks: 3, alley: 1.5, scale, kind });
        const { buildings, width, depth, streets, square } = district;
        buildings.forEach((building, index) => {
          expect(building.x).toBeGreaterThanOrEqual(0);
          expect(building.y).toBeGreaterThanOrEqual(0);
          expect(building.x + building.width).toBeLessThanOrEqual(width);
          expect(building.y + building.depth).toBeLessThanOrEqual(depth);
          expect(buildings.slice(index + 1).some((other) => overlaps(building, other))).toBe(false);
          expect([...streets, square].some((other) => overlaps(building, other))).toBe(false);
          const door = doorOf(building);
          expect(
            streets.some(
              (street) =>
                door.x >= street.x - 1e-6 &&
                door.x <= street.x + street.width + 1e-6 &&
                door.y >= street.y - 1e-6 &&
                door.y <= street.y + street.depth + 1e-6,
            ),
          ).toBe(true);
        });
      }
    },
  );

  it('reproduces a seed and changes the layout for a different seed', () => {
    const options = { seed: 42, blocks: 2, alley: 2, scale: 1 };
    const district = generateDistrict(options);
    expect(generateDistrict(options)).toEqual(district);
    expect(generateDistrict({ ...options, seed: 43 })).not.toEqual(district);
  });

  it('preserves the same footprint areas in the rows comparison', () => {
    const district = generateDistrict({ seed: 42, blocks: 2, alley: 2, scale: 1 });
    const rows = rowComparison(district);
    rows.buildings.forEach((building, index) => {
      expect(building.width * building.depth).toBeCloseTo(
        district.buildings[index].width * district.buildings[index].depth,
      );
      expect(rows.buildings.slice(index + 1).some((other) => overlaps(building, other))).toBe(
        false,
      );
    });
  });

  it.each(DISTRICTS)('fits every floor within the %s buildings', (kind) => {
    const district = generateDistrict({ seed: 77, blocks: 3, alley: 3, scale: 1.3, kind });
    district.buildings.forEach((building) =>
      floorNames(building, 77).forEach((names, floor) => {
        const interior = generateInterior(building, 77, floor);
        const area = interior.rooms.reduce(
          (sum, room) => sum + room.width * room.depth,
          interior.hall.width * interior.hall.depth,
        );
        expect(area).toBeLessThanOrEqual(building.width * building.depth + 1e-6);
        expect(interior.width).toBeCloseTo(building.frontage);
        expect(interior.depth).toBeCloseTo(building.length);
        expect(interior.rooms.map((room) => room.name)).toEqual(names);
        interior.rooms.forEach((room, index) => {
          expect(room.width).toBeGreaterThan(1.5);
          expect(room.depth).toBeGreaterThan(1);
          expect(overlaps(room, interior.hall)).toBe(false);
          expect(interior.rooms.slice(index + 1).some((other) => overlaps(room, other))).toBe(
            false,
          );
        });
      }),
    );
  });

  it('uses the garrison roster and preserves the inn floors', () => {
    const options = { seed: 42, blocks: 2, alley: 2, scale: 1 };
    const camp = generateDistrict({ ...options, kind: 'Encampment' });
    expect(camp.buildings.map((b) => b.type).sort()).toEqual(['Barracks', 'Blacksmith', 'Stable']);
    const center = generateDistrict({ ...options, kind: 'CityCenter', guildHall: false });
    expect(center.buildings.map((b) => b.type).sort()).toEqual([
      'Bakery',
      'Carpenter',
      'GeneralGoods',
      'Inn',
      'Jeweler',
      'Tailor',
      'Tavern',
    ]);
    const inn = center.buildings.find((b) => b.type === 'Inn')!;
    expect(floorNames(inn, 42)).toEqual([
      ['Lobby'],
      ['North Guest Room', 'South Guest Room', 'East Guest Room', 'West Guest Room'],
      ["Owner's Quarters"],
    ]);
  });

  it('keeps the entrance and its row comparison finite without inventing buildings', () => {
    const entrance = generateDistrict({
      seed: 42,
      blocks: 2,
      alley: 2,
      scale: 1,
      kind: 'CityEntrance',
    });
    expect(entrance.buildings).toEqual([]);
    const rows = rowComparison(entrance);
    expect(Number.isFinite(rows.depth)).toBe(true);
    expect(rows.width).toBeGreaterThan(0);
  });
});
