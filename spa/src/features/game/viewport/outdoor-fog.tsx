import type { FootprintWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

export const HORIZON_COLOR = '#a3b5c6';

const FOG_NEAR_CAP = 40;
const FOG_SPAN = 150;
const FOG_CLEARANCE = 20;

export function outdoorFogRange({ width, depth }: FootprintWire) {
  const near = Math.min(FOG_NEAR_CAP, Math.hypot(width, depth) + FOG_CLEARANCE);
  return { near, far: near + FOG_SPAN };
}

export function OutdoorFog({ size }: { size: FootprintWire }) {
  const { near, far } = outdoorFogRange(size);

  return <fog attach="fog" args={[HORIZON_COLOR, near, far]} />;
}
