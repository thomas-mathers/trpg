import type { TerminalCombatOutcome } from '@/features/combat/terminal-combat-outcome';

export const OUTCOME_MARKER: Record<TerminalCombatOutcome, string> = {
  Victory: 'Victory!',
  Defeat: 'You have died',
  Fled: 'You escaped',
};
