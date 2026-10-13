import { createContext, useContext } from 'react';

import type { AbilitySummary } from '@/api/client';

export interface CastTargeting {
  selectAbility: (ability: AbilitySummary) => void;
}

const inertTargeting: CastTargeting = {
  selectAbility: () => undefined,
};

export const CastTargetingContext = createContext<CastTargeting>(inertTargeting);

export function useCastTargeting(): CastTargeting {
  return useContext(CastTargetingContext);
}
