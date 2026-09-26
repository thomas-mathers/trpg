import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import { SceneContext } from '@/features/game/contexts/scene-context';
import { formatGameClockTime } from '@/features/game/game-clock';

import { useGameClock, useGameTimeReader } from './use-game-clock';

const ANCHORED_AT = Date.UTC(2030, 0, 1);
const HOUR = 60 * 60 * 1000;

function scene(gameTimeMilliseconds: number, anchoredAtUnixMilliseconds: number): SceneSnapshot {
  return { gameTimeMilliseconds, anchoredAtUnixMilliseconds } as SceneSnapshot;
}

function sceneWrapper(current: SceneSnapshot | undefined) {
  return ({ children }: { children: ReactNode }) => (
    <SceneContext.Provider value={current}>{children}</SceneContext.Provider>
  );
}

describe('useGameClock', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] });
    vi.setSystemTime(ANCHORED_AT);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('has no time before the first scene arrives', () => {
    const { result } = renderHook(() => useGameClock(), { wrapper: sceneWrapper(undefined) });

    expect(result.current).toBeUndefined();
  });

  it('derives the time from the scene anchor and advances once per second', () => {
    const { result } = renderHook(() => useGameClock(), {
      wrapper: sceneWrapper(scene(2 * HOUR, ANCHORED_AT)),
    });

    expect(formatGameClockTime(result.current!)).toBe('10:00:00');

    act(() => {
      vi.advanceTimersByTime(1000);
    });

    expect(formatGameClockTime(result.current!)).toBe('10:00:01');

    act(() => {
      vi.advanceTimersByTime(59_000);
    });

    expect(formatGameClockTime(result.current!)).toBe('10:01:00');
  });

  it('re-anchors to a newer scene immediately', () => {
    let current = scene(0, ANCHORED_AT);
    const { result, rerender } = renderHook(() => useGameClock(), {
      wrapper: ({ children }: { children: ReactNode }) => sceneWrapper(current)({ children }),
    });
    act(() => {
      vi.advanceTimersByTime(30_000);
    });

    current = scene(HOUR, ANCHORED_AT + 30_000);
    rerender();

    expect(formatGameClockTime(result.current!)).toBe('09:00:00');
  });
});

describe('useGameTimeReader', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(ANCHORED_AT);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('reads the current time at the moment it is called', () => {
    const { result } = renderHook(() => useGameTimeReader(), {
      wrapper: sceneWrapper(scene(0, ANCHORED_AT)),
    });
    vi.setSystemTime(ANCHORED_AT + 125_000);

    expect(formatGameClockTime(result.current()!)).toBe('08:02:05');
  });

  it('keeps one stable reader while newer scenes arrive', () => {
    let current = scene(0, ANCHORED_AT);
    const { result, rerender } = renderHook(() => useGameTimeReader(), {
      wrapper: ({ children }: { children: ReactNode }) => sceneWrapper(current)({ children }),
    });
    const firstReader = result.current;

    current = scene(HOUR, ANCHORED_AT);
    rerender();

    expect(result.current).toBe(firstReader);
    expect(formatGameClockTime(result.current()!)).toBe('09:00:00');
  });

  it('has no time without a scene', () => {
    const { result } = renderHook(() => useGameTimeReader(), { wrapper: sceneWrapper(undefined) });

    expect(result.current()).toBeUndefined();
  });
});
