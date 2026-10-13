import { act } from '@testing-library/react';
import { PerspectiveCamera } from 'three';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '@/test/test-utils';

import { LightPool } from './light-pool';
import * as lighting from './light-rig';

const runtime = vi.hoisted(() => ({ camera: null as unknown, frame: () => {} }));
vi.mock('@react-three/fiber', async () => {
  const { useLayoutEffect } = await import('react');
  return {
    useThree: (select: (state: typeof runtime) => unknown) => select(runtime),
    useFrame: (frame: () => void) => {
      useLayoutEffect(() => {
        runtime.frame = frame;
      });
    },
  };
});

const source = {
  id: 'lantern',
  x: 1,
  y: 2,
  z: 3,
  color: '#ffca76',
  intensity: 3,
  daylightDamping: 1,
};
const sources = [source];

beforeEach(() => {
  vi.restoreAllMocks();
  runtime.camera = new PerspectiveCamera();
});

describe('light selection updates', () => {
  it('selects lights once while the camera and world lighting are unchanged', () => {
    const select = vi.spyOn(lighting, 'lightRig');
    renderWithProviders(<LightPool sources={sources} hour={12} shadowSlots={0} decay={1} />);
    act(() => {
      for (let frame = 0; frame < 120; frame++) runtime.frame();
    });
    expect(select).toHaveBeenCalledTimes(1);
  });

  it('refreshes on camera movement, time changes, and replacement light sources', () => {
    const select = vi.spyOn(lighting, 'lightRig');
    const { rerender } = renderWithProviders(
      <LightPool sources={sources} hour={12} shadowSlots={0} decay={1} />,
    );
    act(() => runtime.frame());
    (runtime.camera as PerspectiveCamera).position.x = 10;
    act(() => runtime.frame());
    expect(select).toHaveBeenLastCalledWith(sources, 12, expect.objectContaining({ x: 10, z: 0 }));
    rerender(<LightPool sources={sources} hour={0} shadowSlots={0} decay={1} />);
    act(() => runtime.frame());
    expect(select.mock.results.at(-1)?.value[0].intensity).toBe(3);
    const replacement = [{ ...source, intensity: 6 }];
    rerender(<LightPool sources={replacement} hour={0} shadowSlots={0} decay={1} />);
    act(() => runtime.frame());
    expect(select.mock.results.at(-1)?.value[0].intensity).toBe(6);
    expect(select).toHaveBeenCalledTimes(4);
  });

  it('initializes replacement light objects when shadow settings change', () => {
    const select = vi.spyOn(lighting, 'lightRig');
    const { rerender } = renderWithProviders(
      <LightPool sources={sources} hour={0} shadowSlots={0} decay={1} />,
    );
    act(() => runtime.frame());
    rerender(<LightPool sources={sources} hour={0} shadowSlots={1} shadowFar={20} decay={1} />);
    act(() => runtime.frame());
    expect(select).toHaveBeenCalledTimes(2);
  });
});
