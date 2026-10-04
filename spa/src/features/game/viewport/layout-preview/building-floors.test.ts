import { describe, expect, it } from 'vitest';

import { DISTRICTS, upperRoomArea } from './district-catalog';
import { generateDistrict, type Building } from './district-generator';
import { floorNames, generateInterior } from './interior-generator';
import { intersects } from './preview-population';

function findBuilding(kind: Parameters<typeof generateDistrict>[0]['kind'], type: string) {
  const district = generateDistrict({ kind, seed: 42, blocks: 2, alley: 2, scale: 0.75 });
  return district.buildings.find((building) => building.type === type)!;
}

const area = ({ width, depth }: { width: number; depth: number }) => width * depth;

describe('upper floors', () => {
  it('sizes the inn owner quarters as a private room instead of a whole-building floor', () => {
    const interior = generateInterior(findBuilding('CityCenter', 'Inn'), 42, 2);
    expect(area(interior.rooms[0])).toBeGreaterThanOrEqual(15);
    expect(area(interior.rooms[0])).toBeLessThanOrEqual(30);
  });

  it('gives an owner suite more space than a guest room', () => {
    const inn = findBuilding('CityCenter', 'Inn');
    const suite = generateInterior(inn, 42, 2).rooms[0];
    const guest = generateInterior(inn, 42, 1).rooms[0];
    expect(area(suite)).toBeGreaterThan(area(guest));
  });

  it('keeps every guest room the same size', () => {
    const rooms = generateInterior(findBuilding('CityCenter', 'Inn'), 42, 1).rooms;
    rooms.forEach((room) => expect(area(room)).toBeCloseTo(area(rooms[0])));
    expect(area(rooms[0])).toBeCloseTo(upperRoomArea('North Guest Room')!);
  });

  it('keeps the ground floor footprint and puts the stairs inside the hallway', () => {
    const inn = findBuilding('CityCenter', 'Inn');
    const interior = generateInterior(inn, 42, 2);
    expect(interior.width).toBeCloseTo(inn.frontage);
    expect(interior.depth).toBeCloseTo(inn.length);
    expect(interior.hall.width).toBeGreaterThan(0);
    expect(interior.stairs.x).toBeGreaterThanOrEqual(interior.hall.x);
    expect(interior.stairs.x + interior.stairs.width).toBeLessThanOrEqual(
      interior.hall.x + interior.hall.width,
    );
    expect(interior.rooms.some((room) => intersects(room, interior.stairs))).toBe(false);
  });

  it.each(DISTRICTS)('stacks the stairs on every floor of %s buildings', (kind) => {
    const district = generateDistrict({ kind, seed: 42, blocks: 2, alley: 2, scale: 0.75 });
    district.buildings.forEach((building) => {
      const [ground, ...upper] = floorNames(building, 42).map((_, floor) =>
        generateInterior(building, 42, floor),
      );
      upper.forEach((interior) => expect(interior.stairs).toEqual(ground.stairs));
    });
  });

  it('opens every upper room onto the hallway', () => {
    const interior = generateInterior(findBuilding('CityCenter', 'Inn'), 42, 1);
    interior.rooms.forEach((room) => {
      const touchesHall =
        Math.abs(room.x + room.width - interior.hall.x) < 1e-6 ||
        Math.abs(room.x - (interior.hall.x + interior.hall.width)) < 1e-6;
      expect(touchesHall).toBe(true);
    });
  });
});

describe('building height', () => {
  it.each(DISTRICTS)('is at least 3 m per floor in %s buildings', (kind) => {
    const district = generateDistrict({ kind, seed: 42, blocks: 2, alley: 2, scale: 0.75 });
    district.buildings.forEach((building: Building) =>
      expect(building.height).toBeGreaterThanOrEqual(floorNames(building, 42).length * 3),
    );
  });
});
