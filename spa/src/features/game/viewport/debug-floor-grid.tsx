import { useEffect, useMemo, useState } from 'react';
import { BufferGeometry, Float32BufferAttribute } from 'three';

import type { FootprintWire } from '@/api/signalr-client/TRPG.GameSessions.Responses';

const CELL_SIZE = 0.5;
const LIFT = 0.02;
const MAJOR_EVERY = 2;

declare global {
  interface Window {
    grid?: (visible?: boolean) => void;
  }
}

function gridLines({ width, depth }: FootprintWire, major: boolean) {
  const positions: number[] = [];
  const stride = major ? CELL_SIZE * MAJOR_EVERY : CELL_SIZE;
  const isMajor = (value: number) => Math.abs((value / (CELL_SIZE * MAJOR_EVERY)) % 1) < 1e-6;

  for (let x = 0; x <= width + 1e-6; x += stride) {
    if (major === isMajor(x)) positions.push(x, LIFT, 0, x, LIFT, depth);
  }
  for (let z = 0; z <= depth + 1e-6; z += stride) {
    if (major === isMajor(z)) positions.push(0, LIFT, z, width, LIFT, z);
  }

  const geometry = new BufferGeometry();
  geometry.setAttribute('position', new Float32BufferAttribute(positions, 3));
  return geometry;
}

export function DebugFloorGrid({ size }: { size: FootprintWire }) {
  const [visible, setVisible] = useState(false);
  const minor = useMemo(() => gridLines(size, false), [size]);
  const major = useMemo(() => gridLines(size, true), [size]);

  useEffect(() => {
    if (!import.meta.env.DEV) return;
    window.grid = (next) => setVisible((current) => next ?? !current);
    return () => {
      delete window.grid;
    };
  }, []);

  if (!visible) return null;

  return (
    <group>
      <lineSegments geometry={minor}>
        <lineBasicMaterial color="#ffffff" transparent opacity={0.35} />
      </lineSegments>
      <lineSegments geometry={major}>
        <lineBasicMaterial color="#ffd54a" />
      </lineSegments>
    </group>
  );
}
