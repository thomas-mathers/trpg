import type { WeatherLook } from './weather-look';

const BLENDED_KEYS = [
  'cloudCoverage',
  'cloudDensity',
  'sunLight',
  'ambientLight',
  'saturation',
  'brightness',
  'fogScale',
  'mist',
] as const satisfies readonly (keyof WeatherLook)[];

const SETTLE_SECONDS = 4;
const SETTLED_GAP = 0.005;
const PARTICLES_PER_SECOND = 250;

type ParticleLook = Pick<WeatherLook, 'particles' | 'particleCount' | 'fallSpeed'>;

function approach(current: number, target: number, blend: number) {
  const gap = target - current;
  return Math.abs(gap) <= SETTLED_GAP ? target : current + gap * blend;
}

function stepParticles(
  current: WeatherLook,
  target: WeatherLook,
  seconds: number,
  blend: number,
): ParticleLook {
  const switching = current.particles !== target.particles;
  if (switching && current.particleCount <= 0) {
    return {
      particles: target.particles,
      particleCount: 0,
      fallSpeed: target.fallSpeed,
    };
  }
  const goal = switching ? 0 : target.particleCount;
  const reach = PARTICLES_PER_SECOND * seconds;
  const change = Math.max(-reach, Math.min(reach, goal - current.particleCount));
  return {
    particles: current.particles,
    particleCount: current.particleCount + change,
    fallSpeed: switching ? current.fallSpeed : approach(current.fallSpeed, target.fallSpeed, blend),
  };
}

export function stepLook(current: WeatherLook, target: WeatherLook, seconds: number): WeatherLook {
  const blend = 1 - Math.exp(-seconds / SETTLE_SECONDS);
  const next: WeatherLook = {
    ...target,
    ...stepParticles(current, target, seconds, blend),
  };
  for (const key of BLENDED_KEYS) next[key] = approach(current[key], target[key], blend);
  return isSettled(next, target) ? target : next;
}

function isSettled(look: WeatherLook, target: WeatherLook) {
  return (
    BLENDED_KEYS.every((key) => look[key] === target[key]) &&
    look.particles === target.particles &&
    look.particleCount === target.particleCount &&
    look.fallSpeed === target.fallSpeed
  );
}
