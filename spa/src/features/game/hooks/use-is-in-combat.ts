import { useEffect, useState } from 'react';

import { gameEventBus } from '@/lib/game-event-bus';

export function useIsInCombat() {
  const [isInCombat, setIsInCombat] = useState(false);

  useEffect(() => {
    const unsubscribeStarted = gameEventBus.on('CombatStarted', () => setIsInCombat(true));
    const unsubscribeResolved = gameEventBus.on('CombatResolved', () => setIsInCombat(false));

    return () => {
      unsubscribeStarted();
      unsubscribeResolved();
    };
  }, []);

  return isInCombat;
}
