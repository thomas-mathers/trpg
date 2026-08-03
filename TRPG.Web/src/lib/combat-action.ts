import type { FightState } from '@/api/client';

// Mirrors TRPG.Contracts.Combat.Requests.PlayerCombatAction. SignalR hub method
// parameters aren't covered by the OpenAPI generator (that only sees REST
// endpoints), so this type is hand-written rather than generated — the `type`
// discriminator and camelCase field names must match the server's
// [JsonPolymorphic]/[JsonDerivedType] setup exactly.
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

// The player's chat bubble for a combat action is derived client-side — the
// hub only streams narration tokens back, it never echoes the action itself.
export function describeCombatAction(action: PlayerCombatAction, fight: FightState | null): string {
  if (action.type === 'UseItemAction') {
    return `Used ${action.itemName}`;
  }

  const target = fight?.combatants.find((c) => c.id === action.targetId);
  return target && !target.isPlayer
    ? `Used ${action.abilityName} on ${target.name}`
    : `Used ${action.abilityName}`;
}
