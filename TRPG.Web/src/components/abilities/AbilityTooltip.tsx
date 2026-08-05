import type { AbilitySummary } from '@/api/client';
import { getAbilityIcon } from '@/lib/ability-visuals';

interface AbilityTooltipProps {
  ability: AbilitySummary;
  isUnlocked: boolean;
  currentLevel: number;
}

export function AbilityTooltip({ ability, isUnlocked, currentLevel }: AbilityTooltipProps) {
  const Icon = getAbilityIcon(ability);

  return (
    <div className="space-y-1.5">
      <div className="flex items-center gap-1.5">
        <Icon className="text-muted-foreground mr-1 size-8 shrink-0" />
        <div className="min-w-0 flex-1">
          <p className="truncate font-semibold" title={ability.name}>
            {ability.name}
          </p>
          <p className="text-muted-foreground text-xs">{ability.skill}</p>
        </div>
      </div>

      <p className="text-muted-foreground text-xs italic">{ability.description}</p>

      {!isUnlocked && (
        <div className="border-border/60 text-destructive space-y-0.5 border-t pt-1.5 text-xs">
          <p>
            Requires {ability.skill} level {ability.requiredSkillLevel} (currently {currentLevel})
          </p>
          {ability.prerequisites.length > 0 && (
            <p>Requires ability: {ability.prerequisites.join(', ')}</p>
          )}
        </div>
      )}
    </div>
  );
}
