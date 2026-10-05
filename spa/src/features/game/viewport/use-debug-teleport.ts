import { useThree } from '@react-three/fiber';
import { type RefObject, useEffect, useRef } from 'react';
import { PerspectiveCamera } from 'three';

import { useScene } from '../contexts/scene-context';
import { EYE_HEIGHT, headingToYaw, toScenePosition } from './layout-math';

declare global {
  interface Window {
    teleport?: (
      x: number,
      y: number,
      headingDegrees?: number,
      pitchDegrees?: number,
      height?: number,
    ) => void;
    overview?: (zoomOut?: number) => void;
    debugScene?: unknown;
  }
}

const HALF_FOV_TANGENT = Math.tan((75 / 2) * (Math.PI / 180));

export function useDebugTeleport(): RefObject<boolean> {
  const held = useRef(false);
  const camera = useThree((state) => state.camera);
  const sceneGraph = useThree((state) => state.scene);
  const { scene } = useScene();
  const { width, depth } = scene.size;

  useEffect(() => {
    if (!import.meta.env.DEV) return;

    const teleport: NonNullable<Window['teleport']> = (
      x,
      y,
      headingDegrees = 0,
      pitchDegrees = 0,
      height = EYE_HEIGHT,
    ) => {
      held.current = height !== EYE_HEIGHT;
      camera.position.set(...toScenePosition(x, y, height));
      camera.rotation.set(
        (pitchDegrees * Math.PI) / 180,
        headingToYaw((headingDegrees * Math.PI) / 180),
        0,
      );
    };

    window.debugScene = scene;
    window.teleport = teleport;
    window.overview = (zoomOut = 1) => {
      const aspect = camera instanceof PerspectiveCamera ? camera.aspect : 1;
      const height = (zoomOut * Math.max(depth, width / aspect)) / 2 / HALF_FOV_TANGENT;
      sceneGraph.fog = null;
      teleport(width / 2, depth / 2, 0, -90, height);
    };

    return () => {
      delete window.debugScene;
      delete window.teleport;
      delete window.overview;
    };
  }, [camera, sceneGraph, scene, width, depth]);

  return held;
}
