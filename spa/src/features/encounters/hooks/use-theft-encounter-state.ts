import { useEffect, useState } from 'react';

import type { TheftEncounterState } from '@/features/encounters/encounter';
import { gameEventBus } from '@/lib/game-event-bus';

export function useTheftEncounterState() {
  const [encounter, setEncounter] = useState<TheftEncounterState | null>(null);

  useEffect(() => {
    const unsubscribeStarted = gameEventBus.on('TheftEncounterStarted', setEncounter);
    const unsubscribeResolved = gameEventBus.on('TheftEncounterResolved', () => setEncounter(null));

    return () => {
      unsubscribeStarted();
      unsubscribeResolved();
    };
  }, []);

  return encounter;
}
