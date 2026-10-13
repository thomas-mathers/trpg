import { fireEvent, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useTabTargeting } from './use-tab-targeting';

const creatures = [
  { id: 'near', condition: 'Awake' as const, placement: { x: 1, y: 0, angle: 0 } },
  { id: 'far', condition: 'Awake' as const, placement: { x: 5, y: 0, angle: 0 } },
];

function mount(overrides: { enabled?: boolean; targetId?: string } = {}) {
  const onTarget = vi.fn();
  renderHook(() =>
    useTabTargeting({
      enabled: overrides.enabled ?? true,
      creatures,
      player: { x: 0, y: 0 },
      targetId: overrides.targetId,
      onTarget,
    }),
  );
  return onTarget;
}

describe('useTabTargeting', () => {
  beforeEach(() => {
    Object.defineProperty(document, 'pointerLockElement', {
      configurable: true,
      value: document.body,
    });
  });

  afterEach(() => {
    Object.defineProperty(document, 'pointerLockElement', { configurable: true, value: null });
  });

  it('targets the nearest creature on Tab', () => {
    const onTarget = mount();

    fireEvent.keyDown(window, { code: 'Tab' });

    expect(onTarget).toHaveBeenCalledWith('near');
  });

  it('advances to the next creature on Tab', () => {
    const onTarget = mount({ targetId: 'near' });

    fireEvent.keyDown(window, { code: 'Tab' });

    expect(onTarget).toHaveBeenCalledWith('far');
  });

  it('goes back to the previous creature on Shift+Tab', () => {
    const onTarget = mount({ targetId: 'far' });

    fireEvent.keyDown(window, { code: 'Tab', shiftKey: true });

    expect(onTarget).toHaveBeenCalledWith('near');
  });

  it('leaves Tab alone while the mouse is not captured', () => {
    Object.defineProperty(document, 'pointerLockElement', { configurable: true, value: null });
    const onTarget = mount();

    fireEvent.keyDown(window, { code: 'Tab' });

    expect(onTarget).not.toHaveBeenCalled();
  });

  it('ignores Tab while targeting is disabled', () => {
    const onTarget = mount({ enabled: false });

    fireEvent.keyDown(window, { code: 'Tab' });

    expect(onTarget).not.toHaveBeenCalled();
  });
});
