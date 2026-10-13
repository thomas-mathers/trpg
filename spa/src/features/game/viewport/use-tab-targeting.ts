import { useEffect } from 'react';

import type { CreatureStatusSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { cycleTarget } from './target-cycle';

export function useTabTargeting({
  enabled,
  creatures,
  player,
  targetId,
  onTarget,
}: {
  enabled: boolean;
  creatures: Pick<CreatureStatusSnapshot, 'id' | 'condition' | 'placement'>[];
  player: { x: number; y: number };
  targetId: string | undefined;
  onTarget: (id: string | undefined) => void;
}) {
  const { x, y } = player;
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (!enabled || event.code !== 'Tab' || event.repeat || !document.pointerLockElement) return;
      event.preventDefault();
      onTarget(cycleTarget(creatures, { x, y }, targetId, event.shiftKey ? -1 : 1));
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [enabled, creatures, x, y, targetId, onTarget]);
}
