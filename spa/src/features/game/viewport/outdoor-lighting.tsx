import { useFrame, useThree } from '@react-three/fiber';
import { useEffect, useMemo, useRef } from 'react';
import { MathUtils } from 'three';
import { CSMShadowNode } from 'three/addons/csm/CSMShadowNode.js';
import { DirectionalLight, type HemisphereLight } from 'three/webgpu';

import type {
  FootprintWire,
  NearbyPropSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { LightPool } from './light-pool';
import { lightSourcesFor } from './light-rig';
import { outdoorFogRange } from './outdoor-fog';
import { useSceneHour } from './scene-hour';
import { skyStateAt } from './sky-state';
import { applyWeather, lightningFlashAt, type WeatherLook } from './weather-look';

const LIGHT_DISTANCE = 200;
const LANTERN_REACH = 20;
const LIGHTNING_AMBIENT = 2.5;
const CASCADES = 3;
const SHADOW_MAP_SIZE = 2048;
const SHADOW_CASTER_RANGE = 300;
const LIGHT_MARGIN = 30;
const SPLIT_BLEND = 0.7;

export function blendedSplitBreaks(
  cascades: number,
  near: number,
  far: number,
  blend: number,
  target: number[],
) {
  for (let index = 1; index <= cascades; index++) {
    const uniform = near + ((far - near) * index) / cascades;
    const logarithmic = near * (far / near) ** (index / cascades);
    target.push(MathUtils.lerp(uniform, logarithmic, blend) / far);
  }
  target[cascades - 1] = 1;
}

export function createOutdoorSun(maxFar: number) {
  const light = new DirectionalLight(0xffffff, 1.5);
  light.castShadow = true;
  light.shadow.mapSize.set(SHADOW_MAP_SIZE, SHADOW_MAP_SIZE);
  light.shadow.camera.near = 0.1;
  light.shadow.camera.far = SHADOW_CASTER_RANGE;
  light.shadow.normalBias = 0.025;
  light.shadow.radius = 3;
  const shadowNode = new CSMShadowNode(light, {
    cascades: CASCADES,
    maxFar,
    lightMargin: LIGHT_MARGIN,
  });
  shadowNode.fade = true;
  shadowNode.mode = 'custom';
  shadowNode.customSplitsCallback = (cascades, near, far, target) =>
    blendedSplitBreaks(cascades, near, far, SPLIT_BLEND, target);
  (light.shadow as typeof light.shadow & { shadowNode: CSMShadowNode }).shadowNode = shadowNode;
  return { light, shadowNode };
}

export function OutdoorLighting({
  size,
  props,
  look,
}: {
  size: FootprintWire;
  props: NearbyPropSnapshot[];
  look: WeatherLook;
}) {
  const hour = useSceneHour();
  const { light: lightState, ambient } = useMemo(
    () => applyWeather(skyStateAt(hour), look),
    [hour, look],
  );
  const hemisphere = useRef<HemisphereLight>(null);
  const lanterns = useMemo(() => lightSourcesFor(props), [props]);
  const camera = useThree((state) => state.camera);
  const viewport = useThree((state) => state.size);
  const maxFar = outdoorFogRange(size).near * look.fogScale;
  const { light, shadowNode } = useMemo(() => createOutdoorSun(maxFar), [maxFar]);

  useEffect(() => () => shadowNode.dispose(), [shadowNode]);

  useEffect(() => {
    if (shadowNode.camera) shadowNode.updateFrustums();
  }, [shadowNode, viewport.width, viewport.height]);

  useEffect(() => {
    light.position.copy(lightState.direction).multiplyScalar(LIGHT_DISTANCE);
    light.color.copy(lightState.color);
    light.intensity = lightState.intensity;
  }, [light, lightState]);

  useFrame(({ clock }) => {
    camera.updateMatrixWorld();
    if (hemisphere.current) {
      const flash = look.lightning ? lightningFlashAt(clock.elapsedTime) : 0;
      hemisphere.current.intensity = ambient.intensity + flash * LIGHTNING_AMBIENT;
    }
  });

  return (
    <>
      <hemisphereLight
        ref={hemisphere}
        color={ambient.sky}
        groundColor={ambient.ground}
        intensity={ambient.intensity}
      />
      <primitive object={light} />
      <primitive object={light.target} />
      <LightPool
        sources={lanterns}
        hour={hour}
        shadowSlots={0}
        distance={LANTERN_REACH}
        decay={1}
      />
    </>
  );
}
