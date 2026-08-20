export type {
  GuardEncounterResolutionFact,
  GuardEncounterResolutionOutcome,
  GuardEncounterState,
  HostileEncounterMemberState,
  HostileEncounterResolutionFact,
  HostileEncounterResolutionOutcome,
  HostileEncounterState,
} from '@/api/signalr-client/TRPG.Encounters.Responses';

export type EncounterActionName = 'Attack' | 'Evade' | 'Retreat';
export type GuardEncounterActionName = 'PayFine' | 'GoToJail' | 'ResistArrest';
