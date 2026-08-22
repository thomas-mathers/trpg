import { useEffect } from 'react';

import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { gameEventBus } from '@/lib/game-event-bus';

export function useDeathRespawn() {
  const { submitNarratedTurn } = useGameChat();
  const chatHub = useChatHub();

  useEffect(() => {
    const unsubscribe = gameEventBus.on('CombatResolved', (outcome) => {
      if (outcome !== 'Defeat') {
        return;
      }

      submitNarratedTurn(null, chatHub.sendRespawn());
    });

    return unsubscribe;
  }, [chatHub, submitNarratedTurn]);
}

export function DeathRespawnEffect() {
  useDeathRespawn();
  return null;
}
