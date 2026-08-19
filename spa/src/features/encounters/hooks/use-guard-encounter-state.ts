import { useEffect, useState } from 'react';

import type { GuardEncounterState } from '@/features/encounters/encounter';
import { gameEventBus } from '@/lib/game-event-bus';

export function useGuardEncounterState() {
  const [encounter, setEncounter] = useState<GuardEncounterState | null>(null);

  useEffect(() => {
    const unsubscribeStarted = gameEventBus.on('GuardEncounterStarted', setEncounter);
    const unsubscribeResolved = gameEventBus.on('GuardEncounterResolved', () => setEncounter(null));

    return () => {
      unsubscribeStarted();
      unsubscribeResolved();
    };
  }, []);

  return encounter;
}

export function useHasActiveGuardEncounter() {
  return useGuardEncounterState() !== null;
}
