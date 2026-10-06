import { describe, expect, it } from 'vitest';

import type { NearbyPropSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { WALL_HEIGHT } from './layout-math';
import {
  indoorAmbientAt,
  lightRig,
  LIGHT_SLOTS,
  lightSourcesFor,
  shadowReach,
  type LightSource,
} from './light-rig';

const NOON = 12.5;
const MIDNIGHT = 0;
const ORIGIN = { x: 0, z: 0 };

const source = (id: string, x: number, daylightDamping: number): LightSource => ({
  id,
  x,
  y: 1,
  z: 0,
  color: '#ffffff',
  intensity: 10,
  daylightDamping,
});

describe('lightSourcesFor', () => {
  it('places a hearth light in front of the fireplace', () => {
    // Arrange
    const fireplace = {
      id: 'f',
      model: 'FurnitureFireplace',
      placement: { x: 1, y: 8, angle: Math.PI / 2 },
      footprint: { width: 1.5, depth: 0.6 },
    } as NearbyPropSnapshot;

    // Act
    const [light] = lightSourcesFor([fireplace]);

    // Assert
    expect(light!.x).toBeGreaterThan(fireplace.placement.x);
    expect(light!.y).toBeLessThan(WALL_HEIGHT);
    expect(light!.daylightDamping).toBe(0);
  });

  it('hangs a chandelier light below the ceiling that goes out in daylight', () => {
    // Arrange
    const chandelier = {
      id: 'c',
      model: 'FurnitureChandelier',
      placement: { x: 4, y: 5, angle: 0 },
      footprint: { width: 0.9, depth: 0.9 },
    } as NearbyPropSnapshot;

    // Act
    const [light] = lightSourcesFor([chandelier]);

    // Assert
    expect(light).toMatchObject({ x: 4, z: 5, daylightDamping: expect.any(Number) });
    expect(light!.y).toBeLessThan(WALL_HEIGHT);
  });

  it('puts a sconce light in front of its wall mount', () => {
    // Arrange
    const sconce = {
      id: 's',
      model: 'FurnitureWallSconce',
      placement: { x: 4, y: 0.1, angle: Math.PI },
      footprint: { width: 0.3, depth: 0.2 },
    } as NearbyPropSnapshot;

    // Act
    const [light] = lightSourcesFor([sconce]);

    // Assert
    expect(light!.z).toBeGreaterThan(sconce.placement.y);
    expect(light!.daylightDamping).toBeGreaterThan(0);
  });

  it.each(['FurnitureStreetLantern', 'FurnitureWallLantern'] as const)(
    'lights %s only after dark',
    (model) => {
      // Arrange
      const lantern = {
        id: 'l',
        model,
        placement: { x: 2, y: 3, angle: 0 },
        footprint: { width: 0.6, depth: 0.6 },
      } as NearbyPropSnapshot;

      // Act
      const [light] = lightSourcesFor([lantern]);

      // Assert
      expect(light).toMatchObject({ x: 2, daylightDamping: 1 });
      expect(light!.y).toBeGreaterThan(1);
    },
  );

  it('ignores props that do not give light', () => {
    // Arrange
    const table = { id: 't', model: 'FurnitureTable' } as NearbyPropSnapshot;

    // Act
    const lights = lightSourcesFor([table]);

    // Assert
    expect(lights).toEqual([]);
  });
});

describe('shadowReach', () => {
  it('reaches the far corner of the room at ceiling height', () => {
    // Act
    const reach = shadowReach({ width: 20, depth: 16 });

    // Assert
    expect(reach).toBeGreaterThan(Math.hypot(20, 16, WALL_HEIGHT));
  });
});

describe('lightRig', () => {
  it('always returns the fixed number of slots', () => {
    // Act
    const rig = lightRig([source('a', 1, 0)], NOON, ORIGIN);

    // Assert
    expect(rig).toHaveLength(LIGHT_SLOTS);
  });

  it('leaves unused slots dark', () => {
    // Act
    const rig = lightRig([source('a', 1, 0)], NOON, ORIGIN);

    // Assert
    expect(rig.slice(1).every((slot) => slot.intensity === 0)).toBe(true);
  });

  it('keeps only the nearest sources', () => {
    // Arrange
    const sources = [5, 1, 4, 2, 3, 6].map((x) => source(`s${x}`, x, 0));

    // Act
    const rig = lightRig(sources, NOON, ORIGIN);

    // Assert
    expect(rig.map((slot) => slot.x)).toEqual([1, 2, 3, 4]);
  });

  it('prefers a strong far source over weak near ones', () => {
    // Arrange
    const weak = [1, 2, 3, 4].map((x) => ({ ...source(`w${x}`, x, 0), intensity: 1 }));
    const strong = { ...source('hearth', 8, 0), intensity: 10 };

    // Act
    const rig = lightRig([...weak, strong], NOON, ORIGIN);

    // Assert
    expect(rig.map((slot) => slot.x)).toContain(8);
  });

  it('keeps each source in the same slot as the focus moves', () => {
    // Arrange
    const low = { ...source('low', 0, 0), y: 0.4 };
    const high = { ...source('high', 10, 0), y: 2.4 };

    // Act
    const nearLow = lightRig([low, high], NOON, { x: 0, z: 0 });
    const nearHigh = lightRig([low, high], NOON, { x: 10, z: 0 });

    // Assert
    expect(nearHigh.map((slot) => slot.x)).toEqual(nearLow.map((slot) => slot.x));
  });

  it('keeps hearths burning at midday', () => {
    // Act
    const [slot] = lightRig([source('h', 1, 0)], NOON, ORIGIN);

    // Assert
    expect(slot!.intensity).toBe(10);
  });

  it('turns lanterns fully off in full daylight', () => {
    // Arrange
    const sources = [source('l', 1, 1)];

    // Act
    const [day] = lightRig(sources, NOON, ORIGIN);

    // Assert
    expect(day!.intensity).toBe(0);
  });

  it('dims candles and lanterns in daylight and brings them up at night', () => {
    // Arrange
    const sources = [source('c', 1, 0.85)];

    // Act
    const day = lightRig(sources, NOON, ORIGIN)[0]!.intensity;
    const night = lightRig(sources, MIDNIGHT, ORIGIN)[0]!.intensity;

    // Assert
    expect(day).toBeLessThan(night / 4);
    expect(night).toBe(10);
  });
});

describe('indoorAmbientAt', () => {
  it('is bright by day and a dim floor at night', () => {
    // Act
    const day = indoorAmbientAt(NOON).intensity;
    const night = indoorAmbientAt(MIDNIGHT).intensity;

    // Assert
    expect(day).toBeGreaterThan(night * 2);
    expect(night).toBeGreaterThan(0);
  });

  it('tints the fill darker at night than by day', () => {
    // Act
    const day = indoorAmbientAt(NOON).sky;
    const night = indoorAmbientAt(MIDNIGHT).sky;

    // Assert
    expect(night.r + night.g + night.b).toBeLessThan(day.r + day.g + day.b);
  });
});
