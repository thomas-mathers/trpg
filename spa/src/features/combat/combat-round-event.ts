import type {
  CombatUpdatePayload as GeneratedCombatUpdatePayload,
  DamageType,
} from '@/api/signalr-client/TRPG.Combat.ClientModels';

export type CombatActionEvent = CombatHitEvent | CombatMissEvent | CombatBlockEvent;

export type CombatRoundEvent =
  | CombatActionEvent
  | CombatRegeneratedEvent
  | CombatResourceStateUpdatedEvent;

interface CombatRoundEventBase {
  attackerId: string;
  attackerName: string;
  abilityName: string;
  targetId: string;
  targetName: string;
  narration?: string | null;
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

export interface CombatRegeneratedEvent {
  type: 'CombatRegeneratedEvent';
  attackerId: string;
  attackerName: string;
  previousAp: number;
  currentAp: number;
  maximumAp: number;
  previousMp: number;
  currentMp: number;
  maximumMp: number;
}

export interface CombatResourceStateUpdatedEvent {
  type: 'CombatResourceStateUpdatedEvent';
  attackerId: string;
  attackerName: string;
  currentAp: number;
  maximumAp: number;
  currentMp: number;
  maximumMp: number;
}

export type CombatUpdatePayload = Pick<GeneratedCombatUpdatePayload, 'combatants' | 'outcome'> & {
  events: CombatRoundEvent[];
};

export function toCombatUpdatePayload(payload: GeneratedCombatUpdatePayload): CombatUpdatePayload {
  return { ...payload, events: payload.events as CombatRoundEvent[] };
}
