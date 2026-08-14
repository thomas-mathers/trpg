export interface HostileEncounterMemberState {
  name: string;
  creatureType: string;
  level: number;
}

export interface HostileEncounterState {
  encounterId: string;
  factionName: string;
  locationName: string;
  members: HostileEncounterMemberState[];
  allowedActions: string[];
}

export interface EncounterResolutionFact {
  encounterId: string;
  outcome: EncounterResolutionOutcome;
  factionName: string;
  locationName: string;
  memberNames: string[];
}

export type EncounterResolutionOutcome =
  | 'Evaded'
  | 'EvadeFailed'
  | 'Retreated'
  | 'RetreatFailed'
  | 'Attacked';

export type PlayerEncounterAction =
  | { type: 'AttackEncounterAction' }
  | { type: 'EvadeEncounterAction' }
  | { type: 'RetreatEncounterAction' };

export const encounterActionByName: Record<string, PlayerEncounterAction> = {
  Attack: { type: 'AttackEncounterAction' },
  Evade: { type: 'EvadeEncounterAction' },
  Retreat: { type: 'RetreatEncounterAction' },
};
