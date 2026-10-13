import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';

import {
  type AbilitySummary,
  getCreatureAbilitiesOptions,
  getPlayerAbilityAvailabilityOptions,
  getPlayerAbilityAvailabilityQueryKey,
} from '@/api/client';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { useScene } from '@/features/game/contexts/scene-context';
import { useCastTargeting } from '@/features/game/hooks/use-cast-targeting';
import { useTarget } from '@/features/game/hooks/use-target';
import { getAbilityIcon } from '@/features/skills/ability-visuals';

import { resolveCastTarget } from '../cast-target';

export function AbilityToolbar() {
  const { scene } = useScene();
  const playerId = scene.playerStatus.id;
  const { selectAbility } = useCastTargeting();
  const { target } = useTarget();
  const queryClient = useQueryClient();

  const abilitiesQuery = useQuery({
    ...getCreatureAbilitiesOptions({ path: { creatureId: playerId ?? '' } }),
    enabled: Boolean(playerId),
  });
  const availabilityQuery = useQuery({
    ...getPlayerAbilityAvailabilityOptions({ path: { playerId: playerId ?? '' } }),
    enabled: Boolean(playerId),
  });

  useEffect(() => {
    if (playerId) {
      void queryClient.invalidateQueries({
        queryKey: getPlayerAbilityAvailabilityQueryKey({ path: { playerId } }),
      });
    }
  }, [scene, playerId, queryClient]);

  const castable = abilitiesQuery.data ?? [];
  if (!playerId || castable.length === 0) {
    return null;
  }

  const availabilityByName = new Map((availabilityQuery.data ?? []).map((a) => [a.name, a]));

  return (
    <TooltipProvider>
      <div className="flex flex-col gap-1.5 pb-2">
        <div
          className="flex max-h-[4.75rem] flex-wrap content-start gap-1.5 overflow-y-auto"
          role="toolbar"
          aria-label="Abilities"
        >
          {castable.map((ability) => {
            const availability = availabilityByName.get(ability.name);
            const needsTarget = resolveCastTarget(ability, target?.id, playerId) === undefined;
            const usable = !needsTarget && (availability?.isUsable ?? true);
            const Icon = getAbilityIcon(ability);
            return (
              <Tooltip key={ability.name}>
                <TooltipTrigger asChild>
                  <span>
                    <Button
                      variant="outline"
                      size="icon"
                      aria-label={ability.name}
                      disabled={!usable}
                      className="size-9"
                      onClick={() => selectAbility(ability)}
                    >
                      <Icon className="size-5" />
                    </Button>
                  </span>
                </TooltipTrigger>
                <TooltipContent className="flex-col items-start gap-0.5">
                  <span className="font-semibold">{ability.name}</span>
                  <span>{ability.description}</span>
                  <span className="opacity-80">{costLine(ability)}</span>
                  {needsTarget ? (
                    <span className="text-destructive">Select a target with Tab</span>
                  ) : (
                    !usable &&
                    availability?.reason && (
                      <span className="text-destructive">{availability.reason}</span>
                    )
                  )}
                </TooltipContent>
              </Tooltip>
            );
          })}
        </div>
      </div>
    </TooltipProvider>
  );
}

function costLine(ability: AbilitySummary): string {
  const costs = [
    Number(ability.apCost) > 0 && `${ability.apCost} AP`,
    Number(ability.mpCost) > 0 && `${ability.mpCost} MP`,
    Number(ability.cooldownSeconds) > 0 && `${ability.cooldownSeconds}s cooldown`,
  ].filter(Boolean);
  return costs.length > 0 ? costs.join(' · ') : 'No cost';
}
