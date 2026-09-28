import type {
  CreaturesMoved,
  WeatherCondition,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { TerminalCombatOutcome } from '@/features/combat/terminal-combat-outcome';

export const OUTCOME_MARKER: Record<TerminalCombatOutcome, string> = {
  Victory: 'Victory!',
  Defeat: 'You have died',
  Fled: 'You escaped',
};

export const WEATHER_MARKER: Record<WeatherCondition, string> = {
  Clear: 'The sky clears',
  Cloudy: 'Clouds roll in',
  Rain: 'Rain begins to fall',
  Storm: 'A storm breaks',
  Snow: 'Snow begins to fall',
  Fog: 'A fog settles in',
};

function joinNames(names: string[]): string {
  if (names.length <= 1) {
    return names.join('');
  }
  return `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`;
}

export function formatMovement({ direction, names, placeName }: CreaturesMoved): string {
  const who = joinNames(names);
  if (direction === 'Arrived') {
    return placeName ? `${who} arrived from ${placeName}` : `${who} arrived`;
  }
  return placeName ? `${who} left, heading towards ${placeName}` : `${who} left`;
}
