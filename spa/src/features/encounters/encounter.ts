export type {
  GuardEncounterResolutionFact,
  GuardEncounterResolutionOutcome,
  GuardEncounterState,
  HostileEncounterMemberState,
  HostileEncounterResolutionFact,
  HostileEncounterResolutionOutcome,
  HostileEncounterState,
  TheftEncounterResolutionFact,
  TheftEncounterResolutionOutcome,
  TheftEncounterState,
} from '@/api/signalr-client/TRPG.Encounters.Responses';

export type EncounterActionName = 'Attack' | 'Evade' | 'Retreat';
export type GuardEncounterActionName = 'PayFine' | 'GoToJail' | 'ResistArrest';
export type TheftEncounterActionName = 'Apologize' | 'Flee';
