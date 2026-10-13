import { PointerLockControls } from '@react-three/drei';
import { useFrame, useThree } from '@react-three/fiber';
import { useEffect, useLayoutEffect, useRef } from 'react';

import type {
  FootprintWire,
  NearbyExitSnapshot,
  PlacementWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { gameEventBus } from '@/lib/game-event-bus';

import { useScene } from '../contexts/scene-context';
import { useChatHub } from '../hooks/use-game-hub-connection';
import { runAction } from '../run-action';
import {
  EYE_HEIGHT,
  type Obstacle,
  type PlanarPoint,
  type WalkerState,
  advanceWalker,
  findConnectorInRange,
  headingToYaw,
  interpolateWalker,
  seedWalker,
  toScenePosition,
  yawToHeading,
} from './layout-math';
import { findSeatInRange, type ViewportSeat } from './seat-interaction';
import { useDebugTeleport } from './use-debug-teleport';
import { type InputFrame, useInputReporter } from './use-input-reporter';
import { useSeatedCamera } from './use-seated-camera';

const FORWARD_KEYS = ['KeyW', 'ArrowUp'];
const BACKWARD_KEYS = ['KeyS', 'ArrowDown'];
const LEFT_KEYS = ['KeyA', 'ArrowLeft'];
const RIGHT_KEYS = ['KeyD', 'ArrowRight'];
const INTERACT_KEY = 'KeyE';
const SNEAK_KEYS = ['ControlLeft', 'ControlRight'];

interface FpsControllerProps {
  walkSpeed: number;
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
  onInputChange: (input: InputFrame) => void;
}

const axis = (keys: Set<string>, positive: string[], negative: string[]) =>
  Number(positive.some((key) => keys.has(key))) - Number(negative.some((key) => keys.has(key)));

export function FpsController({
  walkSpeed,
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
  onInputChange,
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
  const walker = useRef<WalkerState | undefined>(undefined);
  const shown = useRef<PlanarPoint | undefined>(undefined);

  const { scene } = useScene();
  const chatHub = useChatHub();
  const { locationId } = scene;
  const { track: trackInput, halt: haltInput } = useInputReporter(locationId, onInputChange);

  useEffect(
    () =>
      gameEventBus.on('PlayerCorrected', (correction) => {
        if (correction.locationId !== locationId) return;
        const [x, , z] = toScenePosition(correction.x, correction.y);
        camera.position.x = x;
        camera.position.z = z;
      }),
    [camera, locationId],
  );

  useLayoutEffect(() => {
    handlers.current = {
      onEnterConnector,
      onSeatInteraction,
      seated,
      movementLocked,
    };
    latestStart.current = start;
  }, [onEnterConnector, onSeatInteraction, seated, movementLocked, start]);

  // Layout phase so no frame can report the stale seated camera before the snap.
  useLayoutEffect(() => {
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
        void runAction(chatHub.sendToggleSneaking());
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
  }, [chatHub]);

  useFrame((_, deltaSeconds) => {
    if (seated || movementLocked || cameraHeld.current) {
      walker.current = undefined;
      haltInput();
      return;
    }
    const keys = pressed.current;
    const heading = yawToHeading(camera.rotation.y);
    const forward = axis(keys, FORWARD_KEYS, BACKWARD_KEYS);
    const strafe = axis(keys, RIGHT_KEYS, LEFT_KEYS);
    const lastShown = shown.current;
    const cameraMoved = lastShown?.x !== camera.position.x || lastShown?.y !== camera.position.z;
    const state = advanceWalker(
      cameraMoved || !walker.current
        ? seedWalker({ x: camera.position.x, y: camera.position.z })
        : walker.current,
      { heading, forward, strafe, speed: walkSpeed, deltaSeconds },
      obstacles,
      size,
    );
    const view = interpolateWalker(state);
    const next = state.current;
    walker.current = state;
    shown.current = view;
    camera.position.set(view.x, EYE_HEIGHT, view.y);
    trackInput({ forward, strafe, heading, x: next.x, y: next.y }, performance.now());

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
