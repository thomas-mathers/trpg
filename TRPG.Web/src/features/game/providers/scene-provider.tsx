import { useEffect, useState, type ReactNode } from 'react';

import type { SceneSnapshot } from '@/api/client';
import { SceneContext, SessionContext } from '@/features/game/contexts/scene-context';
import { gameEventBus } from '@/lib/game-event-bus';

interface SceneProviderProps {
  sessionId: string;
  children: ReactNode;
}

export function SceneProvider({ sessionId, children }: SceneProviderProps) {
  const [scene, setScene] = useState<SceneSnapshot>();

  useEffect(() => gameEventBus.on('SceneSnapshot', setScene), []);

  return (
    <SessionContext.Provider value={sessionId}>
      <SceneContext.Provider value={scene}>{children}</SceneContext.Provider>
    </SessionContext.Provider>
  );
}
