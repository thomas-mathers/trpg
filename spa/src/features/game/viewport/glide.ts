export interface Glide {
  fromX: number;
  fromY: number;
  toX: number;
  toY: number;
  startedAt: number;
  duration: number;
}

export const MIN_GLIDE_MS = 500;
export const MAX_GLIDE_MS = 6000;

export function settledAt(x: number, y: number, now: number): Glide {
  return { fromX: x, fromY: y, toX: x, toY: y, startedAt: now, duration: 1 };
}

export function glidePosition(glide: Glide, now: number): { x: number; y: number } {
  const fraction = Math.min(Math.max((now - glide.startedAt) / glide.duration, 0), 1);
  return {
    x: glide.fromX + (glide.toX - glide.fromX) * fraction,
    y: glide.fromY + (glide.toY - glide.fromY) * fraction,
  };
}

// Spans the previous update gap so steady updates read as continuous walking.
export function glideTo(
  current: Glide,
  x: number,
  y: number,
  now: number,
  lastUpdateAt: number,
): Glide {
  const { x: fromX, y: fromY } = glidePosition(current, now);
  const duration = Math.min(Math.max(now - lastUpdateAt, MIN_GLIDE_MS), MAX_GLIDE_MS);
  return { fromX, fromY, toX: x, toY: y, startedAt: now, duration };
}
