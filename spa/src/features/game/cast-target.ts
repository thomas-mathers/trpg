import type { AbilitySummary } from '@/api/client';

export function resolveCastTarget(
  ability: Pick<AbilitySummary, 'requiresTarget' | 'category'>,
  targetId: string | undefined,
  playerId: string,
): string | undefined {
  if (!ability.requiresTarget) return playerId;
  if (targetId) return targetId;
  return ability.category === 'Support' ? playerId : undefined;
}
