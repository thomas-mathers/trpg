import { renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { INPUT_HEARTBEAT_INTERVAL_MS, useInputReporter } from './use-input-reporter';

const frame = (forward: number, x = 0, strafe = 0, heading = 0) => ({
  forward,
  strafe,
  heading,
  x,
  y: 0,
});

describe('useInputReporter', () => {
  it('does not report a standing first frame, which only sets the baseline', () => {
    const send = vi.fn();
    const { result } = renderHook(() => useInputReporter('hall', send));

    result.current.track(frame(0), 0);

    expect(send).not.toHaveBeenCalled();
  });

  it('reports a first frame that is already moving', () => {
    const send = vi.fn();
    const { result } = renderHook(() => useInputReporter('hall', send));

    result.current.track(frame(1), 0);

    expect(send).toHaveBeenCalledExactlyOnceWith(frame(1));
  });

  it('reports immediately when the movement axes change', () => {
    const send = vi.fn();
    const { result } = renderHook(() => useInputReporter('hall', send));
    result.current.track(frame(0), 0);

    result.current.track(frame(1), 10);

    expect(send).toHaveBeenCalledExactlyOnceWith(frame(1));
  });

  it('reports a strafe change immediately', () => {
    const send = vi.fn();
    const { result } = renderHook(() => useInputReporter('hall', send));
    result.current.track(frame(1), 0);
    send.mockClear();

    result.current.track(frame(1, 0, 1), 10);

    expect(send).toHaveBeenCalledExactlyOnceWith(frame(1, 0, 1));
  });

  it('holds back an unchanged movement before the heartbeat interval', () => {
    const send = vi.fn();
    const { result } = renderHook(() => useInputReporter('hall', send));
    result.current.track(frame(1), 0);
    send.mockClear();

    result.current.track(frame(1, 0.3), INPUT_HEARTBEAT_INTERVAL_MS - 1);

    expect(send).not.toHaveBeenCalled();
  });

  it('sends a heartbeat while moving once the interval has elapsed', () => {
    const send = vi.fn();
    const { result } = renderHook(() => useInputReporter('hall', send));
    result.current.track(frame(1), 0);
    send.mockClear();

    result.current.track(frame(1, 0.4), INPUT_HEARTBEAT_INTERVAL_MS);

    expect(send).toHaveBeenCalledExactlyOnceWith(frame(1, 0.4));
  });

  it('sends nothing while standing still', () => {
    const send = vi.fn();
    const { result } = renderHook(() => useInputReporter('hall', send));
    result.current.track(frame(0), 0);

    result.current.track(frame(0), INPUT_HEARTBEAT_INTERVAL_MS * 4);

    expect(send).not.toHaveBeenCalled();
  });

  it('reports a turn on the spot once the interval has elapsed', () => {
    const send = vi.fn();
    const { result } = renderHook(() => useInputReporter('hall', send));
    result.current.track(frame(0), 0);

    result.current.track(frame(0, 0, 0, 0.5), INPUT_HEARTBEAT_INTERVAL_MS);

    expect(send).toHaveBeenCalledExactlyOnceWith(frame(0, 0, 0, 0.5));
  });

  it('starts a fresh baseline when the location changes', () => {
    const send = vi.fn();
    const { result, rerender } = renderHook(({ id }) => useInputReporter(id, send), {
      initialProps: { id: 'hall' },
    });
    result.current.track(frame(0), 0);
    rerender({ id: 'cellar' });

    result.current.track(frame(0, 8), INPUT_HEARTBEAT_INTERVAL_MS);

    expect(send).not.toHaveBeenCalled();
  });

  it('halts a moving player with a zero input at the last position', () => {
    const send = vi.fn();
    const { result } = renderHook(() => useInputReporter('hall', send));
    result.current.track(frame(1), 0);
    result.current.track(frame(1, 0.2), 100);
    send.mockClear();

    result.current.halt();

    expect(send).toHaveBeenCalledExactlyOnceWith(frame(0, 0.2));
  });

  it('does not halt a player who is already standing', () => {
    const send = vi.fn();
    const { result } = renderHook(() => useInputReporter('hall', send));
    result.current.track(frame(0), 0);

    result.current.halt();

    expect(send).not.toHaveBeenCalled();
  });

  it('halts only once', () => {
    const send = vi.fn();
    const { result } = renderHook(() => useInputReporter('hall', send));
    result.current.track(frame(1), 0);
    result.current.halt();
    send.mockClear();

    result.current.halt();

    expect(send).not.toHaveBeenCalled();
  });
});
