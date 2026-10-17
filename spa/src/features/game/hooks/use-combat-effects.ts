import { useEffect, useRef, useState } from 'react';

import { gameEventBus } from '@/lib/game-event-bus';

import { toCombatTexts, type CombatText } from '../viewport/combat-text';

export interface FloatingText extends CombatText {
  id: number;
}

const TEXT_LIFETIME_MS = 1100;
let nextId = 0;

export function useCreatureCombatEffects(creatureId: string) {
  const [floaters, setFloaters] = useState<FloatingText[]>([]);
  const strikeRequested = useRef(false);

  useEffect(() => {
    const timers = new Set<ReturnType<typeof setTimeout>>();
    const unsubscribe = gameEventBus.on('CombatActions', (actions) => {
      const texts = toCombatTexts(actions);
      if (texts.some((text) => text.attackerId === creatureId)) {
        strikeRequested.current = true;
      }
      const mine = texts
        .filter((text) => text.targetId === creatureId)
        .map((text) => ({ ...text, id: nextId++ }));
      if (mine.length === 0) {
        return;
      }
      setFloaters((current) => [...current, ...mine]);
      const timer = setTimeout(() => {
        timers.delete(timer);
        setFloaters((current) => current.filter((f) => !mine.some((m) => m.id === f.id)));
      }, TEXT_LIFETIME_MS);
      timers.add(timer);
    });
    return () => {
      unsubscribe();
      timers.forEach(clearTimeout);
    };
  }, [creatureId]);

  return { floaters, strikeRequested };
}

export function useEngagedCreatureIds(): ReadonlySet<string> {
  const [engaged, setEngaged] = useState<ReadonlySet<string>>(new Set());

  useEffect(() => {
    const unsubscribeStarted = gameEventBus.on('CombatStarted', ({ combatants }) =>
      setEngaged(new Set(combatants.map((combatant) => combatant.id))),
    );
    const unsubscribeResolved = gameEventBus.on('CombatResolved', () => setEngaged(new Set()));
    return () => {
      unsubscribeStarted();
      unsubscribeResolved();
    };
  }, []);

  return engaged;
}
