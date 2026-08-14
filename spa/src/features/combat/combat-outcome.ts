export type CombatOutcome = 'Ongoing' | 'Victory' | 'Defeat' | 'Fled';

export type TerminalCombatOutcome = Exclude<CombatOutcome, 'Ongoing'>;
