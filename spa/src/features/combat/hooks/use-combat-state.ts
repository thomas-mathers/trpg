import { useEffect, useReducer } from 'react';

import type { FightState } from '@/api/client';
import type { CombatOutcome } from '@/features/combat/combat-outcome';
import type {
  CombatActionEvent,
  CombatRegeneratedEvent,
  CombatResourceStateUpdatedEvent,
  CombatRoundEvent,
  CombatUpdatePayload,
} from '@/features/combat/combat-round-event';
import { gameEventBus } from '@/lib/game-event-bus';

const ATTACK_WINDUP_MS = 220;
const IMPACT_TO_TOAST_MS = 180;
const TOAST_TO_DEFENDER_RECOVERY_MS = 720;
const DEFENDER_TO_ATTACKER_RECOVERY_MS = 180;
const BETWEEN_TURNS_MS = 650;
const BEFORE_ROUND_REGEN_MS = 900;

export interface CombatFlash {
  kind: 'hit' | 'crit' | 'miss' | 'block';
  damage?: number;
  nonce: number;
}

type AnimationStep =
  | {
      kind: 'begin';
      attackerId: string;
      resourceState?: CombatResourceStateUpdatedEvent;
      delayMs: number;
    }
  | { kind: 'apply'; event: CombatActionEvent; delayMs: number }
  | { kind: 'toast'; delayMs: number }
  | { kind: 'defenderRecovery'; delayMs: number }
  | { kind: 'attackerRecovery'; delayMs: number }
  | { kind: 'prepareRegeneration'; events: CombatRegeneratedEvent[]; delayMs: number }
  | { kind: 'regenerate'; events: CombatRegeneratedEvent[]; delayMs: number }
  | { kind: 'settle'; fightState: FightState; delayMs: number };

interface CombatState {
  fight: FightState | null;
  activeAttackerId: string | null;
  activeDefenderId: string | null;
  activeCombatEvent: CombatActionEvent | null;
  combatFlashes: Record<string, CombatFlash>;
  isPlayingBack: boolean;
  combatOutcome: CombatOutcome | null;
  queue: AnimationStep[];
  pendingOutcome: CombatOutcome | null;
  eventCounter: number;
}

type CombatStateAction =
  | { type: 'FIGHT_STARTED'; fight: FightState }
  | { type: 'ROUND_RECEIVED'; payload: CombatUpdatePayload; skipAnimation: boolean }
  | { type: 'STEP_ADVANCED' }
  | { type: 'OUTCOME_RECEIVED'; outcome: CombatOutcome }
  | { type: 'RESOLVED' };

const initialState: CombatState = {
  fight: null,
  activeAttackerId: null,
  activeDefenderId: null,
  activeCombatEvent: null,
  combatFlashes: {},
  isPlayingBack: false,
  combatOutcome: null,
  queue: [],
  pendingOutcome: null,
  eventCounter: 0,
};

function applyRoundEventToFight(fight: FightState, event: CombatRoundEvent): FightState {
  if (event.type !== 'CombatHitEvent') {
    return fight;
  }

  return {
    combatants: fight.combatants.map((combatant) =>
      combatant.id === event.targetId
        ? {
            ...combatant,
            currentHp: event.targetRemainingHp,
            maximumHp: event.targetMaximumHp,
            isAlive: !event.killed,
          }
        : combatant,
    ),
  };
}

function applyRegenerationToFight(
  fight: FightState,
  events: CombatRegeneratedEvent[],
  phase: 'before' | 'after',
): FightState {
  const regenerationByCombatantId = new Map(events.map((event) => [event.attackerId, event]));

  return {
    combatants: fight.combatants.map((combatant) => {
      const regeneration = regenerationByCombatantId.get(combatant.id);
      return regeneration
        ? {
            ...combatant,
            currentAp: phase === 'before' ? regeneration.previousAp : regeneration.currentAp,
            maximumAp: regeneration.maximumAp,
            currentMp: phase === 'before' ? regeneration.previousMp : regeneration.currentMp,
            maximumMp: regeneration.maximumMp,
          }
        : combatant;
    }),
  };
}

function applyResourceStateToFight(
  fight: FightState,
  resourceState: CombatResourceStateUpdatedEvent,
): FightState {
  return {
    combatants: fight.combatants.map((combatant) =>
      combatant.id === resourceState.attackerId
        ? {
            ...combatant,
            currentAp: resourceState.currentAp,
            maximumAp: resourceState.maximumAp,
            currentMp: resourceState.currentMp,
            maximumMp: resourceState.maximumMp,
          }
        : combatant,
    ),
  };
}

function toCombatFlash(event: CombatActionEvent, nonce: number): CombatFlash {
  switch (event.type) {
    case 'CombatHitEvent':
      return { kind: event.isCritical ? 'crit' : 'hit', damage: event.damage, nonce };
    case 'CombatMissEvent':
      return { kind: 'miss', nonce };
    case 'CombatBlockEvent':
      return { kind: 'block', nonce };
  }
}

