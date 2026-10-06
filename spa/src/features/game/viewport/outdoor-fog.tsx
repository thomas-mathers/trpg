import { useEffect, useMemo } from 'react';
import { Fog } from 'three';

import type { FootprintWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { useSceneHour } from './scene-hour';
import { skyStateAt } from './sky-state';
import { applyWeather, type WeatherLook } from './weather-look';

const FOG_NEAR_CAP = 100;
const FOG_SPAN = 150;
const FOG_CLEARANCE = 0;

export function outdoorFogRange({ width, depth }: FootprintWire) {
  const near = Math.min(FOG_NEAR_CAP, Math.max(width, depth) + FOG_CLEARANCE);
  return { near, far: near + FOG_SPAN };
}

export function OutdoorFog({ size, look }: { size: FootprintWire; look: WeatherLook }) {
  const { near: baseNear, far: baseFar } = outdoorFogRange(size);
  const near = baseNear * look.fogScale;
  const far = baseFar * look.fogScale;
  const hour = useSceneHour();
  const { fogColor } = useMemo(() => applyWeather(skyStateAt(hour), look), [hour, look]);
  const fog = useMemo(() => new Fog(fogColor, near, far), []);

  useEffect(() => {
    fog.color.copy(fogColor);
    fog.near = near;
    fog.far = far;
  }, [fog, fogColor, near, far]);

  return <primitive object={fog} attach="fog" />;
}
