import { useEffect, useState } from 'react';

import type { SuspicionEncounterState } from '@/features/encounters/encounter';
import { gameEventBus } from '@/lib/game-event-bus';

export function useSuspicionEncounterState() {
  const [encounter, setEncounter] = useState<SuspicionEncounterState | null>(null);

  useEffect(() => {
    const unsubscribeStarted = gameEventBus.on('SuspicionEncounterStarted', setEncounter);
    const unsubscribeResolved = gameEventBus.on('SuspicionEncounterResolved', () =>
      setEncounter(null),
    );

    return () => {
      unsubscribeStarted();
      unsubscribeResolved();
    };
  }, []);

  return encounter;
}
