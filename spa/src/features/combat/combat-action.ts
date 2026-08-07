import type { FightState } from '@/api/client';

export type PlayerCombatAction = UseAbilityAction | UseItemAction;

export interface UseAbilityAction {
  type: 'UseAbilityAction';
  targetId: string;
  abilityName: string;
}

export interface UseItemAction {
  type: 'UseItemAction';
  itemName: string;
}

export function formatCombatAction(action: PlayerCombatAction, fight: FightState | null): string {
  if (action.type === 'UseItemAction') {
    return `Used ${action.itemName}`;
  }

  const target = fight?.combatants.find((c) => c.id === action.targetId);
  return target && !target.isPlayer
    ? `Used ${action.abilityName} on ${target.name}`
    : `Used ${action.abilityName}`;
}
