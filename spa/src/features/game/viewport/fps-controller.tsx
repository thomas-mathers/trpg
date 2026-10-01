import { PointerLockControls } from '@react-three/drei';
import { useFrame, useThree } from '@react-three/fiber';
import { useEffect, useRef } from 'react';

import type {
  ConnectorLayoutWire,
  FootprintWire,
  PlacementWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import {
  EYE_HEIGHT,
  type Obstacle,
  WALK_SPEED,
  clampToBounds,
  computeMovement,
  findConnectorInRange,
  headingToYaw,
  pushOutOfObstacles,
  toScenePosition,
  yawToHeading,
} from './layout-math';
import { findSeatInRange, type ViewportSeat } from './seat-interaction';
import { useSeatedCamera } from './use-seated-camera';

const MAX_FRAME_SECONDS = 0.1;

const FORWARD_KEYS = ['KeyW', 'ArrowUp'];
const BACKWARD_KEYS = ['KeyS', 'ArrowDown'];
const LEFT_KEYS = ['KeyA', 'ArrowLeft'];
const RIGHT_KEYS = ['KeyD', 'ArrowRight'];
const INTERACT_KEY = 'KeyE';

interface FpsControllerProps {
  movementLocked?: boolean;
  seats: ViewportSeat[];
  seated: boolean;
  seatedPlacement?: PlacementWire;
  onNearbySeatChange: (seat: ViewportSeat | undefined) => void;
  onSeatInteraction: (seat: ViewportSeat | undefined) => void;
  size: FootprintWire;
  start: PlacementWire;
  obstacles: Obstacle[];
  connectors: ConnectorLayoutWire[];
  lockSelector: string;
  onLockChange: (locked: boolean) => void;
  onNearbyConnectorChange: (connector: ConnectorLayoutWire | undefined) => void;
  onEnterConnector: (connector: ConnectorLayoutWire) => void;
}

const axis = (keys: Set<string>, positive: string[], negative: string[]) =>
  Number(positive.some((key) => keys.has(key))) - Number(negative.some((key) => keys.has(key)));

export function FpsController({
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
}: FpsControllerProps) {
  const camera = useThree((state) => state.camera);
  const pressed = useRef(new Set<string>());
  const nearbyConnector = useRef<ConnectorLayoutWire | undefined>(undefined);
  const nearbySeat = useRef<ViewportSeat | undefined>(undefined);
  const handlers = useRef({
    onEnterConnector,
    onSeatInteraction,
    seated,
    movementLocked,
  });
  const { x, y, angle } = start;

  useEffect(() => {
    handlers.current = {
      onEnterConnector,
      onSeatInteraction,
      seated,
      movementLocked,
    };
  }, [onEnterConnector, onSeatInteraction, seated, movementLocked]);

  useEffect(() => {
    camera.rotation.order = 'YXZ';
    if (seated) return;
    camera.position.set(...toScenePosition(x, y, EYE_HEIGHT));
    camera.rotation.set(0, headingToYaw(angle), 0);
  }, [camera, x, y, angle, seated]);

  useSeatedCamera(camera, seated, seatedPlacement, start, obstacles, size);

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
      keys.add(event.code);
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
    if (seated || movementLocked) return;
    const keys = pressed.current;
    const delta = computeMovement({
      heading: yawToHeading(camera.rotation.y),
      forward: axis(keys, FORWARD_KEYS, BACKWARD_KEYS),
      strafe: axis(keys, RIGHT_KEYS, LEFT_KEYS),
      speed: WALK_SPEED,
      deltaSeconds: Math.min(deltaSeconds, MAX_FRAME_SECONDS),
    });
    const attempted = { x: camera.position.x + delta.x, y: camera.position.z + delta.y };
    const next = clampToBounds(pushOutOfObstacles(attempted, obstacles), size);
    camera.position.set(next.x, EYE_HEIGHT, next.y);

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
