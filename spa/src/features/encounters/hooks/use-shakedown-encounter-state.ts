import { useEffect, useState } from 'react';

import type { ShakedownEncounterState } from '@/features/encounters/encounter';
import { gameEventBus } from '@/lib/game-event-bus';

export function useShakedownEncounterState() {
  const [encounter, setEncounter] = useState<ShakedownEncounterState | null>(null);

  useEffect(() => {
    const unsubscribeStarted = gameEventBus.on('ShakedownEncounterStarted', setEncounter);
    const unsubscribeResolved = gameEventBus.on('ShakedownEncounterResolved', () =>
      setEncounter(null),
    );

    return () => {
      unsubscribeStarted();
      unsubscribeResolved();
    };
  }, []);

  return encounter;
}
