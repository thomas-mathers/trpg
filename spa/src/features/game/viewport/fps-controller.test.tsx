import { act, fireEvent } from '@testing-library/react';
import { PerspectiveCamera } from 'three';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '@/test/test-utils';

import { FpsController } from './fps-controller';
import { BASE_MOVEMENT_SPEED, EYE_HEIGHT } from './layout-math';
import { SEATED_EYE_HEIGHT, type ViewportSeat } from './seat-interaction';

const renderer = vi.hoisted(() => ({
  frame: (_: unknown, _delta: number) => {},
  camera: null as unknown,
}));
vi.mock('@react-three/fiber', () => ({
  useThree: (select: (state: { camera: unknown }) => unknown) =>
    select({ camera: renderer.camera }),
  useFrame: (frame: typeof renderer.frame) => {
    renderer.frame = frame;
  },
}));
vi.mock('@react-three/drei', () => ({ PointerLockControls: () => null }));
const chair: ViewportSeat = {
  id: 'chair',
  name: 'Chair',
  model: 'SeatChair',
  placement: { x: 5, y: 5, angle: 0 },
  footprint: { width: 0.6, depth: 0.6 },
  isOccupied: false,
  isOccupiedByPlayer: false,
};
function setup() {
  const props = {
    movementSpeed: BASE_MOVEMENT_SPEED,
    size: { width: 20, depth: 20 },
    start: { x: 5, y: 6, angle: 0 },
    seats: [chair],
    seated: false,
    obstacles: [chair],
    connectors: [],
    lockSelector: '#canvas',
    onLockChange: vi.fn(),
    onNearbyConnectorChange: vi.fn(),
    onEnterConnector: vi.fn(),
    onNearbySeatChange: vi.fn(),
    onSeatInteraction: vi.fn(),
    onChatRequested: vi.fn(),
  };
  return { props, ...renderWithProviders(<FpsController {...props} />) };
}
beforeEach(() => {
  renderer.camera = new PerspectiveCamera();
  Object.defineProperty(document, 'pointerLockElement', {
    configurable: true,
    value: document.body,
  });
});
describe('chair interaction', () => {
  it('offers E only within walking reach and ignores held E repeats', () => {
    const { props } = setup();
    act(() => renderer.frame(null, 0.1));
    fireEvent.keyDown(window, { code: 'KeyE' });
    fireEvent.keyDown(window, { code: 'KeyE', repeat: true });
    expect(props.onSeatInteraction).toHaveBeenCalledExactlyOnceWith(chair);
    (renderer.camera as PerspectiveCamera).position.set(10, EYE_HEIGHT, 10);
    act(() => renderer.frame(null, 0.1));
    fireEvent.keyDown(window, { code: 'KeyE' });
    expect(props.onSeatInteraction).toHaveBeenCalledTimes(1);
    expect(props.onNearbySeatChange).toHaveBeenLastCalledWith(undefined);
  });
  it('locks walking in the chair, uses E to stand, then restores walking', () => {
    const { props, rerender } = setup();
    const camera = renderer.camera as PerspectiveCamera;
    rerender(<FpsController {...props} seated seatedPlacement={chair.placement} />);
    fireEvent.keyDown(window, { code: 'KeyW' });
    act(() => renderer.frame(null, 0.1));
    expect(camera.position.toArray()).toEqual([5, SEATED_EYE_HEIGHT, 5]);
    fireEvent.keyDown(window, { code: 'KeyE' });
    expect(props.onSeatInteraction).toHaveBeenCalledOnce();
    expect(props.onEnterConnector).not.toHaveBeenCalled();
    rerender(<FpsController {...props} />);
    expect(camera.position.toArray()).toEqual([5, EYE_HEIGHT, 6]);
    act(() => renderer.frame(null, 0.1));
    expect(camera.position.z).toBeLessThan(6);
  });
  it('stands back where the player sat down when the server pose follows the seat', () => {
    const { props, rerender } = setup();
    const camera = renderer.camera as PerspectiveCamera;
    rerender(
      <FpsController {...props} seated seatedPlacement={chair.placement} start={chair.placement} />,
    );
    rerender(<FpsController {...props} />);
    expect(camera.position.toArray()).toEqual([5, EYE_HEIGHT, 6]);
  });
  it('ignores interaction keys while typing outside pointer lock', () => {
    const { props } = setup();
    act(() => renderer.frame(null, 0.1));
    Object.defineProperty(document, 'pointerLockElement', { configurable: true, value: null });
    fireEvent.keyDown(window, { code: 'KeyE' });
    expect(props.onSeatInteraction).not.toHaveBeenCalled();
  });
});
