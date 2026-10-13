import { Color, MathUtils, Vector3 } from 'three';

import { skyStateAt } from './sky-state';
import { applyWeather, type WeatherLook } from './weather-look';

const BASE_MIST = 0.0025;
const DAWN_MIST_BOOST = 1.5;
const DAWN_ELEVATION = 0.3;
const SUN_SCATTER = 0.6;

export interface OutdoorFogState {
  color: Color;
  sunTint: Color;
  sunDirection: Vector3;
  mist: number;
  scatter: number;
}

export function outdoorFogStateAt(hour: number, look: WeatherLook): OutdoorFogState {
  const raw = skyStateAt(hour);
  const { fogColor, light } = applyWeather(raw, look);
  const dawn = 1 - MathUtils.smoothstep(Math.abs(raw.sunDirection.y), 0, DAWN_ELEVATION);
  return {
    color: fogColor,
    sunTint: light.color,
    sunDirection: raw.sunDirection,
    mist: BASE_MIST * look.mist * (1 + DAWN_MIST_BOOST * dawn),
    scatter: SUN_SCATTER * raw.daylight * look.sunLight,
  };
}
