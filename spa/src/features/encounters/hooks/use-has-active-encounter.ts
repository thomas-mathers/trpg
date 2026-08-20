import { useGuardEncounterState } from '@/features/encounters/hooks/use-guard-encounter-state';
import { useHostileEncounterState } from '@/features/encounters/hooks/use-hostile-encounter-state';

export function useHasActiveEncounter() {
  const hostileEncounter = useHostileEncounterState();
  const guardEncounter = useGuardEncounterState();
  return hostileEncounter !== null || guardEncounter !== null;
}
