import { useGuardEncounterState } from '@/features/encounters/hooks/use-guard-encounter-state';
import { useHostileEncounterState } from '@/features/encounters/hooks/use-hostile-encounter-state';
import { useSuspicionEncounterState } from '@/features/encounters/hooks/use-suspicion-encounter-state';
import { useTheftEncounterState } from '@/features/encounters/hooks/use-theft-encounter-state';
import { useTrapEncounterState } from '@/features/encounters/hooks/use-trap-encounter-state';

export function useHasActiveEncounter() {
  const hostileEncounter = useHostileEncounterState();
  const guardEncounter = useGuardEncounterState();
  const suspicionEncounter = useSuspicionEncounterState();
  const theftEncounter = useTheftEncounterState();
  const trapEncounter = useTrapEncounterState();
  return (
    hostileEncounter !== null ||
    guardEncounter !== null ||
    suspicionEncounter !== null ||
    theftEncounter !== null ||
    trapEncounter !== null
  );
}
