import { describe, expect, it } from 'vitest';

import { outdoorFogStateAt } from './outdoor-fog-state';
import { weatherLookFor } from './weather-look';

const CLEAR = weatherLookFor('Clear');

describe('outdoorFogStateAt', () => {
  it('scatters sunlight by day and not at night', () => {
    // Act
    const noon = outdoorFogStateAt(12, CLEAR);
    const midnight = outdoorFogStateAt(0, CLEAR);

    // Assert
    expect(noon.scatter).toBeGreaterThan(0);
    expect(midnight.scatter).toBe(0);
  });

  it('adds no mist in clear weather', () => {
    // Act
    const state = outdoorFogStateAt(6, CLEAR);

    // Assert
    expect(state.mist).toBe(0);
  });

  it('thickens the mist around dawn compared to noon', () => {
    // Arrange
    const rain = weatherLookFor('Rain');

    // Act
    const dawn = outdoorFogStateAt(6, rain);
    const noon = outdoorFogStateAt(12, rain);

    // Assert
    expect(dawn.mist).toBeGreaterThan(noon.mist);
  });

  it('thickens the mist in foggy weather', () => {
    // Act
    const foggy = outdoorFogStateAt(12, weatherLookFor('Fog'));
    const clear = outdoorFogStateAt(12, CLEAR);

    // Assert
    expect(foggy.mist).toBeGreaterThan(clear.mist);
  });
});
