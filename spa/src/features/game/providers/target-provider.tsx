import { useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

import { useScene } from '@/features/game/contexts/scene-context';

import { GameHubConnectionContext } from '../hooks/use-game-hub-connection';
import { TargetContext } from '../hooks/use-target';

export function TargetProvider({ children }: { children: ReactNode }) {
  const { scene } = useScene();
  const chatHub = useContext(GameHubConnectionContext)?.chatHub;
  const [targetId, setTargetId] = useState<string>();
  const target = scene.nearbyCreatures.find(({ id }) => id === targetId);

  useEffect(() => {
    chatHub?.sendSelectTarget(target?.id).catch(() => undefined);
  }, [chatHub, target?.id]);

  useEffect(() => {
    if (targetId && !target) {
      setTargetId(undefined);
    }
  }, [targetId, target]);

  const value = useMemo(() => ({ target, setTargetId }), [target]);

  return <TargetContext.Provider value={value}>{children}</TargetContext.Provider>;
}
