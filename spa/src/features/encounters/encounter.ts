export type {
  EncounterResolutionFact,
  EncounterResolutionOutcome,
  GuardEncounterResolutionFact,
  GuardEncounterResolutionOutcome,
  GuardEncounterState,
  HostileEncounterMemberState,
  HostileEncounterState,
} from '@/api/signalr-client/TRPG.Encounters.Responses';

export type EncounterActionName = 'Attack' | 'Evade' | 'Retreat';

export type GuardEncounterActionName = 'PayFine' | 'GoToJail' | 'ResistArrest';
