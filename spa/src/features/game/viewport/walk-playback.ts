import type { CreatureWalkSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

export interface WalkPose {
  x: number;
  y: number;
  angle: number;
  finished: boolean;
}

const FULL_TURN = Math.PI * 2;
const MAX_BLEND_METERS = 1.5;

export function walkedDistance(
  {
    startedAtGameTimeMilliseconds,
    metersPerGameSecond,
    pausedAtGameTimeMilliseconds,
  }: CreatureWalkSnapshot,
  gameTimeMilliseconds: number,
): number {
  const effectiveMilliseconds = Math.min(
    gameTimeMilliseconds,
    pausedAtGameTimeMilliseconds ?? Infinity,
  );
  const elapsedSeconds = (effectiveMilliseconds - startedAtGameTimeMilliseconds) / 1000;
  return Math.max(0, elapsedSeconds * metersPerGameSecond);
}

export function walkSignature({
  points,
  startedAtGameTimeMilliseconds,
  metersPerGameSecond,
  pausedAtGameTimeMilliseconds,
}: CreatureWalkSnapshot): string {
  const first = points[0];
  const last = points[points.length - 1];
  return `${startedAtGameTimeMilliseconds}:${pausedAtGameTimeMilliseconds}:${metersPerGameSecond}:${points.length}:${first?.x},${first?.y}:${last?.x},${last?.y}`;
}

export function walkPoseAt(points: CreatureWalkSnapshot['points'], distance: number): WalkPose {
  let remaining = distance;
  let heading = 0;

  for (let i = 0; i < points.length - 1; i++) {
    const from = points[i];
    const to = points[i + 1];
    const dx = to.x - from.x;
    const dy = to.y - from.y;
    const length = Math.hypot(dx, dy);
    if (length === 0) continue;

    heading = headingOf(dx, dy);
    if (remaining <= length) {
      const fraction = remaining / length;
      return {
        x: from.x + dx * fraction,
        y: from.y + dy * fraction,
        angle: heading,
        finished: false,
      };
    }
    remaining -= length;
  }

  const last = points[points.length - 1];
  return { x: last.x, y: last.y, angle: heading, finished: true };
}

function headingOf(dx: number, dy: number): number {
  const heading = Math.atan2(dx, -dy);
  return heading < 0 ? heading + FULL_TURN : heading;
}

export function blendOffset(
  from: { x: number; y: number } | null,
  target: { x: number; y: number },
): { x: number; y: number } {
  const offset = from ? { x: from.x - target.x, y: from.y - target.y } : { x: 0, y: 0 };
  return Math.hypot(offset.x, offset.y) <= MAX_BLEND_METERS ? offset : { x: 0, y: 0 };
}
