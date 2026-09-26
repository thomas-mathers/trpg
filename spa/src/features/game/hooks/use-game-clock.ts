import { useCallback, useEffect, useRef, useState } from 'react';

import { useScene } from '@/features/game/contexts/scene-context';
import {
  gameDateTimeAt,
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
  };
}

// Re-renders once per second with the current fictional date and time.
export function useGameClock(): GameDateTime | undefined {
  const anchor = useClockAnchor();
  const gameTimeMilliseconds = anchor?.gameTimeMilliseconds;
  const anchoredAtUnixMilliseconds = anchor?.anchoredAtUnixMilliseconds;
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const interval = setInterval(() => setNow(Date.now()), TICK_MILLISECONDS);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    setNow(Date.now());
  }, [gameTimeMilliseconds, anchoredAtUnixMilliseconds]);

  if (gameTimeMilliseconds === undefined || anchoredAtUnixMilliseconds === undefined) {
    return undefined;
  }

  return gameDateTimeAt({ gameTimeMilliseconds, anchoredAtUnixMilliseconds }, now);
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
