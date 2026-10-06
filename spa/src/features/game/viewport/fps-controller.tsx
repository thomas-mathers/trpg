import { PointerLockControls } from '@react-three/drei';
import { useFrame, useThree } from '@react-three/fiber';
import { useEffect, useRef } from 'react';

import { toggleCreatureSneaking } from '@/api/client';
import type {
  FootprintWire,
  NearbyExitSnapshot,
  PlacementWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { useScene } from '../contexts/scene-context';
import {
  EYE_HEIGHT,
  type Obstacle,
  clampToBounds,
  computeMovement,
  findConnectorInRange,
  headingToYaw,
  pushOutOfObstacles,
  toScenePosition,
  walkSpeedFor,
  yawToHeading,
} from './layout-math';
import { findSeatInRange, type ViewportSeat } from './seat-interaction';
import { useDebugTeleport } from './use-debug-teleport';
import { usePoseReporter } from './use-pose-reporter';
import { useSeatedCamera } from './use-seated-camera';

const MAX_FRAME_SECONDS = 0.1;

const FORWARD_KEYS = ['KeyW', 'ArrowUp'];
const BACKWARD_KEYS = ['KeyS', 'ArrowDown'];
const LEFT_KEYS = ['KeyA', 'ArrowLeft'];
const RIGHT_KEYS = ['KeyD', 'ArrowRight'];
const INTERACT_KEY = 'KeyE';
const SNEAK_KEYS = ['ControlLeft', 'ControlRight'];

interface FpsControllerProps {
  movementSpeed: number;
  movementLocked?: boolean;
  seats: ViewportSeat[];
  seated: boolean;
  seatedPlacement?: PlacementWire;
  onNearbySeatChange: (seat: ViewportSeat | undefined) => void;
  onSeatInteraction: (seat: ViewportSeat | undefined) => void;
  size: FootprintWire;
  start: PlacementWire;
  obstacles: Obstacle[];
  connectors: NearbyExitSnapshot[];
  lockSelector: string;
  onLockChange: (locked: boolean) => void;
  onNearbyConnectorChange: (connector: NearbyExitSnapshot | undefined) => void;
  onEnterConnector: (connector: NearbyExitSnapshot) => void;
  onPoseChange: (pose: PlacementWire) => void;
}

const axis = (keys: Set<string>, positive: string[], negative: string[]) =>
  Number(positive.some((key) => keys.has(key))) - Number(negative.some((key) => keys.has(key)));

export function FpsController({
  movementSpeed,
  movementLocked = false,
  seats,
  seated,
  seatedPlacement,
  onNearbySeatChange,
  onSeatInteraction,
  size,
  start,
  obstacles,
  connectors,
  lockSelector,
  onLockChange,
  onNearbyConnectorChange,
  onEnterConnector,
  onPoseChange,
}: FpsControllerProps) {
  const camera = useThree((state) => state.camera);
  const pressed = useRef(new Set<string>());
  const nearbyConnector = useRef<NearbyExitSnapshot | undefined>(undefined);
  const nearbySeat = useRef<ViewportSeat | undefined>(undefined);
  const handlers = useRef({
    onEnterConnector,
    onSeatInteraction,
    seated,
    movementLocked,
  });
  const latestStart = useRef(start);

  const { scene, setMovementSpeed } = useScene();
  const { locationId } = scene;
  const trackPose = usePoseReporter(locationId, onPoseChange);

  useEffect(() => {
    handlers.current = {
      onEnterConnector,
      onSeatInteraction,
      seated,
      movementLocked,
    };
    latestStart.current = start;
  }, [onEnterConnector, onSeatInteraction, seated, movementLocked, start]);

  useEffect(() => {
    camera.rotation.order = 'YXZ';
    if (seated) return;
    const { x, y, angle } = latestStart.current;
    camera.position.set(...toScenePosition(x, y, EYE_HEIGHT));
    camera.rotation.set(0, headingToYaw(angle), 0);
  }, [camera, locationId, seated]);

  useSeatedCamera(camera, seated, seatedPlacement);
  const cameraHeld = useDebugTeleport();

  useEffect(() => {
    const keys = pressed.current;

    const onKeyDown = (event: KeyboardEvent) => {
      if (!document.pointerLockElement || handlers.current.movementLocked) {
        return;
      }

      if (event.code === INTERACT_KEY && !event.repeat) {
        if (handlers.current.seated || nearbySeat.current) {
          handlers.current.onSeatInteraction(nearbySeat.current);
        } else if (nearbyConnector.current) {
          handlers.current.onEnterConnector(nearbyConnector.current);
        }
        return;
      }

      if (SNEAK_KEYS.includes(event.code)) {
        toggleCreatureSneaking({ path: { creatureId: scene.playerStatus.id } }).then((response) => {
          if (!response.data) {
            return;
          }

          const { movementSpeed } = response.data;

          setMovementSpeed(movementSpeed);
        });
      }

      keys.add(event.code);
    };

    const onKeyUp = (event: KeyboardEvent) => keys.delete(event.code);

    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);

    return () => {
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('keyup', onKeyUp);
    };
  }, [scene.playerStatus.id, setMovementSpeed]);

  useFrame((_, deltaSeconds) => {
    if (seated || movementLocked || cameraHeld.current) return;
    const keys = pressed.current;
    const delta = computeMovement({
      heading: yawToHeading(camera.rotation.y),
      forward: axis(keys, FORWARD_KEYS, BACKWARD_KEYS),
      strafe: axis(keys, RIGHT_KEYS, LEFT_KEYS),
      speed: walkSpeedFor(movementSpeed),
      deltaSeconds: Math.min(deltaSeconds, MAX_FRAME_SECONDS),
    });
    const attempted = { x: camera.position.x + delta.x, y: camera.position.z + delta.y };
    const next = clampToBounds(pushOutOfObstacles(attempted, obstacles), size);
    camera.position.set(next.x, EYE_HEIGHT, next.y);
    trackPose({ ...next, angle: yawToHeading(camera.rotation.y) }, performance.now());

    const seat = findSeatInRange(next, seats);
    if (
      seat?.id !== nearbySeat.current?.id ||
      seat?.isOccupied !== nearbySeat.current?.isOccupied
    ) {
      nearbySeat.current = seat;
      onNearbySeatChange(seat);
    }
    const nearest = findConnectorInRange(next, connectors);
    if (nearest?.connectorId !== nearbyConnector.current?.connectorId) {
      nearbyConnector.current = nearest;
      onNearbyConnectorChange(nearest);
    }
  });

  return (
    <PointerLockControls
      enabled={!movementLocked}
      selector={lockSelector}
      onLock={() => onLockChange(true)}
      onUnlock={() => {
        pressed.current.clear();
        onLockChange(false);
      }}
    />
  );
}
