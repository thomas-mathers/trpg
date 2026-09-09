import type { CompassDirection } from '@/api/client';

const ROTATION: Record<CompassDirection, number> = {
  North: 0,
  Northeast: 45,
  East: 90,
  Southeast: 135,
  South: 180,
  Southwest: 225,
  West: 270,
  Northwest: 315,
};

// Somewhere with a position gets an arrow pointing the way the passage actually runs, which is the
// only sense of a dungeon's shape the player gets without a map. Everywhere else keeps a plain one.
export function ExitDirectionArrow({ direction }: { direction: CompassDirection | null }) {
  if (direction === null) {
    return (
      <span className="text-muted-foreground" aria-hidden>
        →
      </span>
    );
  }

  return (
    <span
      className="text-muted-foreground inline-block"
      style={{ transform: `rotate(${ROTATION[direction]}deg)` }}
      aria-label={`Leads ${direction.toLowerCase()}`}
      role="img"
    >
      ↑
    </span>
  );
}
