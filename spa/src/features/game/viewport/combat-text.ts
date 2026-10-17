import type { CombatActionResult } from '@/api/signalr-client/TRPG.Combat.Responses';

export type CombatTextKind = 'hit' | 'crit' | 'miss' | 'block';

export interface CombatText {
  attackerId: string;
  targetId: string;
  kind: CombatTextKind;
  text: string;
}

export const COMBAT_TEXT_COLORS: Record<CombatTextKind, string> = {
  hit: '#ffffff',
  crit: '#ffd23f',
  miss: '#d4d4d4',
  block: '#6fb7ff',
};

export function toCombatTexts(actions: CombatActionResult[]): CombatText[] {
  return actions.flatMap((action): CombatText[] => {
    const base = { attackerId: action.attackerId, targetId: action.targetId };
    switch (action.outcome) {
      case 'Hit':
        return action.isCritical
          ? [{ ...base, kind: 'crit' as const, text: `${action.damage ?? 0}!` }]
          : [{ ...base, kind: 'hit' as const, text: `${action.damage ?? 0}` }];
      case 'Miss':
        return [{ ...base, kind: 'miss' as const, text: 'Miss' }];
      case 'Block':
        return [{ ...base, kind: 'block' as const, text: 'Blocked' }];
      default:
        return [];
    }
  });
}

export function yawToward(from: { x: number; z: number }, to: { x: number; z: number }): number {
  return -Math.atan2(to.x - from.x, -(to.z - from.z));
}
