import { useCreatureCombatEffects } from '../hooks/use-combat-effects';
import { COMBAT_TEXT_COLORS, type CombatTextKind } from '../viewport/combat-text';

const ANIMATION_CLASS: Record<CombatTextKind, string> = {
  hit: 'combat-flash-float',
  crit: 'combat-flash-float-crit',
  miss: 'combat-flash-float-miss',
  block: 'combat-flash-float-miss',
};

export function PlayerCombatText({ playerId }: { playerId: string }) {
  const { floaters } = useCreatureCombatEffects(playerId);

  return (
    <div
      aria-hidden
      className="pointer-events-none absolute top-1/2 left-1/2 z-10 flex -translate-x-1/2 flex-col items-center gap-1"
    >
      {floaters.map((floater) => (
        <span
          key={floater.id}
          className={`${ANIMATION_CLASS[floater.kind]} text-3xl font-extrabold`}
          style={{ color: COMBAT_TEXT_COLORS[floater.kind] }}
        >
          {floater.text}
        </span>
      ))}
    </div>
  );
}
