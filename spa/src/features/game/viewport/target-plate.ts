import type { CreatureStatusSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

export interface PlateBar {
  color: string;
  fraction: number;
}

export interface TargetPlate {
  title: string;
  standingColor: string;
  bars: PlateBar[];
}

const HOSTILE_COLOR = '#ef4444';
const NEUTRAL_COLOR = '#facc15';
const FRIENDLY_COLOR = '#4ade80';
const HP_COLOR = '#d6453d';
const AP_COLOR = '#d49a2a';
const MP_COLOR = '#4a8fd6';

export function standingColor(reputation: number | undefined): string {
  if (reputation === undefined || reputation === 0) return NEUTRAL_COLOR;
  return reputation > 0 ? FRIENDLY_COLOR : HOSTILE_COLOR;
}

function bar(color: string, current: number, maximum: number): PlateBar[] {
  if (maximum <= 0) return [];
  return [{ color, fraction: Math.min(1, Math.max(0, current / maximum)) }];
}

export function targetPlate(creature: CreatureStatusSnapshot): TargetPlate {
  const { name, level, condition, reputation } = creature;
  const dead = condition === 'Dead';
  const title = [name, `Lv ${level}`, condition === 'Awake' ? undefined : condition]
    .filter(Boolean)
    .join(' · ');

  return {
    title,
    standingColor: standingColor(reputation),
    bars: dead
      ? []
      : [
          ...bar(HP_COLOR, creature.currentHp, creature.maximumHp),
          ...bar(AP_COLOR, creature.currentAp, creature.maximumAp),
          ...bar(MP_COLOR, creature.currentMp, creature.maximumMp),
        ],
  };
}
