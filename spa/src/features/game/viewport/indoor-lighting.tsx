import { useMemo } from 'react';
import { PointLight } from 'three/webgpu';

import type {
  FootprintWire,
  NearbyPropSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { createFireLight, isFireSource } from './indoor-fire-light';
import { WALL_HEIGHT } from './layout-math';

function FireLight({ prop, size }: { prop: NearbyPropSnapshot; size: FootprintWire }) {
  const light = useMemo(() => createFireLight(prop, size), [prop, size]);
  return <primitive object={light} />;
}

function CeilingLight({ size: { width, depth } }: { size: FootprintWire }) {
  const light = useMemo(() => {
    const point = new PointLight('#fff0d6', 4);
    point.position.set(width / 2, WALL_HEIGHT - 0.2, depth / 2);
    point.decay = 1;
    point.castShadow = true;
    point.shadow.mapSize.set(512, 512);
    point.shadow.camera.near = 0.1;
    point.shadow.camera.far = Math.hypot(width / 2, depth / 2, WALL_HEIGHT) + 1;
    point.shadow.camera.updateProjectionMatrix();
    point.shadow.normalBias = 0.02;
    point.shadow.radius = 3;
    return point;
  }, [width, depth]);
  return <primitive object={light} />;
}

export function IndoorLighting({
  size,
  props,
}: {
  size: FootprintWire;
  props: NearbyPropSnapshot[];
}) {
  const fires = props.filter(isFireSource);
  return (
    <>
      <ambientLight intensity={fires.length > 0 ? 0.45 : 0.7} />
      <CeilingLight size={size} />
      {fires.map((prop) => (
        <FireLight key={prop.id} prop={prop} size={size} />
      ))}
    </>
  );
}
