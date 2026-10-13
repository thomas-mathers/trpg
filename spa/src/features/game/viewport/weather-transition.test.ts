import { describe, expect, it } from 'vitest';

import { weatherLookFor } from './weather-look';
import { stepLook } from './weather-transition';

const CLEAR = weatherLookFor('Clear');
const FOG = weatherLookFor('Fog');

describe('stepLook', () => {
  it('moves partway toward the target', () => {
    // Act
    const next = stepLook(CLEAR, FOG, 0.1);

    // Assert
    expect(next.mist).toBeGreaterThan(CLEAR.mist);
    expect(next.mist).toBeLessThan(FOG.mist);
  });

  it('returns the target itself once every blended value has settled', () => {
    // Act
    const next = stepLook(CLEAR, CLEAR, 0.1);

    // Assert
    expect(next).toBe(CLEAR);
  });

  it('starts a new particle kind from zero', () => {
    // Act
    const next = stepLook(CLEAR, weatherLookFor('Rain'), 0.01);

    // Assert
    expect(next.particles).toBe('rain');
    expect(next.particleCount).toBe(0);
  });

  it('reaches the target given enough time', () => {
    // Arrange
    let look = CLEAR;

    // Act
    for (let frame = 0; frame < 6000; frame++) look = stepLook(look, FOG, 0.016);

    // Assert
    expect(look).toBe(FOG);
  });

  it('ramps the particle count up gradually', () => {
    // Arrange
    const rain = weatherLookFor('Rain');

    // Act
    const started = stepLook(CLEAR, rain, 0.01);
    const next = stepLook(started, rain, 0.5);

    // Assert
    expect(next.particleCount).toBeGreaterThan(0);
    expect(next.particleCount).toBeLessThan(rain.particleCount);
  });

  it('fades the old particles out before switching kind', () => {
    // Arrange
    const rain = weatherLookFor('Rain');
    const snow = weatherLookFor('Snow');

    // Act
    const next = stepLook(rain, snow, 0.5);

    // Assert
    expect(next.particles).toBe('rain');
    expect(next.particleCount).toBeLessThan(rain.particleCount);
  });
});
