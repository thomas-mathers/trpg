import { useFrame, useThree } from '@react-three/fiber';
import { useMemo, useRef } from 'react';
import { Vector3 } from 'three';
import { PointLight } from 'three/webgpu';

import { LIGHT_SLOTS, type LightSource, lightRig } from './light-rig';

const SHADOW_MAP_SIZE = 512;
const direction = new Vector3();

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
  const previous = useRef<{
    sources: LightSource[];
    hour: number;
    x: number;
    z: number;
    forwardX: number;
    forwardZ: number;
    lights: PointLight[];
  }>(undefined);

  useFrame(() => {
    const { x, z } = camera.position;
    camera.getWorldDirection(direction);
    const heading = Math.hypot(direction.x, direction.z) || 1;
    const forward = { x: direction.x / heading, z: direction.z / heading };
    const last = previous.current;
    if (
      last?.sources === sources &&
      last.hour === hour &&
      last.x === x &&
      last.z === z &&
      last.forwardX === forward.x &&
      last.forwardZ === forward.z &&
      last.lights === lights
    )
      return;
    previous.current = { sources, hour, x, z, forwardX: forward.x, forwardZ: forward.z, lights };
    const rig = lightRig(sources, hour, { x, z, forward });
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
