import { useGuardEncounterState } from '@/features/encounters/hooks/use-guard-encounter-state';
import { useHostileEncounterState } from '@/features/encounters/hooks/use-hostile-encounter-state';
import { useTheftEncounterState } from '@/features/encounters/hooks/use-theft-encounter-state';

export function useHasActiveEncounter() {
  const hostileEncounter = useHostileEncounterState();
  const guardEncounter = useGuardEncounterState();
  const theftEncounter = useTheftEncounterState();
  return hostileEncounter !== null || guardEncounter !== null || theftEncounter !== null;
}
