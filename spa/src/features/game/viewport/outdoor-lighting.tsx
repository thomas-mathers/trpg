import { useFrame, useThree } from '@react-three/fiber';
import { useEffect, useMemo } from 'react';
import { CSMShadowNode } from 'three/addons/csm/CSMShadowNode.js';
import { DirectionalLight, Vector3 } from 'three/webgpu';

import type {
  FootprintWire,
  NearbyPropSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { useGameClock } from '@/features/game/hooks/use-game-clock';

import { toScenePosition } from './layout-math';

export const SUN_DIRECTION = new Vector3(-1, -2, 1).normalize();

export function createOutdoorSun(maxFar: number) {
  const light = new DirectionalLight(0xffffff, 1.5);
  light.position.copy(SUN_DIRECTION).multiplyScalar(-200);
  light.castShadow = true;
  light.shadow.mapSize.set(2048, 2048);
  light.shadow.camera.near = 0.1;
  light.shadow.camera.far = 500;
  light.shadow.normalBias = 0.025;
  light.shadow.radius = 3;
  const shadowNode = new CSMShadowNode(light, { cascades: 3, maxFar, lightMargin: 50 });
  shadowNode.fade = true;
  (light.shadow as typeof light.shadow & { shadowNode: CSMShadowNode }).shadowNode = shadowNode;
  return { light, shadowNode };
}

export function OutdoorLighting({
  size: { width, depth },
  height,
  props,
}: {
  size: FootprintWire;
  height: number;
  props: NearbyPropSnapshot[];
}) {
  const gameTime = useGameClock();
  const isNight = gameTime !== undefined && (gameTime.hour < 6 || gameTime.hour >= 19);
  const camera = useThree((state) => state.camera);
  const viewport = useThree((state) => state.size);
  const maxFar = Math.min(camera.far, Math.hypot(width, depth, height) + 20);
  const { light, shadowNode } = useMemo(() => createOutdoorSun(maxFar), [maxFar]);

  useEffect(() => {
    if (shadowNode.camera) shadowNode.updateFrustums();
  }, [shadowNode, viewport.width, viewport.height]);

  useEffect(() => {
    light.intensity = isNight ? 0.08 : 1.5;
  }, [light, isNight]);

  useFrame(() => {
    camera.updateMatrixWorld();
  });

  return (
    <>
      <ambientLight intensity={isNight ? 0.2 : 0.5} />
      <primitive object={light} />
      <primitive object={light.target} />
      {isNight &&
        props
          .filter(
            ({ model }) => model === 'FurnitureStreetLantern' || model === 'FurnitureWallLantern',
          )
          .map(({ id, model, placement }) => (
            <pointLight
              key={id}
              position={toScenePosition(
                placement.x,
                placement.y,
                model === 'FurnitureStreetLantern' ? 3 : 2.1,
              )}
              color="#ffca76"
              intensity={8}
              distance={11}
              decay={2}
            />
          ))}
    </>
  );
}
