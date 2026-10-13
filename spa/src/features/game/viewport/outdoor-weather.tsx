import type {
  FootprintWire,
  NearbyPropSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { OutdoorFog } from './outdoor-fog';
import { OutdoorLighting } from './outdoor-lighting';
import { OutdoorSky } from './outdoor-sky';
import { useSmoothedLook } from './use-smoothed-look';
import type { WeatherLook } from './weather-look';
import { WeatherParticles } from './weather-particles';

export function OutdoorWeather({
  size,
  props,
  target,
}: {
  size: FootprintWire;
  props: NearbyPropSnapshot[];
  target: WeatherLook;
}) {
  const look = useSmoothedLook(target);
  return (
    <>
      <OutdoorSky look={look} />
      <OutdoorFog size={size} look={look} />
      <WeatherParticles look={look} />
      <OutdoorLighting size={size} props={props} look={look} />
    </>
  );
}
