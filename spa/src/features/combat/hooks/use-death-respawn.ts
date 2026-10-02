import { useEffect, useRef } from 'react';

import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { runAction } from '@/features/game/run-action';
import { gameEventBus } from '@/lib/game-event-bus';

export function useDeathRespawn() {
  const chatHub = useChatHub();
  const respawn = useRef(() => {});

  useEffect(() => {
    respawn.current = () => void runAction(chatHub.sendRespawn());
  });

  // Subscribed once: re-subscribing on each render drops a defeat emitted between cleanup and re-subscribe.
  useEffect(
    () =>
      gameEventBus.on('CombatResolved', (outcome) => {
        if (outcome === 'Defeat') {
          respawn.current();
        }
      }),
    [],
  );
}

export function DeathRespawnEffect() {
  useDeathRespawn();
  return null;
}
