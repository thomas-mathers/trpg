import type { CreatureStatusSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

type Targetable = Pick<CreatureStatusSnapshot, 'id' | 'condition' | 'placement'>;

export function cycleTarget(
  creatures: Targetable[],
  player: { x: number; y: number },
  currentId: string | undefined,
  step: 1 | -1,
): string | undefined {
  const ordered = creatures
    .filter(({ condition }) => condition !== 'Dead')
    .map(({ id, placement }) => ({
      id,
      distance: Math.hypot(placement.x - player.x, placement.y - player.y),
    }))
    .sort((a, b) => a.distance - b.distance);
  if (ordered.length === 0) return undefined;

  const index = ordered.findIndex(({ id }) => id === currentId);
  if (index === -1) return ordered[step === 1 ? 0 : ordered.length - 1].id;
  return ordered[(index + step + ordered.length) % ordered.length].id;
}
