import { Color } from 'three';

import type { WeatherCondition } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import type { SkyState } from './sky-state';

export type ParticleKind = 'none' | 'rain' | 'snow';

export interface WeatherLook {
  cloudCoverage: number;
  cloudDensity: number;
  sunLight: number;
  ambientLight: number;
  saturation: number;
  brightness: number;
  fogScale: number;
  particles: ParticleKind;
  particleCount: number;
  fallSpeed: number;
  lightning: boolean;
}

const NO_PARTICLES = {
  particles: 'none',
  particleCount: 0,
  fallSpeed: 0,
  lightning: false,
} as const;

const LOOKS: Record<WeatherCondition, WeatherLook> = {
  Clear: {
    cloudCoverage: 0.25,
    cloudDensity: 0.3,
    sunLight: 1,
    ambientLight: 1,
    saturation: 1,
    brightness: 1,
    fogScale: 1,
    ...NO_PARTICLES,
  },
  Cloudy: {
    cloudCoverage: 0.7,
    cloudDensity: 0.7,
    sunLight: 0.7,
    ambientLight: 1.05,
    saturation: 0.6,
    brightness: 0.85,
    fogScale: 0.9,
    ...NO_PARTICLES,
  },
  Rain: {
    cloudCoverage: 0.9,
    cloudDensity: 0.95,
    sunLight: 0.4,
    ambientLight: 1,
    saturation: 0.4,
    brightness: 0.6,
    fogScale: 0.6,
    particles: 'rain',
    particleCount: 1800,
    fallSpeed: 14,
    lightning: false,
  },
  Storm: {
    cloudCoverage: 1,
    cloudDensity: 1,
    sunLight: 0.2,
    ambientLight: 0.9,
    saturation: 0.3,
    brightness: 0.4,
    fogScale: 0.45,
    particles: 'rain',
    particleCount: 2600,
    fallSpeed: 18,
    lightning: true,
  },
  Snow: {
    cloudCoverage: 0.85,
    cloudDensity: 0.8,
    sunLight: 0.5,
    ambientLight: 1.15,
    saturation: 0.5,
    brightness: 0.85,
    fogScale: 0.55,
    particles: 'snow',
    particleCount: 1800,
    fallSpeed: 1.4,
    lightning: false,
  },
  Fog: {
    cloudCoverage: 0.5,
    cloudDensity: 0.5,
    sunLight: 0.45,
    ambientLight: 1.1,
    saturation: 0.3,
    brightness: 0.8,
    fogScale: 0.25,
    ...NO_PARTICLES,
  },
};

const LIGHTNING_PERIOD = 9;
const LUMA = [0.2126, 0.7152, 0.0722] as const;

export function weatherLookFor(condition: WeatherCondition | undefined): WeatherLook {
  return LOOKS[condition ?? 'Clear'];
}

export function applyWeather(state: SkyState, look: WeatherLook): SkyState {
  const { light, ambient, fogColor } = state;
  return {
    ...state,
    light: {
      ...light,
      color: desaturate(light.color, look.saturation),
      intensity: light.intensity * look.sunLight,
    },
    ambient: {
      sky: desaturate(ambient.sky, look.saturation),
      ground: desaturate(ambient.ground, look.saturation),
      intensity: ambient.intensity * look.ambientLight,
    },
    fogColor: desaturate(fogColor, look.saturation).multiplyScalar(look.brightness),
  };
}

export function lightningFlashAt(seconds: number): number {
  const cycle = Math.floor(seconds / LIGHTNING_PERIOD);
  const start = cycle * LIGHTNING_PERIOD + hash(cycle) * (LIGHTNING_PERIOD - 1);
  const age = seconds - start;
  return Math.max(pulse(age, 0.15), 0.6 * pulse(age - 0.25, 0.2));
}

export function wrapAround(value: number, low: number, size: number): number {
  const offset = value - low;
  return low + offset - Math.floor(offset / size) * size;
}

function desaturate(source: Color, saturation: number): Color {
  const luma = source.r * LUMA[0] + source.g * LUMA[1] + source.b * LUMA[2];
  return source.clone().lerp(new Color(luma, luma, luma), 1 - saturation);
}

function pulse(age: number, width: number): number {
  return age < 0 || age > width ? 0 : 1 - age / width;
}

function hash(value: number): number {
  const scrambled = Math.sin(value * 127.1) * 43758.5453;
  return scrambled - Math.floor(scrambled);
}
