import { useCallback, useEffect, useRef, useState } from 'react';

import type { FightState } from '@/api/client';
import type { CombatOutcome } from '@/lib/combat-outcome';
import type { CombatRoundEvent, CombatUpdatePayload } from '@/lib/combat-round-event';
import { gameEventBus } from '@/lib/gameEventBus';

const ATTACK_WINDUP_MS = 700;
const ATTACK_RESULT_PAUSE_MS = 900;

export interface CombatCardEffect {
  kind: 'hit' | 'crit' | 'miss' | 'block';
  damage?: number;
  nonce: number;
}

function sleep(ms: number) {
  return new Promise<void>((resolve) => setTimeout(resolve, ms));
}

function prefersReducedMotion() {
  return (
    typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches
  );
}

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

function toCardEffect(event: CombatRoundEvent, nonce: number): CombatCardEffect {
  switch (event.type) {
    case 'CombatHitEvent':
      return { kind: event.isCritical ? 'crit' : 'hit', damage: event.damage, nonce };
    case 'CombatMissEvent':
      return { kind: 'miss', nonce };
    case 'CombatBlockEvent':
      return { kind: 'block', nonce };
  }
}

export function useCombatState() {
  const [fight, setFight] = useState<FightState | null>(null);
  const [activeAttackerId, setActiveAttackerId] = useState<string | null>(null);
  const [cardEffects, setCardEffects] = useState<Record<string, CombatCardEffect>>({});
  const [isPlayingBack, setIsPlayingBack] = useState(false);
  const [combatOutcome, setCombatOutcome] = useState<CombatOutcome | null>(null);

  const fightRef = useRef<FightState | null>(null);
  const playbackTokenRef = useRef(0);
  const nonceRef = useRef(0);

  useEffect(() => {
    fightRef.current = fight;
  }, [fight]);

  useEffect(() => {
    async function playRound(payload: CombatUpdatePayload) {
      const token = ++playbackTokenRef.current;
      const baseline = fightRef.current;

      if (baseline === null || payload.events.length === 0 || prefersReducedMotion()) {
        setFight(payload.fightState);
        return;
      }

      setIsPlayingBack(true);
      let working = baseline;

      for (const event of payload.events) {
        setActiveAttackerId(event.attackerId);
        await sleep(ATTACK_WINDUP_MS);
        if (playbackTokenRef.current !== token) {
          return;
        }

        working = applyRoundEventToFight(working, event);
        setFight(working);
        setActiveAttackerId(null);
        setCardEffects((current) => ({
          ...current,
          [event.targetId]: toCardEffect(event, ++nonceRef.current),
        }));

        await sleep(ATTACK_RESULT_PAUSE_MS);
        if (playbackTokenRef.current !== token) {
          return;
        }
      }

      // Catches up anything not carried by a discrete event - AP/MP costs, buffs, dots, hots,
      // conditions - so the display converges on the server's authoritative post-round state.
      setFight(payload.fightState);
      setIsPlayingBack(false);
    }

    const offStarted = gameEventBus.on('CombatStarted', (payload) => {
      playbackTokenRef.current += 1;
      setIsPlayingBack(false);
      setActiveAttackerId(null);
      setFight(payload);
    });
    const offUpdated = gameEventBus.on('CombatUpdated', (payload) => {
      void playRound(payload);
    });
    const offEnded = gameEventBus.on('CombatEnded', (outcome) => {
      playbackTokenRef.current += 1;
      setIsPlayingBack(false);
      setActiveAttackerId(null);
      // fight is deliberately left in place (not cleared to null) here - the outcome screen needs
      // the final FightState to keep rendering the combat console underneath it until the player
      // dismisses the outcome, at which point dismissOutcome clears both together.
      setCombatOutcome(outcome);
    });

    return () => {
      offStarted();
      offUpdated();
      offEnded();
    };
  }, []);

  const dismissOutcome = useCallback(() => {
    setCombatOutcome(null);
    setFight(null);
  }, []);

  return {
    fight,
    isInCombat: fight !== null,
    activeAttackerId,
    cardEffects,
    isPlayingBack,
    combatOutcome,
    dismissOutcome,
  };
}
