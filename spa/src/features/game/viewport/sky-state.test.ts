import { describe, expect, it } from 'vitest';

import { HORIZON_COLOR, NIGHT_SKY_COLOR, skyStateAt, sunDirectionAt } from './sky-state';

describe('sunDirectionAt', () => {
  it('rises in the east and sets in the west', () => {
    // Act
    const morning = sunDirectionAt(7);
    const evening = sunDirectionAt(18);

    // Assert
    expect(morning.x).toBeGreaterThan(0);
    expect(evening.x).toBeLessThan(0);
  });

  it('sits highest around the middle of the day', () => {
    // Act
    const noon = sunDirectionAt(12.5);

    // Assert
    expect(noon.y).toBeGreaterThan(sunDirectionAt(8).y);
    expect(noon.y).toBeGreaterThan(sunDirectionAt(17).y);
  });

  it('is below the horizon at midnight', () => {
    // Act
    const midnight = sunDirectionAt(0);

    // Assert
    expect(midnight.y).toBeLessThan(0);
  });
});

describe('skyStateAt', () => {
  it('lights the scene from above with a bright sun at midday', () => {
    // Act
    const { light, isNight } = skyStateAt(12.5);

    // Assert
    expect(isNight).toBe(false);
    expect(light.direction.y).toBeGreaterThan(0.5);
    expect(light.intensity).toBeGreaterThan(1.4);
  });

  it('lights the scene from above with dim moonlight at night', () => {
    // Act
    const { light, isNight } = skyStateAt(0);

    // Assert
    expect(isNight).toBe(true);
    expect(light.direction.y).toBeGreaterThan(0);
    expect(light.intensity).toBeLessThan(0.3);
  });

  it('warms the sunlight near the horizon', () => {
    // Act
    const dawn = skyStateAt(6.5).light.color;
    const noon = skyStateAt(12.5).light.color;

    // Assert
    expect(dawn.b).toBeLessThan(noon.b);
  });

  it('dims the light gradually through dusk rather than switching off', () => {
    // Act
    const intensities = [17, 18, 18.5, 19, 19.5].map((hour) => skyStateAt(hour).light.intensity);

    // Assert
    expect(intensities[0]).toBeGreaterThan(intensities[1]);
    expect(intensities[1]).toBeGreaterThan(intensities[2]);
    expect(intensities[2]).toBeGreaterThan(intensities[3]);
  });

  it('matches the fog to the night sky after dark and the horizon at midday', () => {
    // Act
    const night = skyStateAt(0).fogColor.getHexString();
    const day = skyStateAt(12.5).fogColor.getHexString();

    // Assert
    expect(`#${night}`).toBe(NIGHT_SKY_COLOR);
    expect(`#${day}`).toBe(HORIZON_COLOR);
  });

  it('blends the fog through intermediate colours across dusk instead of snapping', () => {
    // Arrange
    const hours = Array.from({ length: 41 }, (_, index) => 17.5 + index * 0.05);

    // Act
    const fogs = hours.map((hour) => skyStateAt(hour).fogColor);

    // Assert
    const largestStep = Math.max(
      ...fogs
        .slice(1)
        .map((fog, index) =>
          Math.hypot(fog.r - fogs[index]!.r, fog.g - fogs[index]!.g, fog.b - fogs[index]!.b),
        ),
    );
    expect(largestStep).toBeLessThan(0.05);
  });

  it('fades the sky daylight from full to none across dusk', () => {
    // Act
    const daylights = [17, 18.5, 19, 19.5, 21].map((hour) => skyStateAt(hour).daylight);

    // Assert
    expect(daylights[0]).toBeGreaterThan(0.95);
    expect(daylights[4]).toBe(0);
    expect(daylights[1]).toBeGreaterThan(daylights[2]!);
    expect(daylights[2]).toBeGreaterThan(daylights[3]!);
  });
});
