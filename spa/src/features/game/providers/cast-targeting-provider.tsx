import { useQueryClient } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import type { AbilitySummary } from '@/api/client';
import { getPlayerAbilityAvailabilityQueryKey } from '@/api/client';
import { useScene } from '@/features/game/contexts/scene-context';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { useTarget } from '@/features/game/hooks/use-target';
import { useAction } from '@/features/game/run-action';

import { resolveCastTarget } from '../cast-target';
import { CastTargetingContext, type CastTargeting } from '../hooks/use-cast-targeting';

export function CastTargetingProvider({ children }: { children: ReactNode }) {
  const { scene } = useScene();
  const { target } = useTarget();
  const chatHub = useChatHub();
  const queryClient = useQueryClient();
  const { pending, run } = useAction();

  const playerId = scene?.playerStatus.id;

  const castOn = async (ability: AbilitySummary, targetId: string) => {
    if (!playerId || pending) {
      return;
    }

    await run(chatHub.sendCastAbility(targetId, ability.name));
    void queryClient.invalidateQueries({
      queryKey: getPlayerAbilityAvailabilityQueryKey({ path: { playerId } }),
    });
  };

  const value: CastTargeting = {
    selectAbility: (ability) => {
      const targetId = playerId && resolveCastTarget(ability, target?.id, playerId);
      if (targetId) {
        void castOn(ability, targetId);
      }
    },
  };

  return <CastTargetingContext.Provider value={value}>{children}</CastTargetingContext.Provider>;
}
