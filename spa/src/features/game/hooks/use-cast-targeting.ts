import { createContext, useContext } from 'react';

import type { AbilitySummary } from '@/api/client';

export interface CastTarget {
  id: string;
  name: string;
}

export interface CastTargeting {
  pendingAbility: AbilitySummary | null;
  selectAbility: (ability: AbilitySummary) => void;
  cancel: () => void;
  castOn: (target: CastTarget) => void;
}

const inertTargeting: CastTargeting = {
  pendingAbility: null,
  selectAbility: () => undefined,
  cancel: () => undefined,
  castOn: () => undefined,
};

export const CastTargetingContext = createContext<CastTargeting>(inertTargeting);

export function useCastTargeting(): CastTargeting {
  return useContext(CastTargetingContext);
}
