import { useEffect, useState } from 'react';

import type { TrapEncounterState } from '@/features/encounters/encounter';
import { gameEventBus } from '@/lib/game-event-bus';

export function useTrapEncounterState() {
  const [encounter, setEncounter] = useState<TrapEncounterState | null>(null);

  useEffect(() => {
    const unsubscribeStarted = gameEventBus.on('TrapEncounterStarted', setEncounter);
    const unsubscribeResolved = gameEventBus.on('TrapEncounterResolved', () => setEncounter(null));

    return () => {
      unsubscribeStarted();
      unsubscribeResolved();
    };
  }, []);

  return encounter;
}
