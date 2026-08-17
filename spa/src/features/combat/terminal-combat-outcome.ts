import type { CombatOutcome } from '@/api/signalr-client/TRPG.Combat.Responses';

export type TerminalCombatOutcome = Exclude<CombatOutcome, 'Ongoing'>;
