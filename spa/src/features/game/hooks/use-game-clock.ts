import { useCallback, useEffect, useRef, useState } from 'react';

import { useScene } from '@/features/game/contexts/scene-context';
import {
  gameDateTimeAt,
  gameTimeMillisecondsAt,
  type GameClockAnchor,
  type GameDateTime,
} from '@/features/game/game-clock';

const TICK_MILLISECONDS = 1000;

function useClockAnchor(): GameClockAnchor | undefined {
  const scene = useScene();
  if (!scene) return undefined;

  return {
    gameTimeMilliseconds: scene.gameTimeMilliseconds,
    anchoredAtUnixMilliseconds: scene.anchoredAtUnixMilliseconds,
    timeScale: scene.timeScale,
  };
}

function useClockNow(anchor: GameClockAnchor | undefined): number {
  const gameTimeMilliseconds = anchor?.gameTimeMilliseconds;
  const anchoredAtUnixMilliseconds = anchor?.anchoredAtUnixMilliseconds;
  const timeScale = anchor?.timeScale;
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const interval = setInterval(() => setNow(Date.now()), TICK_MILLISECONDS);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    setNow(Date.now());
  }, [gameTimeMilliseconds, anchoredAtUnixMilliseconds, timeScale]);

  return now;
}

// Re-renders once per second with the current fictional date and time.
export function useGameClock(): GameDateTime | undefined {
  const anchor = useClockAnchor();
  const now = useClockNow(anchor);

  return anchor ? gameDateTimeAt(anchor, now) : undefined;
}

// Re-renders once per second with the current game time in milliseconds since the game epoch.
export function useGameTimeMilliseconds(): number | undefined {
  const anchor = useClockAnchor();
  const now = useClockNow(anchor);

  return anchor ? gameTimeMillisecondsAt(anchor, now) : undefined;
}

// For one-off calculations at a moment of interaction; never re-renders and never reads a stale hour.
export function useGameTimeReader(): () => GameDateTime | undefined {
  const anchor = useClockAnchor();
  const anchorRef = useRef(anchor);

  useEffect(() => {
    anchorRef.current = anchor;
  });

  return useCallback(
    () => (anchorRef.current ? gameDateTimeAt(anchorRef.current, Date.now()) : undefined),
    [],
  );
}
