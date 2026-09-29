import { useQueryClient } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';

import type { AbilitySummary } from '@/api/client';
import { getPlayerAbilityAvailabilityQueryKey } from '@/api/client';
import { useScene } from '@/features/game/contexts/scene-context';
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';

import {
  CastTargetingContext,
  type CastTarget,
  type CastTargeting,
} from '../hooks/use-cast-targeting';

export function CastTargetingProvider({ children }: { children: ReactNode }) {
  const scene = useScene();
  const chatHub = useChatHub();
  const queryClient = useQueryClient();
  const { isStreaming, submitNarratedTurn } = useGameChat();
  const [pendingAbility, setPendingAbility] = useState<AbilitySummary | null>(null);

  const playerId = scene?.playerStatus.id;

  const castOn = (ability: AbilitySummary, target: CastTarget) => {
    if (!playerId || isStreaming) {
      return;
    }

    setPendingAbility(null);
    const displayText =
      target.id === playerId ? `Cast ${ability.name}` : `Cast ${ability.name} on ${target.name}`;
    submitNarratedTurn(
      displayText,
      chatHub.sendCastAbility(target.id, ability.name),
      undefined,
      () => {
        void queryClient.invalidateQueries({
          queryKey: getPlayerAbilityAvailabilityQueryKey({ path: { playerId } }),
        });
      },
    );
  };

  const value: CastTargeting = {
    pendingAbility,
    cancel: () => setPendingAbility(null),
    selectAbility: (ability) => {
      if (!ability.requiresTarget && playerId) {
        castOn(ability, { id: playerId, name: 'yourself' });
        return;
      }
      setPendingAbility((current) => (current?.name === ability.name ? null : ability));
    },
    castOn: (target) => {
      if (pendingAbility) {
        castOn(pendingAbility, target);
      }
    },
  };

  return <CastTargetingContext.Provider value={value}>{children}</CastTargetingContext.Provider>;
}
