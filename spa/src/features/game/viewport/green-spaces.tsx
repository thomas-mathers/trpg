import { useEffect, useMemo } from 'react';
import { Shape, ShapeGeometry } from 'three';

import type { GreenSpaceSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

interface Lawn {
  id: string;
  x: number;
  y: number;
  width: number;
  depth: number;
}

const GRASS_HEIGHT = 0.012;

export function residentialLawns(spaces: GreenSpaceSnapshot[]): Lawn[] {
  return spaces.map(({ id, placement, footprint }) => ({
    id,
    x: placement.x,
    y: placement.y,
    width: footprint.width,
    depth: footprint.depth,
  }));
}

function lawnGeometry(width: number, depth: number) {
  const x = width / 2;
  const y = depth / 2;
  const shape = new Shape();
  shape.moveTo(-x, -y);
  shape.lineTo(x, -y);
  shape.lineTo(x, y);
  shape.lineTo(-x, y);
  shape.closePath();
  return new ShapeGeometry(shape);
}

function LawnMesh({ id, x, y, width, depth }: Lawn) {
  const geometry = useMemo(() => lawnGeometry(width, depth), [width, depth]);
  useEffect(() => () => geometry.dispose(), [geometry]);

  return (
    <mesh
      key={id}
      rotation={[-Math.PI / 2, 0, 0]}
      position={[x, GRASS_HEIGHT, y]}
      geometry={geometry}
      receiveShadow
    >
      <meshLambertMaterial color="#506b42" />
    </mesh>
  );
}

export function GreenSpaces({ spaces }: { spaces: GreenSpaceSnapshot[] }) {
  const lawns = useMemo(() => residentialLawns(spaces), [spaces]);
  return lawns.map((lawn) => <LawnMesh key={lawn.id} {...lawn} />);
}
