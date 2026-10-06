import { useMemo } from 'react';

import type {
  FootprintWire,
  NearbyPropSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { LightPool } from './light-pool';
import { indoorAmbientAt, lightSourcesFor, shadowReach } from './light-rig';
import { useSceneHour } from './scene-hour';

export function IndoorLighting({
  size,
  props,
}: {
  size: FootprintWire;
  props: NearbyPropSnapshot[];
}) {
  const hour = useSceneHour();
  const sources = useMemo(() => lightSourcesFor(props), [props]);
  const ambient = useMemo(() => indoorAmbientAt(hour), [hour]);

  return (
    <>
      <hemisphereLight
        color={ambient.sky}
        groundColor={ambient.ground}
        intensity={ambient.intensity}
      />
      <LightPool
        sources={sources}
        hour={hour}
        shadowSlots={1}
        shadowFar={shadowReach(size)}
        decay={0.85}
      />
    </>
  );
}
