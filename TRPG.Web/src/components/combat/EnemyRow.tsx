import type { CombatantState } from '@/api/client';
import { CombatantCard } from '@/components/combat/CombatantCard';
import type { CombatCardEffect } from '@/hooks/useCombatState';

interface EnemyRowProps {
  enemies: CombatantState[];
  playerLevel: number;
  targetable?: boolean;
  onSelectTarget?: (id: string) => void;
  activeAttackerId?: string | null;
  cardEffects?: Record<string, CombatCardEffect>;
}

export function EnemyRow({
  enemies,
  playerLevel,
  targetable = false,
  onSelectTarget,
  activeAttackerId = null,
  cardEffects = {},
}: EnemyRowProps) {
  return (
    <div className="flex max-h-52 flex-wrap gap-2 overflow-y-auto p-2.5 pb-1">
      {enemies.map((enemy) => (
        <CombatantCard
          key={enemy.id}
          combatant={enemy}
          playerLevel={playerLevel}
          targetable={targetable}
          onSelect={() => onSelectTarget?.(enemy.id)}
          effect={cardEffects[enemy.id]}
          isActing={activeAttackerId === enemy.id}
        />
      ))}
    </div>
  );
}
