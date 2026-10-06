import { useFrame, useThree } from '@react-three/fiber';
import { useMemo } from 'react';
import { PointLight } from 'three/webgpu';

import { LIGHT_SLOTS, type LightSource, lightRig } from './light-rig';

const SHADOW_MAP_SIZE = 512;

interface LightFalloff {
  shadowSlots: number;
  shadowFar?: number;
  decay: number;
  distance?: number;
}

function createSlotLight(
  { shadowFar = 0, decay, distance = 0 }: Omit<LightFalloff, 'shadowSlots'>,
  castShadow: boolean,
) {
  const light = new PointLight('#ffb56a', 0, distance, decay);
  light.castShadow = castShadow;
  if (castShadow) {
    light.shadow.mapSize.set(SHADOW_MAP_SIZE, SHADOW_MAP_SIZE);
    light.shadow.camera.near = 0.1;
    light.shadow.camera.far = shadowFar;
    light.shadow.camera.updateProjectionMatrix();
    light.shadow.normalBias = 0.02;
    light.shadow.radius = 3;
  }
  return light;
}

export function LightPool({
  sources,
  hour,
  shadowSlots,
  shadowFar,
  decay,
  distance,
}: LightFalloff & { sources: LightSource[]; hour: number }) {
  const camera = useThree((state) => state.camera);
  const lights = useMemo(
    () =>
      Array.from({ length: LIGHT_SLOTS }, (_, slot) =>
        createSlotLight({ shadowFar, decay, distance }, slot < shadowSlots),
      ),
    [shadowSlots, shadowFar, decay, distance],
  );

  useFrame(() => {
    const rig = lightRig(sources, hour, { x: camera.position.x, z: camera.position.z });
    rig.forEach((state, slot) => {
      const light = lights[slot]!;
      light.position.set(state.x, state.y, state.z);
      light.color.set(state.color);
      light.intensity = state.intensity;
      light.shadow.autoUpdate = state.intensity > 0;
    });
  });

  return (
    <>
      {lights.map((light) => (
        <primitive key={light.uuid} object={light} />
      ))}
    </>
  );
}
