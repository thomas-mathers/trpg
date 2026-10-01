import { PointerLockControls } from '@react-three/drei';
import { useFrame, useThree } from '@react-three/fiber';
import { useEffect, useRef } from 'react';

import type {
  FootprintWire,
  PlacementWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import {
  EYE_HEIGHT,
  WALK_SPEED,
  clampToBounds,
  computeMovement,
  headingToYaw,
  toScenePosition,
  yawToHeading,
} from './layout-math';

const MAX_FRAME_SECONDS = 0.1;

const FORWARD_KEYS = ['KeyW', 'ArrowUp'];
const BACKWARD_KEYS = ['KeyS', 'ArrowDown'];
const LEFT_KEYS = ['KeyA', 'ArrowLeft'];
const RIGHT_KEYS = ['KeyD', 'ArrowRight'];

interface FpsControllerProps {
  size: FootprintWire;
  start: PlacementWire;
  lockSelector: string;
  onLockChange: (locked: boolean) => void;
}

const axis = (keys: Set<string>, positive: string[], negative: string[]) =>
  Number(positive.some((key) => keys.has(key))) - Number(negative.some((key) => keys.has(key)));

export function FpsController({ size, start, lockSelector, onLockChange }: FpsControllerProps) {
  const camera = useThree((state) => state.camera);
  const pressed = useRef(new Set<string>());
  const { x, y, angle } = start;

  useEffect(() => {
    camera.rotation.order = 'YXZ';
    camera.position.set(...toScenePosition(x, y, EYE_HEIGHT));
    camera.rotation.set(0, headingToYaw(angle), 0);
  }, [camera, x, y, angle]);

  useEffect(() => {
    const keys = pressed.current;
    const onKeyDown = (event: KeyboardEvent) => {
      if (document.pointerLockElement) {
        keys.add(event.code);
      }
    };
    const onKeyUp = (event: KeyboardEvent) => keys.delete(event.code);

    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);
    return () => {
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('keyup', onKeyUp);
    };
  }, []);

  useFrame((_, deltaSeconds) => {
    const keys = pressed.current;
    const delta = computeMovement({
      heading: yawToHeading(camera.rotation.y),
      forward: axis(keys, FORWARD_KEYS, BACKWARD_KEYS),
      strafe: axis(keys, RIGHT_KEYS, LEFT_KEYS),
      speed: WALK_SPEED,
      deltaSeconds: Math.min(deltaSeconds, MAX_FRAME_SECONDS),
    });
    const next = clampToBounds(
      { x: camera.position.x + delta.x, y: camera.position.z + delta.y },
      size,
    );
    camera.position.set(next.x, EYE_HEIGHT, next.y);
  });

  return (
    <PointerLockControls
      selector={lockSelector}
      onLock={() => onLockChange(true)}
      onUnlock={() => {
        pressed.current.clear();
        onLockChange(false);
      }}
    />
  );
}
