import { useEffect, useMemo, useState, type ReactNode } from 'react';

import { useScene } from '@/features/game/contexts/scene-context';

import { TargetContext } from '../hooks/use-target';

export function TargetProvider({ children }: { children: ReactNode }) {
  const { scene } = useScene();
  const [targetId, setTargetId] = useState<string>();
  const target = scene.nearbyCreatures.find(({ id }) => id === targetId);

  useEffect(() => {
    if (targetId && !target) {
      setTargetId(undefined);
    }
  }, [targetId, target]);

  const value = useMemo(() => ({ target, setTargetId }), [target]);

  return <TargetContext.Provider value={value}>{children}</TargetContext.Provider>;
}
