import { useEffect, useMemo, useState } from 'react';
import { BufferGeometry, Float32BufferAttribute } from 'three';

import type { CreatureStatusSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { toScenePosition } from './layout-math';

const LIFT = 0.15;

declare global {
  interface Window {
    paths?: (visible?: boolean) => void;
  }
}

function hueFor(id: string) {
  let hash = 0;
  for (const char of id) hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  return hash % 360;
}

function pathGeometry({ walk }: CreatureStatusSnapshot) {
  const walkPath = walk?.points ?? [];
  const positions: number[] = [];
  for (let i = 0; i < walkPath.length - 1; i++) {
    positions.push(
      ...toScenePosition(walkPath[i].x, walkPath[i].y, LIFT),
      ...toScenePosition(walkPath[i + 1].x, walkPath[i + 1].y, LIFT),
    );
  }

  const geometry = new BufferGeometry();
  geometry.setAttribute('position', new Float32BufferAttribute(positions, 3));
  return geometry;
}

function WalkPath({ creature }: { creature: CreatureStatusSnapshot }) {
  const geometry = useMemo(() => pathGeometry(creature), [creature.walk]);

  return (
    <lineSegments geometry={geometry}>
      <lineBasicMaterial color={`hsl(${hueFor(creature.id)}, 90%, 55%)`} />
    </lineSegments>
  );
}

export function DebugWalkPaths({ creatures }: { creatures: CreatureStatusSnapshot[] }) {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    if (!import.meta.env.DEV) return;
    window.paths = (next) => setVisible((current) => next ?? !current);
    return () => {
      delete window.paths;
    };
  }, []);

  if (!visible) return null;

  return (
    <group>
      {creatures
        .filter(({ walk }) => walk !== undefined)
        .map((creature) => (
          <WalkPath key={creature.id} creature={creature} />
        ))}
    </group>
  );
}
