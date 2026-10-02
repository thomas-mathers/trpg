import { useQueryClient } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';

import type { AbilitySummary } from '@/api/client';
import { getPlayerAbilityAvailabilityQueryKey } from '@/api/client';
import { useScene } from '@/features/game/contexts/scene-context';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { useAction } from '@/features/game/run-action';

import {
  CastTargetingContext,
  type CastTarget,
  type CastTargeting,
} from '../hooks/use-cast-targeting';

export function CastTargetingProvider({ children }: { children: ReactNode }) {
  const scene = useScene();
  const chatHub = useChatHub();
  const queryClient = useQueryClient();
  const { pending, run } = useAction();
  const [pendingAbility, setPendingAbility] = useState<AbilitySummary | null>(null);

  const playerId = scene?.playerStatus.id;

  const castOn = async (ability: AbilitySummary, target: CastTarget) => {
    if (!playerId || pending) {
      return;
    }

    setPendingAbility(null);
    await run(chatHub.sendCastAbility(target.id, ability.name));
    void queryClient.invalidateQueries({
      queryKey: getPlayerAbilityAvailabilityQueryKey({ path: { playerId } }),
    });
  };

  const value: CastTargeting = {
    pendingAbility,
    cancel: () => setPendingAbility(null),
    selectAbility: (ability) => {
      if (!ability.requiresTarget && playerId) {
        void castOn(ability, { id: playerId, name: 'yourself' });
        return;
      }
      setPendingAbility((current) => (current?.name === ability.name ? null : ability));
    },
    castOn: (target) => {
      if (pendingAbility) {
        void castOn(pendingAbility, target);
      }
    },
  };

  return <CastTargetingContext.Provider value={value}>{children}</CastTargetingContext.Provider>;
}
