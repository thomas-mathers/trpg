import { describe, expect, it } from 'vitest';

import type { WeatherCondition } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { skyStateAt } from './sky-state';
import { applyWeather, lightningFlashAt, weatherLookFor, wrapAround } from './weather-look';

const NOON = 12;

describe('weatherLookFor', () => {
  it('treats a missing condition as clear', () => {
    expect(weatherLookFor(undefined)).toBe(weatherLookFor('Clear'));
  });

  it.each<[WeatherCondition, string]>([
    ['Rain', 'rain'],
    ['Storm', 'rain'],
    ['Snow', 'snow'],
    ['Clear', 'none'],
    ['Cloudy', 'none'],
    ['Fog', 'none'],
  ])('%s uses %s particles', (condition, kind) => {
    expect(weatherLookFor(condition).particles).toBe(kind);
  });

  it('only storms flash lightning', () => {
    const flashing = (['Clear', 'Cloudy', 'Rain', 'Storm', 'Snow', 'Fog'] as const).filter(
      (condition) => weatherLookFor(condition).lightning,
    );

    expect(flashing).toEqual(['Storm']);
  });
});

describe('applyWeather', () => {
  it('leaves clear weather untouched', () => {
    const state = skyStateAt(NOON);

    const result = applyWeather(state, weatherLookFor('Clear'));

    expect(result.light.intensity).toBe(state.light.intensity);
    expect(result.fogColor.equals(state.fogColor)).toBe(true);
  });

  it('dims the sun and darkens the fog in a storm', () => {
    const state = skyStateAt(NOON);

    const result = applyWeather(state, weatherLookFor('Storm'));

    expect(result.light.intensity).toBeLessThan(state.light.intensity);
    expect(result.fogColor.r).toBeLessThan(state.fogColor.r);
  });

  it('does not mutate the source state', () => {
    const state = skyStateAt(NOON);
    const before = state.fogColor.clone();

    applyWeather(state, weatherLookFor('Fog'));

    expect(state.fogColor.equals(before)).toBe(true);
  });
});

describe('lightningFlashAt', () => {
  it('stays within 0 to 1 and flashes at least once per period', () => {
    const samples = Array.from({ length: 900 }, (_, index) => lightningFlashAt(index / 100));

    expect(Math.min(...samples)).toBe(0);
    expect(Math.max(...samples)).toBeGreaterThan(0.9);
    expect(Math.max(...samples)).toBeLessThanOrEqual(1);
  });

  it('is deterministic for a given time', () => {
    expect(lightningFlashAt(4.2)).toBe(lightningFlashAt(4.2));
  });
});

describe('wrapAround', () => {
  it.each([
    [5, 0, 10, 5],
    [12, 0, 10, 2],
    [-3, 0, 10, 7],
    [2, -5, 10, 2],
  ])('wraps %d into [%d, +%d)', (value, low, size, expected) => {
    expect(wrapAround(value, low, size)).toBeCloseTo(expected);
  });
});
