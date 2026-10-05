import { useFrame } from '@react-three/fiber';
import { useEffect, useMemo } from 'react';
import { SkyMesh } from 'three/addons/objects/SkyMesh.js';

import { useGameClock } from '@/features/game/hooks/use-game-clock';

import { SUN_DIRECTION } from './outdoor-lighting';

export function createOutdoorSky() {
  const mesh = new SkyMesh();
  mesh.scale.setScalar(1000);
  mesh.frustumCulled = false;
  mesh.sunPosition.value.copy(SUN_DIRECTION).negate();
  mesh.turbidity.value = 3;
  mesh.rayleigh.value = 1.8;
  mesh.material.colorNode = mesh.material.colorNode!.mul(0.05);
  return mesh;
}

export function OutdoorSky() {
  const gameTime = useGameClock();
  const isNight = gameTime !== undefined && (gameTime.hour < 6 || gameTime.hour >= 19);
  const sky = useMemo(createOutdoorSky, []);

  useEffect(
    () => () => {
      sky.geometry.dispose();
      sky.material.dispose();
    },
    [sky],
  );

  useFrame(({ camera }) => {
    sky.position.copy(camera.position);
  });

  return isNight ? <color attach="background" args={['#14202b']} /> : <primitive object={sky} />;
}
