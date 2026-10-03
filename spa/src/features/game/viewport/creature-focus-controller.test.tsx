import { act, fireEvent } from '@testing-library/react';
import { Group, Mesh, BoxGeometry, MeshBasicMaterial, PerspectiveCamera, Scene } from 'three';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { CreatureStatusSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { renderWithProviders } from '@/test/test-utils';

import type { CreatureFocus } from './creature-focus';
import { CreatureFocusController } from './creature-focus-controller';

const runtime = vi.hoisted(() => ({
  camera: null as unknown,
  scene: null as unknown,
  frames: [] as ((_: unknown, dt: number) => void)[],
}));
vi.mock('@react-three/fiber', () => ({
  useThree: (select?: (state: typeof runtime) => unknown) => (select ? select(runtime) : runtime),
  useFrame: (frame: (_: unknown, dt: number) => void) => {
    runtime.frames.push(frame);
  },
}));
beforeEach(() => {
  runtime.camera = new PerspectiveCamera(75, 1, 0.1, 100);
  const scene = new Scene();
  const npc = new Group();
  npc.userData.creatureId = 'npc';
  npc.position.z = -2;
  npc.add(new Mesh(new BoxGeometry(0.6, 1.8, 0.6), new MeshBasicMaterial()));
  scene.add(npc);
  scene.updateMatrixWorld(true);
  runtime.scene = scene;
  runtime.frames = [];
  Object.defineProperty(document, 'pointerLockElement', {
    configurable: true,
    value: document.body,
  });
  document.exitPointerLock = vi.fn();
  vi.stubGlobal('matchMedia', () => ({ matches: false }));
});
const creatures = [{ id: 'npc', placement: { x: 0, y: -2, angle: 0 } }];
const statuses = [{ ...creatures[0], posture: 'Standing' }] as CreatureStatusSnapshot[];
function props() {
  return {
    focus: null as CreatureFocus | null,
    creatures,
    statuses,
    enabled: true,
    onTarget: vi.fn(),
    onFocus: vi.fn(),
    onRestored: vi.fn(),
  };
}
function tick() {
  act(() => {
    for (let i = 0; i < 120; i++) runtime.frames.forEach((frame) => frame(null, 1 / 60));
  });
}

describe('creature focus controller', () => {
  it('captures E and releases pointer lock to open the selected NPC', () => {
    const handlers = props();
    renderWithProviders(<CreatureFocusController {...handlers} />);
    fireEvent.keyDown(window, { code: 'KeyE' });
    expect(handlers.onFocus).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'npc', headHeight: 1.63 }),
    );
    expect(document.exitPointerLock).toHaveBeenCalledOnce();
  });
  it('ignores E during another turn or while typing outside pointer lock', () => {
    const handlers = props();
    const { rerender } = renderWithProviders(
      <CreatureFocusController {...handlers} enabled={false} />,
    );
    fireEvent.keyDown(window, { code: 'KeyE' });
    Object.defineProperty(document, 'pointerLockElement', { configurable: true, value: null });
    rerender(<CreatureFocusController {...handlers} />);
    fireEvent.keyDown(window, { code: 'KeyE' });
    expect(handlers.onFocus).not.toHaveBeenCalled();
  });
  it('zooms toward the face without moving through scenery, then restores the camera', () => {
    const handlers = props();
    const camera = runtime.camera as PerspectiveCamera;
    const initial = camera.quaternion.clone();
    const focus = { id: 'npc', x: 0, y: -2, headHeight: 1.63, facing: Math.PI };
    const { rerender } = renderWithProviders(
      <CreatureFocusController {...handlers} focus={focus} />,
    );
    tick();
    expect(camera.fov).toBeCloseTo(38);
    expect(camera.position.toArray()).toEqual([0, 0, 0]);
    expect(camera.quaternion.angleTo(initial)).toBeGreaterThan(0.1);
    runtime.frames = [];
    rerender(<CreatureFocusController {...handlers} />);
    tick();
    expect(camera.fov).toBe(75);
    expect(camera.quaternion.angleTo(initial)).toBeCloseTo(0);
    expect(handlers.onRestored).toHaveBeenCalledOnce();
  });
});
