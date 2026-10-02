import { useEffect, useRef } from 'react';

import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { gameEventBus } from '@/lib/game-event-bus';

export function useDeathRespawn() {
  const { submitNarratedTurn } = useGameChat();
  const chatHub = useChatHub();
  const respawn = useRef(() => {});

  useEffect(() => {
    respawn.current = () => submitNarratedTurn(null, chatHub.sendRespawn());
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
