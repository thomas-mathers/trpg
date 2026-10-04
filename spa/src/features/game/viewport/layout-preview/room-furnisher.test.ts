import { describe, expect, it } from 'vitest';

import { generateDistrict } from './district-generator';
import { generateInterior } from './interior-generator';
import { districtPopulation, interiorPopulation, intersects } from './preview-population';

describe('purposeful furnishings', () => {
  it('uses a forge and anvil in the blacksmith workshop', () => {
    const district = generateDistrict({
      kind: 'Encampment',
      seed: 42,
      blocks: 2,
      alley: 2,
      scale: 0.75,
    });
    const building = district.buildings.find((b) => b.type === 'Blacksmith')!;
    const interior = generateInterior(building, 42);
    const { props } = interiorPopulation(interior, 42);
    expect(props.map((p) => p.name)).toEqual(
      expect.arrayContaining(['Forge', 'Anvil', 'Counter', 'Weapon rack']),
    );
    const forge = props.find((p) => p.name === 'Forge')!;
    expect(forge.x).toBeCloseTo(0.2);
    expect(forge.y + forge.depth).toBeCloseTo(interior.depth - 0.2);
  });

  it('keeps a central aisle through the temple pews', () => {
    const district = generateDistrict({
      kind: 'HolySite',
      seed: 42,
      blocks: 2,
      alley: 2,
      scale: 0.75,
    });
    const interior = generateInterior(district.buildings[0], 42);
    const { props } = interiorPopulation(interior, 42);
    const pews = props.filter((p) => p.name === 'Pew');
    expect(pews.length).toBeGreaterThanOrEqual(8);
    const aisle = { x: interior.width / 2 - 1, y: 0, width: 2, depth: interior.depth - 2 };
    expect(pews.some((pew) => intersects(pew, aisle))).toBe(false);
    expect(props.some((p) => p.name === 'Altar')).toBe(true);
  });

  it('puts one bed against the wall of each guest room', () => {
    const district = generateDistrict({
      kind: 'CityCenter',
      seed: 42,
      blocks: 2,
      alley: 2,
      scale: 0.75,
    });
    const inn = district.buildings.find((b) => b.type === 'Inn')!;
    const interior = generateInterior(inn, 42, 1);
    const { props } = interiorPopulation(interior, 42);
    expect(props.filter((p) => p.name === 'Bed')).toHaveLength(4);
    interior.rooms.forEach((room) => {
      expect(
        props.filter(
          (p) =>
            p.name === 'Bed' &&
            [room.x + 0.2, room.x + room.width - p.width - 0.2].some(
              (wallX) => Math.abs(p.x - wallX) < 1e-6,
            ) &&
            Math.abs(p.y - room.y - 0.2) < 1e-6,
        ),
      ).toHaveLength(1);
    });
  });

  it('runs inn lobby benches lengthwise along the west wall', () => {
    const district = generateDistrict({
      kind: 'CityCenter',
      seed: 42,
      blocks: 2,
      alley: 2,
      scale: 0.75,
    });
    const inn = district.buildings.find((b) => b.type === 'Inn')!;
    const { props } = interiorPopulation(generateInterior(inn, 42), 42);
    const benches = props.filter((p) => p.name === 'Bench');
    expect(benches.length).toBeGreaterThan(0);
    benches.forEach((bench) => {
      expect(bench.rotation).toBe(270);
      expect(bench.x).toBeCloseTo(0.2);
      expect(bench.depth).toBeCloseTo(1.5);
      expect(bench.width).toBeCloseTo(0.5);
    });
  });

  it.each(['Bakery', 'Carpenter'])('lines the %s work tables up against the east wall', (type) => {
    const district = generateDistrict({
      kind: 'CityCenter',
      seed: 42,
      blocks: 2,
      alley: 2,
      scale: 0.75,
    });
    const building = district.buildings.find((b) => b.type === type)!;
    const interior = generateInterior(building, 42);
    const tables = interiorPopulation(interior, 42).props.filter((p) => p.name === 'Work table');
    expect(tables.length).toBeGreaterThanOrEqual(2);
    tables.forEach((table) => {
      expect(table.rotation).toBe(90);
      expect(table.x + table.width).toBeCloseTo(interior.width - 0.2);
    });
  });

  it('gives the library many reading tables with chairs', () => {
    const district = generateDistrict({
      kind: 'Scientific',
      seed: 42,
      blocks: 2,
      alley: 2,
      scale: 0.75,
    });
    const library = district.buildings.find((b) => b.type === 'Library')!;
    const { props } = interiorPopulation(generateInterior(library, 42), 42);
    const tables = props.filter((p) => p.name === 'Dining table');
    expect(tables.length).toBeGreaterThanOrEqual(6);
    expect(props.filter((p) => p.name === 'Chair')).toHaveLength(tables.length * 2);
  });

  it('faces the arrival square benches inward from both sides and labels a notice board', () => {
    const district = generateDistrict({
      kind: 'CityEntrance',
      seed: 42,
      blocks: 2,
      alley: 2,
      scale: 0.75,
    });
    const { props } = districtPopulation(district, 42);
    const benches = props.filter((p) => p.name === 'Bench');
    expect(benches.filter((b) => b.rotation === 270)).toHaveLength(3);
    expect(benches.filter((b) => b.rotation === 90)).toHaveLength(3);
    expect(props.some((p) => p.name === 'Notice board')).toBe(true);
  });

  it('stocks each trade with its own goods along the south wall', () => {
    const stock = {
      Scientific: ['Apothecary', 'Cauldron', 'Herb rack'],
      CityCenter: ['Tailor', 'Cloth shelf'],
    } as const;
    Object.entries(stock).forEach(([kind, [type, ...names]]) => {
      const district = generateDistrict({
        kind: kind as 'Scientific' | 'CityCenter',
        seed: 42,
        blocks: 2,
        alley: 2,
        scale: 0.75,
      });
      const building = district.buildings.find((b) => b.type === type)!;
      const { props } = interiorPopulation(generateInterior(building, 42), 42);
      expect(props.map((p) => p.name)).toEqual(expect.arrayContaining(names));
    });
  });

  it('lays rugs under furniture without blocking it or the stairs', () => {
    const district = generateDistrict({
      kind: 'HolySite',
      seed: 42,
      blocks: 2,
      alley: 2,
      scale: 0.75,
    });
    const interior = generateInterior(district.buildings[0], 42);
    const { props } = interiorPopulation(interior, 42);
    const rugs = props.filter((p) => p.shape === 'rug');
    expect(rugs.length).toBeGreaterThan(0);
    rugs.forEach((rug) => expect(intersects(rug, interior.stairs)).toBe(false));
    expect(props.filter((p) => p.name === 'Pew').length).toBeGreaterThan(0);
  });

  it('puts a centrepiece in the middle of the market square', () => {
    const district = generateDistrict({
      kind: 'CityCenter',
      seed: 42,
      blocks: 2,
      alley: 2,
      scale: 0.75,
    });
    const fountain = districtPopulation(district, 42).props.find((p) => p.name === 'Fountain')!;
    const { square } = district;
    expect(fountain.x + fountain.width / 2).toBeCloseTo(square.x + square.width / 2);
    expect(fountain.y + fountain.depth / 2).toBeCloseTo(square.y + square.depth / 2);
  });

  it('places tavern chairs beside dining tables', () => {
    const district = generateDistrict({
      kind: 'CityCenter',
      seed: 42,
      blocks: 2,
      alley: 2,
      scale: 0.75,
    });
    const tavern = district.buildings.find((b) => b.type === 'Tavern')!;
    const { props } = interiorPopulation(generateInterior(tavern, 42), 42);
    const tables = props.filter((p) => p.name === 'Dining table');
    const chairs = props.filter((p) => p.name === 'Chair');
    expect(tables).toHaveLength(2);
    expect(chairs).toHaveLength(4);
    chairs.forEach((chair) =>
      expect(tables.some((table) => intersects(chair, table, 0.5))).toBe(true),
    );
  });
});
