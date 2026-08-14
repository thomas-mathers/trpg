import { useEffect, useState } from 'react';

import type { HostileEncounterState } from '@/features/encounters/encounter';
import { gameEventBus } from '@/lib/game-event-bus';

export function useEncounterState() {
  const [encounter, setEncounter] = useState<HostileEncounterState | null>(null);

  useEffect(() => {
    const unsubscribeStarted = gameEventBus.on('EncounterStarted', setEncounter);
    const unsubscribeResolved = gameEventBus.on('EncounterResolved', () => setEncounter(null));

    return () => {
      unsubscribeStarted();
      unsubscribeResolved();
    };
  }, []);

  return encounter;
}

export function useHasActiveEncounter() {
  return useEncounterState() !== null;
}
