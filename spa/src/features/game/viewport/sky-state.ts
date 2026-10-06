import { Color, MathUtils, Vector3 } from 'three';

export const NIGHT_SKY_COLOR = '#14202b';
export const HORIZON_COLOR = '#a3b5c6';

const SUNRISE_HOUR = 6;
const SUNSET_HOUR = 19;
const MAX_ELEVATION = MathUtils.degToRad(62);

const SUN_LOW = new Color('#ff9f5a');
const SUN_HIGH = new Color('#fff4e0');
const MOON = new Color('#9db4e0');
const SKY_DAY = new Color('#bcd4f0');
const SKY_NIGHT = new Color('#2a3b5c');
const GROUND_DAY = new Color('#8a7a62');
const GROUND_NIGHT = new Color('#16161e');
const HORIZON_DUSK = new Color('#d9a07a');

export interface SkyState {
  sunDirection: Vector3;
  isNight: boolean;
  daylight: number;
  twilight: number;
  light: { direction: Vector3; color: Color; intensity: number };
  ambient: { sky: Color; ground: Color; intensity: number };
  fogColor: Color;
}

function dayAngle(hour: number) {
  const wrapped = ((hour % 24) + 24) % 24;
  if (wrapped >= SUNRISE_HOUR && wrapped < SUNSET_HOUR) {
    return (Math.PI * (wrapped - SUNRISE_HOUR)) / (SUNSET_HOUR - SUNRISE_HOUR);
  }
  const sinceSunset = (wrapped - SUNSET_HOUR + 24) % 24;
  return Math.PI + (Math.PI * sinceSunset) / (24 - (SUNSET_HOUR - SUNRISE_HOUR));
}

export function sunDirectionAt(hour: number) {
  const angle = dayAngle(hour);
  const rise = Math.sin(angle);
  return new Vector3(
    Math.cos(angle),
    rise * Math.sin(MAX_ELEVATION),
    rise * Math.cos(MAX_ELEVATION),
  );
}

export function skyStateAt(hour: number): SkyState {
  const sunDirection = sunDirectionAt(hour);
  const elevation = sunDirection.y;
  const daylight = MathUtils.smoothstep(elevation, -0.08, 0.25);
  const twilight = Math.exp(-((elevation / 0.16) ** 2));
  const isNight = elevation < -0.06;

  const sunLight = SUN_LOW.clone().lerp(SUN_HIGH, MathUtils.smoothstep(elevation, 0, 0.5));
  const sunIntensity = 1.5 * MathUtils.smoothstep(elevation, 0, 0.3);
  const moonIntensity = 0.2 * MathUtils.smoothstep(-elevation, 0, 0.3);
  const isSunUp = elevation >= 0;

  return {
    sunDirection,
    isNight,
    daylight,
    twilight,
    light: {
      direction: isSunUp ? sunDirection : sunDirection.clone().negate(),
      color: isSunUp ? sunLight : MOON.clone(),
      intensity: isSunUp ? sunIntensity : moonIntensity,
    },
    ambient: {
      sky: SKY_NIGHT.clone().lerp(SKY_DAY, daylight),
      ground: GROUND_NIGHT.clone().lerp(GROUND_DAY, daylight),
      intensity: MathUtils.lerp(0.4, 0.55, daylight),
    },
    fogColor: new Color(NIGHT_SKY_COLOR)
      .lerp(new Color(HORIZON_COLOR), daylight)
      .lerp(HORIZON_DUSK, twilight * 0.7),
  };
}
