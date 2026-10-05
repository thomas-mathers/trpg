import { useEffect, useMemo } from 'react';
import { BufferGeometry, Float32BufferAttribute } from 'three';

import type {
  CompassDirection,
  FootprintWire,
  RoadClassSnapshot,
  RoadSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { apronQuads, roadRenderOrder, roadRibbon } from './boundary-geometry';
import type { PlanarPoint } from './layout-math';

export const OUTDOOR_FLOOR_COLOR = '#777c79';
const ROAD_COLORS: Record<RoadClassSnapshot, string> = {
  Avenue: '#a89a82',
  Street: '#9a8b72',
  Lane: '#8a7c64',
};
const APRON_COLOR = ROAD_COLORS.Avenue;
const ROAD_OFFSET_FACTOR: Record<RoadClassSnapshot, number> = { Lane: -1, Street: -2, Avenue: -3 };

export function Roads({ roads }: { roads: RoadSnapshot[] }) {
  return (
    <>
      {roads.map((road) => (
        <RoadMesh key={roadKey(road.points)} {...road} />
      ))}
    </>
  );
}

function roadKey(points: RoadSnapshot['points']): string {
  const first = points[0];
  const last = points[points.length - 1];
  return `${first?.x}:${first?.y}:${last?.x}:${last?.y}`;
}

function RoadMesh({ points, width, class: roadClass }: RoadSnapshot) {
  const geometry = useMemo(() => {
    const { positions, indices } = roadRibbon(points, width);
    const ribbon = new BufferGeometry();
    ribbon.setAttribute('position', new Float32BufferAttribute(positions, 3));
    ribbon.setIndex(indices);
    ribbon.computeVertexNormals();
    return ribbon;
  }, [points, width, roadClass]);
  useEffect(() => () => geometry.dispose(), [geometry]);

  return (
    <mesh geometry={geometry} receiveShadow renderOrder={roadRenderOrder(roadClass)}>
      <meshLambertMaterial
        color={ROAD_COLORS[roadClass]}
        depthWrite={false}
        polygonOffset
        polygonOffsetFactor={ROAD_OFFSET_FACTOR[roadClass]}
        polygonOffsetUnits={ROAD_OFFSET_FACTOR[roadClass]}
      />
    </mesh>
  );
}

function apronGeometry(corners: PlanarPoint[]): BufferGeometry {
  const geometry = new BufferGeometry();
  geometry.setAttribute(
    'position',
    new Float32BufferAttribute(
      corners.flatMap(({ x, y }) => [x, 0, y]),
      3,
    ),
  );
  geometry.setAttribute(
    'normal',
    new Float32BufferAttribute(
      corners.flatMap(() => [0, 1, 0]),
      3,
    ),
  );
  geometry.setIndex([0, 1, 2, 0, 2, 3]);
  const [from, to, , back] = corners;
  const facesDown = (to.x - from.x) * (back.y - from.y) - (to.y - from.y) * (back.x - from.x) > 0;
  if (facesDown) geometry.setIndex([0, 2, 1, 0, 3, 2]);
  return geometry;
}

function ApronMesh({ corners }: { corners: PlanarPoint[] }) {
  const geometry = useMemo(() => apronGeometry(corners), [corners]);
  useEffect(() => () => geometry.dispose(), [geometry]);

  return (
    <mesh geometry={geometry} receiveShadow renderOrder={roadRenderOrder('Avenue')}>
      <meshLambertMaterial
        color={APRON_COLOR}
        depthWrite={false}
        polygonOffset
        polygonOffsetFactor={-3}
        polygonOffsetUnits={-3}
      />
    </mesh>
  );
}

export function RoadAprons({
  roads,
  size,
  openEdges,
}: {
  roads: RoadSnapshot[];
  size: FootprintWire;
  openEdges: CompassDirection[];
}) {
  const quads = useMemo(() => apronQuads(roads, size, openEdges), [roads, size, openEdges]);

  return (
    <>
      {quads.map(({ key, corners }) => (
        <ApronMesh key={key} corners={corners} />
      ))}
    </>
  );
}
