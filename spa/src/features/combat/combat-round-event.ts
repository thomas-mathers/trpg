import type { DamageType, FightState } from '@/api/client';

export type CombatRoundEvent = CombatHitEvent | CombatMissEvent | CombatBlockEvent;

interface CombatRoundEventBase {
  attackerId: string;
  attackerName: string;
  abilityName: string;
  targetId: string;
  targetName: string;
}

export interface CombatHitEvent extends CombatRoundEventBase {
  type: 'CombatHitEvent';
  damage: number;
  damageType: DamageType;
  isCritical: boolean;
  killed: boolean;
  targetRemainingHp: number;
  targetMaximumHp: number;
  appliedConditions: string[];
}

export interface CombatMissEvent extends CombatRoundEventBase {
  type: 'CombatMissEvent';
}

export interface CombatBlockEvent extends CombatRoundEventBase {
  type: 'CombatBlockEvent';
}

export interface CombatUpdatePayload {
  fightState: FightState;
  events: CombatRoundEvent[];
}
