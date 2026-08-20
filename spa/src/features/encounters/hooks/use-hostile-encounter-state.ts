import { useEffect, useState } from 'react';

import type { HostileEncounterState } from '@/features/encounters/encounter';
import { gameEventBus } from '@/lib/game-event-bus';

export function useHostileEncounterState() {
  const [encounter, setEncounter] = useState<HostileEncounterState | null>(null);

  useEffect(() => {
    const unsubscribeStarted = gameEventBus.on('HostileEncounterStarted', setEncounter);
    const unsubscribeResolved = gameEventBus.on('HostileEncounterResolved', () =>
      setEncounter(null),
    );

    return () => {
      unsubscribeStarted();
      unsubscribeResolved();
    };
  }, []);

  return encounter;
}
