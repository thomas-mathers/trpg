import type { IStreamResult } from '@microsoft/signalr';
import { ShieldAlert } from 'lucide-react';
import { useEffect, useReducer, useState } from 'react';
import { GiSparkles } from 'react-icons/gi';
import { toast } from 'sonner';

import type {
  CombatActionResult,
  CombatRegeneration,
  CombatResourceState,
  CombatantState,
  CombatUpdated,
} from '@/api/signalr-client/TRPG.Combat.Responses';
import type { TerminalCombatOutcome } from '@/features/combat/terminal-combat-outcome';
import { GameToast } from '@/features/game/components/game-toast';
import { useGameChat } from '@/features/game/hooks/use-game-chat';
import { useChatHub } from '@/features/game/hooks/use-game-hub-connection';
import { useDelayedReveal } from '@/hooks/use-delayed-reveal';
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
      resourceState?: CombatResourceState;
      delayMs: number;
    }
  | { kind: 'apply'; event: CombatActionResult; delayMs: number }
  | { kind: 'toast'; delayMs: number }
  | { kind: 'defenderRecovery'; delayMs: number }
  | { kind: 'attackerRecovery'; delayMs: number }
  | { kind: 'prepareRegeneration'; events: CombatRegeneration[]; delayMs: number }
  | { kind: 'regenerate'; events: CombatRegeneration[]; delayMs: number }
  | { kind: 'settle'; combatants: CombatantState[]; delayMs: number };

interface CombatState {
  fightId: string | null;
  fight: CombatantState[] | null;
  activeAttackerId: string | null;
  activeDefenderId: string | null;
  activeCombatEvent: CombatActionResult | null;
  combatFlashes: Record<string, CombatFlash>;
  isPlayingBack: boolean;
  combatOutcome: TerminalCombatOutcome | null;
  queue: AnimationStep[];
  pendingOutcome: TerminalCombatOutcome | null;
  eventCounter: number;
}

type CombatStateAction =
  | { type: 'FIGHT_STARTED'; fightId: string; fight: CombatantState[] }
  | { type: 'ROUND_RECEIVED'; payload: CombatUpdated; skipAnimation: boolean }
  | { type: 'STEP_ADVANCED' }
  | { type: 'RESOLVED' };

