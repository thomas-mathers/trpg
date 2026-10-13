export interface Gait {
  phase: number;
  amount: number;
}

const STRIDE_METERS = 1.25;
const TELEPORT_METERS = 2;
const MIN_SPEED = 0.1;
const BLEND_RATE = 12;

export function advanceGait(gait: Gait, distance: number, dt: number, height: number): void {
  const moved = distance < TELEPORT_METERS ? distance : 0;
  const walking = dt > 0 && moved / dt > MIN_SPEED;
  gait.phase = (gait.phase + (moved / (STRIDE_METERS * height)) * 2 * Math.PI) % (2 * Math.PI);
  const target = walking ? 1 : 0;
  gait.amount += (target - gait.amount) * (1 - Math.exp(-BLEND_RATE * dt));
}
