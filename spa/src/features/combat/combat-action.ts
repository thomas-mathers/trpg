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
