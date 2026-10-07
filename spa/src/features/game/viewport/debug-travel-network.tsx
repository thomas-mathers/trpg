import { useEffect, useMemo, useState } from 'react';
import { BufferGeometry, Float32BufferAttribute } from 'three';

import type { TravelNetworkSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { toScenePosition } from './layout-math';

const LIFT = 0.2;
const NODE_RADIUS = 0.25;
const PORT_RADIUS = 0.45;

declare global {
  interface Window {
    network?: (visible?: boolean) => void;
  }
}

function edgeGeometry(edges: TravelNetworkSnapshot['edges'], bidirectional: boolean) {
  const positions: number[] = [];
  for (const edge of edges) {
    if (edge.bidirectional !== bidirectional) continue;
    for (let i = 0; i < edge.points.length - 1; i++) {
      positions.push(
        ...toScenePosition(edge.points[i].x, edge.points[i].y, LIFT),
        ...toScenePosition(edge.points[i + 1].x, edge.points[i + 1].y, LIFT),
      );
    }
  }

  const geometry = new BufferGeometry();
  geometry.setAttribute('position', new Float32BufferAttribute(positions, 3));
  return geometry;
}

function NetworkGraph({ nodes, edges }: TravelNetworkSnapshot) {
  const twoWay = useMemo(() => edgeGeometry(edges, true), [edges]);
  const oneWay = useMemo(() => edgeGeometry(edges, false), [edges]);

  return (
    <group>
      <lineSegments geometry={twoWay}>
        <lineBasicMaterial color="#38bdf8" />
      </lineSegments>
      <lineSegments geometry={oneWay}>
        <lineBasicMaterial color="#f472b6" />
      </lineSegments>
      {nodes.map(({ id, position, isPort }) => (
        <mesh key={id} position={toScenePosition(position.x, position.y, LIFT)}>
          <sphereGeometry args={[isPort ? PORT_RADIUS : NODE_RADIUS, 12, 8]} />
          <meshBasicMaterial color={isPort ? '#facc15' : '#ffffff'} />
        </mesh>
      ))}
    </group>
  );
}

export function DebugTravelNetwork({ network }: { network?: TravelNetworkSnapshot }) {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    if (!import.meta.env.DEV) return;
    window.network = (next) => setVisible((current) => next ?? !current);
    return () => {
      delete window.network;
    };
  }, []);

  if (!visible || !network) return null;

  return <NetworkGraph {...network} />;
}
