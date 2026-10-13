import { useEffect, useMemo } from 'react';

import type { FootprintWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { createOutdoorFog } from './outdoor-fog-node';
import { outdoorFogStateAt } from './outdoor-fog-state';
import { useSceneHour } from './scene-hour';
import type { WeatherLook } from './weather-look';

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
  const state = useMemo(() => outdoorFogStateAt(hour, look), [hour, look]);
  const fog = useMemo(createOutdoorFog, []);

  useEffect(() => {
    fog.color.value.copy(state.color);
    fog.sunTint.value.copy(state.sunTint);
    fog.sunDirection.value.copy(state.sunDirection);
    fog.mist.value = state.mist;
    fog.scatter.value = state.scatter;
    fog.near.value = near;
    fog.far.value = far;
  }, [fog, state, near, far]);

  return <primitive object={fog.node} attach="fogNode" />;
}