function buildRoundSteps(payload: CombatUpdatePayload): AnimationStep[] {
  const steps: AnimationStep[] = [];
  if (payload.events.length === 0) {
    steps.push({ kind: 'settle', fightState: payload.fightState, delayMs: 0 });
    return steps;
  }

  const actionEvents = payload.events.filter(
    (event): event is CombatActionEvent =>
      event.type !== 'CombatRegeneratedEvent' && event.type !== 'CombatResourceStateUpdatedEvent',
  );
  const resourceStatesByCombatantId = new Map(
    payload.events
      .filter(
        (event): event is CombatResourceStateUpdatedEvent =>
          event.type === 'CombatResourceStateUpdatedEvent',
      )
      .map((event) => [event.attackerId, event]),
  );
  const regenerationEvents = payload.events.filter(
    (event): event is CombatRegeneratedEvent => event.type === 'CombatRegeneratedEvent',
  );

  for (const [index, event] of actionEvents.entries()) {
    steps.push({
      kind: 'begin',
      attackerId: event.attackerId,
      resourceState: resourceStatesByCombatantId.get(event.attackerId),
      delayMs: index === 0 ? 0 : BETWEEN_TURNS_MS,
    });
    steps.push({ kind: 'apply', event, delayMs: ATTACK_WINDUP_MS });
    steps.push({ kind: 'toast', delayMs: IMPACT_TO_TOAST_MS });
    steps.push({ kind: 'defenderRecovery', delayMs: TOAST_TO_DEFENDER_RECOVERY_MS });
    steps.push({ kind: 'attackerRecovery', delayMs: DEFENDER_TO_ATTACKER_RECOVERY_MS });
  }

  if (regenerationEvents.length > 0) {
    steps.push({
      kind: 'prepareRegeneration',
      events: regenerationEvents,
      delayMs: BEFORE_ROUND_REGEN_MS,
    });
    steps.push({ kind: 'regenerate', events: regenerationEvents, delayMs: 280 });
  }

  steps.push({ kind: 'settle', fightState: payload.fightState, delayMs: 0 });
  return steps;
}

function reduceStep(state: CombatState, step: AnimationStep): CombatState {
  switch (step.kind) {
    case 'begin':
      return {
        ...state,
        activeAttackerId: step.attackerId,
        activeDefenderId: null,
        activeCombatEvent: null,
        fight:
          state.fight && step.resourceState
            ? applyResourceStateToFight(state.fight, step.resourceState)
            : state.fight,
      };
    case 'apply':
      return {
        ...state,
        fight: state.fight ? applyRoundEventToFight(state.fight, step.event) : state.fight,
        activeDefenderId: step.event.targetId,
        activeCombatEvent: step.event,
        combatFlashes: {
          ...state.combatFlashes,
          [step.event.targetId]: toCombatFlash(step.event, state.eventCounter + 1),
        },
        eventCounter: state.eventCounter + 1,
      };
    case 'toast':
      return state;
    case 'defenderRecovery':
      return { ...state, activeDefenderId: null };
    case 'attackerRecovery':
      return { ...state, activeAttackerId: null, activeCombatEvent: null };
    case 'prepareRegeneration':
      return {
        ...state,
        fight: state.fight
          ? applyRegenerationToFight(state.fight, step.events, 'before')
          : state.fight,
      };
    case 'regenerate':
      return {
        ...state,
        fight: state.fight
          ? applyRegenerationToFight(state.fight, step.events, 'after')
          : state.fight,
      };
    case 'settle':
      return { ...state, fight: step.fightState };
  }
}

function combatReducer(state: CombatState, action: CombatStateAction): CombatState {
  switch (action.type) {
    case 'FIGHT_STARTED':
      return { ...initialState, fight: action.fight };

    case 'ROUND_RECEIVED': {
      if (state.fight === null || action.skipAnimation) {
        return { ...state, fight: action.payload.fightState };
      }

      return {
        ...state,
        queue: [...state.queue, ...buildRoundSteps(action.payload)],
        isPlayingBack: true,
      };
    }

    case 'STEP_ADVANCED': {
      const [step, ...rest] = state.queue;
      if (!step) {
        return state;
      }

      const next = { ...reduceStep(state, step), queue: rest };
      if (rest.length > 0) {
        return next;
      }

      return {
        ...next,
        isPlayingBack: false,
        combatOutcome: state.pendingOutcome,
        pendingOutcome: null,
      };
    }

    case 'OUTCOME_RECEIVED':
      return state.queue.length === 0
        ? { ...state, combatOutcome: action.outcome }
        : { ...state, pendingOutcome: action.outcome };

    case 'RESOLVED':
      return { ...state, combatOutcome: null, fight: null };

    default:
      return state;
  }
}

function prefersReducedMotion() {
  return (
    typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches
  );
}

export function useCombatState() {
  const [state, dispatch] = useReducer(combatReducer, initialState);

  useEffect(() => {
    if (state.queue.length === 0) {
      return;
    }

    const timer = setTimeout(() => dispatch({ type: 'STEP_ADVANCED' }), state.queue[0].delayMs);
    return () => clearTimeout(timer);
  }, [state.queue]);

  useEffect(() => {
    const unsubscribeCombatStarted = gameEventBus.on('CombatStarted', (payload) => {
      dispatch({ type: 'FIGHT_STARTED', fight: payload });
    });
    const unsubscribeCombatUpdated = gameEventBus.on('CombatUpdated', (payload) => {
      dispatch({ type: 'ROUND_RECEIVED', payload, skipAnimation: prefersReducedMotion() });
    });
    const unsubscribeCombatEnded = gameEventBus.on('CombatEnded', (outcome) => {
      dispatch({ type: 'OUTCOME_RECEIVED', outcome });
    });

    return () => {
      unsubscribeCombatStarted();
      unsubscribeCombatUpdated();
      unsubscribeCombatEnded();
    };
  }, []);

  useEffect(() => {
    if (!state.combatOutcome) {
      return;
    }

    gameEventBus.emit('CombatResolved', state.combatOutcome);
    dispatch({ type: 'RESOLVED' });
  }, [state.combatOutcome]);

  return {
    fight: state.fight,
    activeAttackerId: state.activeAttackerId,
    activeDefenderId: state.activeDefenderId,
    activeCombatEvent: state.activeCombatEvent,
    combatFlashes: state.combatFlashes,
    isPlayingBack: state.isPlayingBack,
  };
}
