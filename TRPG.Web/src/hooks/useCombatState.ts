import { useEffect, useState } from 'react';

import type { FightState } from '@/api/client';
import { gameEventBus } from '@/lib/gameEventBus';

export function useCombatState() {
  const [fight, setFight] = useState<FightState | null>(null);

  useEffect(() => {
    const offStarted = gameEventBus.on('CombatStarted', setFight);
    const offUpdated = gameEventBus.on('CombatUpdated', setFight);
    const offEnded = gameEventBus.on('CombatEnded', () => setFight(null));
    return () => {
      offStarted();
      offUpdated();
      offEnded();
    };
  }, []);

  return { fight, isInCombat: fight !== null };
}
