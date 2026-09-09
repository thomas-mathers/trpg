export type {
  GuardEncounterResolutionFact,
  GuardEncounterResolutionOutcome,
  GuardEncounterState,
  HostileEncounterMemberState,
  HostileEncounterResolutionFact,
  HostileEncounterResolutionOutcome,
  HostileEncounterState,
  SuspicionCause,
  SuspicionEncounterResolutionFact,
  SuspicionEncounterResolutionOutcome,
  SuspicionEncounterState,
  TheftEncounterResolutionFact,
  TheftEncounterResolutionOutcome,
  TheftEncounterState,
  TrapEncounterResolutionFact,
  TrapEncounterResolutionOutcome,
  TrapEncounterState,
  TrapKind,
} from '@/api/signalr-client/TRPG.Encounters.Responses';

export type EncounterActionName = 'Attack' | 'Evade' | 'Retreat';
export type GuardEncounterActionName = 'PayFine' | 'GoToJail' | 'ResistArrest';
export type SuspicionEncounterActionName = 'Comply' | 'Flee';
export type TheftEncounterActionName = 'Apologize' | 'Flee';
export type TrapEncounterActionName = 'Attempt' | 'Withdraw' | 'Disarm';
