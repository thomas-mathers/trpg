import { useEffect, useReducer } from 'react';

import type { FightState } from '@/api/client';
import type { CombatOutcome } from '@/features/combat/combat-outcome';
import type { CombatRoundEvent, CombatUpdatePayload } from '@/features/combat/combat-round-event';
import { gameEventBus } from '@/lib/game-event-bus';

const ATTACK_WINDUP_MS = 700;
const ATTACK_RESULT_PAUSE_MS = 900;

export interface CombatFlash {
  kind: 'hit' | 'crit' | 'miss' | 'block';
  damage?: number;
  nonce: number;
}

type AnimationStep =
  | { kind: 'windup'; attackerId: string; delayMs: number }
  | { kind: 'resources'; fightState: FightState; delayMs: number }
  | { kind: 'apply'; event: CombatRoundEvent; delayMs: number }
  | { kind: 'settle'; fightState: FightState; delayMs: number };

interface CombatState {
  fight: FightState | null;
  activeAttackerId: string | null;
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
        ? { ...combatant, currentHp: event.targetRemainingHp, maximumHp: event.targetMaximumHp }
        : combatant,
    ),
  };
}

function applyResourcesToFight(fight: FightState, fightState: FightState): FightState {
  const resourcesByCombatantId = new Map(
    fightState.combatants.map((combatant) => [combatant.id, combatant]),
  );

  return {
    combatants: fight.combatants.map((combatant) => {
      const updatedCombatant = resourcesByCombatantId.get(combatant.id);
      return updatedCombatant
        ? {
            ...combatant,
            currentAp: updatedCombatant.currentAp,
            maximumAp: updatedCombatant.maximumAp,
            currentMp: updatedCombatant.currentMp,
            maximumMp: updatedCombatant.maximumMp,
          }
        : combatant;
    }),
  };
}

function toCombatFlash(event: CombatRoundEvent, nonce: number): CombatFlash {
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
  const [firstEvent, ...remainingEvents] = payload.events;
  if (!firstEvent) {
    steps.push({ kind: 'resources', fightState: payload.fightState, delayMs: 0 });
  } else {
    steps.push({ kind: 'windup', attackerId: firstEvent.attackerId, delayMs: ATTACK_WINDUP_MS });
    steps.push({ kind: 'resources', fightState: payload.fightState, delayMs: 0 });
    steps.push({ kind: 'apply', event: firstEvent, delayMs: ATTACK_RESULT_PAUSE_MS });
  }

  for (const event of remainingEvents) {
    steps.push({ kind: 'windup', attackerId: event.attackerId, delayMs: ATTACK_WINDUP_MS });
    steps.push({ kind: 'apply', event, delayMs: ATTACK_RESULT_PAUSE_MS });
  }
  steps.push({ kind: 'settle', fightState: payload.fightState, delayMs: 0 });
  return steps;
}

function reduceStep(state: CombatState, step: AnimationStep): CombatState {
  switch (step.kind) {
    case 'windup':
      return { ...state, activeAttackerId: step.attackerId };
    case 'resources':
      return {
        ...state,
        fight: state.fight ? applyResourcesToFight(state.fight, step.fightState) : state.fight,
      };
    case 'apply':
      return {
        ...state,
        fight: state.fight ? applyRoundEventToFight(state.fight, step.event) : state.fight,
        activeAttackerId: null,
        combatFlashes: {
          ...state.combatFlashes,
          [step.event.targetId]: toCombatFlash(step.event, state.eventCounter + 1),
        },
        eventCounter: state.eventCounter + 1,
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
    combatFlashes: state.combatFlashes,
    isPlayingBack: state.isPlayingBack,
  };
}
