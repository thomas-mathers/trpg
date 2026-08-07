import { createContext, useContext } from 'react';

import type { SceneSnapshot } from '@/api/client';

export const SessionContext = createContext<string | null>(null);
export const PlayerIdContext = createContext<string | undefined>(undefined);
export const SceneContext = createContext<SceneSnapshot | undefined>(undefined);

export function useSessionId() {
  const sessionId = useContext(SessionContext);
  if (!sessionId) {
    throw new Error('useSessionId must be used within a SceneProvider');
  }
  return sessionId;
}

export function useScene() {
  return useContext(SceneContext);
}

export function usePlayerId() {
  return useContext(PlayerIdContext);
}
