import { createContext, useContext } from 'react';

import type { CreatureStatusSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

export interface TargetState {
  target: CreatureStatusSnapshot | undefined;
  setTargetId: (id: string | undefined) => void;
}

const noTarget: TargetState = {
  target: undefined,
  setTargetId: () => undefined,
};

export const TargetContext = createContext<TargetState>(noTarget);

export function useTarget(): TargetState {
  return useContext(TargetContext);
}