const initialState: CombatState = {
  fightId: null,
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

function applyRoundEventToFight(
  fight: CombatantState[],
  event: CombatActionResult,
): CombatantState[] {
  const { killed, targetMaximumHp, targetRemainingHp } = event;
  if (
    event.outcome !== 'Hit' ||
    targetRemainingHp === undefined ||
    targetMaximumHp === undefined ||
    killed === undefined
  ) {
    return fight;
  }

  return fight.map((combatant) =>
    combatant.id === event.targetId
      ? {
          ...combatant,
          currentHp: targetRemainingHp,
          maximumHp: targetMaximumHp,
          isAlive: !killed,
        }
      : combatant,
  );
}

function applyRegenerationToFight(
  fight: CombatantState[],
  events: CombatRegeneration[],
  phase: 'before' | 'after',
): CombatantState[] {
  const regenerationByCombatantId = new Map(events.map((event) => [event.combatantId, event]));

  return fight.map((combatant) => {
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
  });
}

function applyResourceStateToFight(
  fight: CombatantState[],
  resourceState: CombatResourceState,
): CombatantState[] {
  return fight.map((combatant) =>
    combatant.id === resourceState.combatantId
      ? {
          ...combatant,
          currentAp: resourceState.currentAp,
          maximumAp: resourceState.maximumAp,
          currentMp: resourceState.currentMp,
          maximumMp: resourceState.maximumMp,
        }
      : combatant,
  );
}

function toCombatFlash(event: CombatActionResult, nonce: number): CombatFlash {
  switch (event.outcome) {
    case 'Hit':
      return {
        kind: event.isCritical ? 'crit' : 'hit',
        damage: event.damage,
        nonce,
      };
    case 'Miss':
      return { kind: 'miss', nonce };
    case 'Block':
      return { kind: 'block', nonce };
  }
}

function buildRoundSteps(payload: CombatUpdated): AnimationStep[] {
  const steps: AnimationStep[] = [];
  if (payload.actions.length === 0) {
    steps.push({ kind: 'settle', combatants: payload.combatants, delayMs: 0 });
    return steps;
  }

  const resourceStatesByCombatantId = new Map(
    payload.resourceStates.map((resourceState) => [resourceState.combatantId, resourceState]),
  );

  for (const [index, event] of payload.actions.entries()) {
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

  if (payload.regenerations.length > 0) {
    steps.push({
      kind: 'prepareRegeneration',
      events: payload.regenerations,
      delayMs: BEFORE_ROUND_REGEN_MS,
    });
    steps.push({ kind: 'regenerate', events: payload.regenerations, delayMs: 280 });
  }

  steps.push({ kind: 'settle', combatants: payload.combatants, delayMs: 0 });
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
      return { ...state, fight: step.combatants };
  }
}

function combatReducer(state: CombatState, action: CombatStateAction): CombatState {
  switch (action.type) {
    case 'FIGHT_STARTED':
      return { ...initialState, fightId: action.fightId, fight: action.fight };

    case 'ROUND_RECEIVED': {
      const outcome = action.payload.outcome === 'Ongoing' ? undefined : action.payload.outcome;

      // A round with no actions (e.g. a clean flee) has nothing worth animating, so skip queuing it and apply it immediately.
      if (state.fight === null || action.skipAnimation || action.payload.actions.length === 0) {
        return {
          ...state,
          fight: action.payload.combatants,
          combatOutcome: outcome ?? state.combatOutcome,
        };
      }

      return {
        ...state,
        queue: [...state.queue, ...buildRoundSteps(action.payload)],
        isPlayingBack: true,
        pendingOutcome: outcome ?? state.pendingOutcome,
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

    case 'RESOLVED':
      return { ...state, combatOutcome: null, fight: null, fightId: null };

    default:
      return state;
  }
}

function prefersReducedMotion() {
  return (
    typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches
  );
}

export function useCombat() {
  const [state, dispatch] = useReducer(combatReducer, initialState);
  const { isStreaming, submitNarratedTurn } = useGameChat();
  const chatHub = useChatHub();
  const submitFlee = () => submitNarratedTurn(null, chatHub.sendFlee());
  const [isActionPending, setIsActionPending] = useState(false);
  // Exclude our own combat action's isStreaming flip so an ordinary round doesn't hide the dialog; a concluding fight closes it via state.fight going null instead.
  const isRevealed = useDelayedReveal(!!state.fight && !(isStreaming && !isActionPending));

  useEffect(() => {
    if (state.queue.length === 0) {
      return;
    }

    const timer = setTimeout(() => dispatch({ type: 'STEP_ADVANCED' }), state.queue[0].delayMs);
    return () => clearTimeout(timer);
  }, [state.queue]);

  useEffect(() => {
    const unsubscribeCombatStarted = gameEventBus.on('CombatStarted', (payload) => {
      dispatch({ type: 'FIGHT_STARTED', fightId: payload.fightId, fight: payload.combatants });
    });
    const unsubscribeCombatUpdated = gameEventBus.on('CombatUpdated', (payload) => {
      dispatch({ type: 'ROUND_RECEIVED', payload, skipAnimation: prefersReducedMotion() });
      for (const message of payload.messages) {
        toast.custom((toastId) => (
          <GameToast toastId={toastId} icon={GiSparkles} title="Combat" description={message} />
        ));
      }
      if (payload.outcome !== 'Ongoing') {
        gameEventBus.emit('CombatOutcomeKnown', payload.outcome);
      }
    });

    return () => {
      unsubscribeCombatStarted();
      unsubscribeCombatUpdated();
    };
  }, []);

  useEffect(() => {
    if (!state.combatOutcome || !state.fightId) {
      return;
    }

    gameEventBus.emit('CombatResolved', state.combatOutcome);
    dispatch({ type: 'RESOLVED' });
  }, [state.combatOutcome]);

  const submitCombatAction = (stream: IStreamResult<string>) => {
    setIsActionPending(true);
    submitNarratedTurn(
      null,
      stream,
      (error) => {
        const description =
          error instanceof Error ? error.message : 'Combat action could not be resolved.';
        toast.custom((toastId) => (
          <GameToast
            toastId={toastId}
            icon={ShieldAlert}
            title="Action rejected"
            description={description}
          />
        ));
      },
      () => setIsActionPending(false),
    );
  };

  const submitUseAbilityCombatAction = (targetId: string, abilityName: string) =>
    submitCombatAction(chatHub.resolveUseAbilityCombatAction(targetId, abilityName));

  const submitUseItemCombatAction = (itemName: string) =>
    submitCombatAction(chatHub.resolveUseItemCombatAction(itemName));

  const disabled = state.isPlayingBack || isActionPending || isStreaming;

  return {
    fight: state.fight,
    activeAttackerId: state.activeAttackerId,
    activeDefenderId: state.activeDefenderId,
    activeCombatEvent: state.activeCombatEvent,
    combatFlashes: state.combatFlashes,
    isRevealed,
    disabled,
    submitUseAbilityCombatAction,
    submitUseItemCombatAction,
    submitFlee,
  };
}
