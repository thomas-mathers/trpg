import { useEffect, useMemo } from 'react';
import { BufferGeometry, Float32BufferAttribute } from 'three';

import type {
  CompassDirection,
  FootprintWire,
  RoadClassSnapshot,
  RoadSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import {
  apronQuads,
  edgeAttribute,
  NO_CAP_FADE,
  roadRenderOrder,
  roadRibbon,
} from './boundary-geometry';
import type { PlanarPoint } from './layout-math';
import { createRoadMaterial, ROAD_EDGE_ATTRIBUTE, type RoadSurface } from './road-material';

export const OUTDOOR_FLOOR_COLOR = '#6f7658';
const ROAD_COLORS: Record<RoadClassSnapshot, string> = {
  Avenue: '#b5a98f',
  Street: '#9d8e74',
  Lane: '#837358',
};
const ROAD_SURFACES: Record<RoadClassSnapshot, RoadSurface> = {
  Avenue: 'cobble',
  Street: 'gravel',
  Lane: 'dirt',
};
const ROAD_OFFSET_FACTOR: Record<RoadClassSnapshot, number> = { Lane: -1, Street: -2, Avenue: -3 };

function layeredRoadMaterial(roadClass: RoadClassSnapshot) {
  const material = createRoadMaterial(ROAD_SURFACES[roadClass], ROAD_COLORS[roadClass]);
  material.depthWrite = false;
  material.polygonOffset = true;
  material.polygonOffsetFactor = ROAD_OFFSET_FACTOR[roadClass];
  material.polygonOffsetUnits = ROAD_OFFSET_FACTOR[roadClass];
  return material;
}

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
    const { positions, indices, edges } = roadRibbon(points, width);
    const ribbon = new BufferGeometry();
    ribbon.setAttribute('position', new Float32BufferAttribute(positions, 3));
    ribbon.setAttribute(ROAD_EDGE_ATTRIBUTE, new Float32BufferAttribute(edges, 4));
    ribbon.setIndex(indices);
    ribbon.computeVertexNormals();
    return ribbon;
  }, [points, width, roadClass]);
  const material = useMemo(() => layeredRoadMaterial(roadClass), [roadClass]);
  useEffect(() => () => geometry.dispose(), [geometry]);
  useEffect(() => () => material.dispose(), [material]);

  return (
    <mesh
      geometry={geometry}
      material={material}
      receiveShadow
      renderOrder={roadRenderOrder(roadClass)}
    />
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
  const [from, to, , back] = corners;
  const half = Math.hypot(to.x - from.x, to.y - from.y) / 2;
  geometry.setAttribute(
    ROAD_EDGE_ATTRIBUTE,
    new Float32BufferAttribute(
      edgeAttribute(half, Math.hypot(back.x - from.x, back.y - from.y), NO_CAP_FADE, NO_CAP_FADE),
      4,
    ),
  );
  geometry.setIndex([0, 1, 2, 0, 2, 3]);
  const facesDown = (to.x - from.x) * (back.y - from.y) - (to.y - from.y) * (back.x - from.x) > 0;
  if (facesDown) geometry.setIndex([0, 2, 1, 0, 3, 2]);
  return geometry;
}

function ApronMesh({ corners }: { corners: PlanarPoint[] }) {
  const geometry = useMemo(() => apronGeometry(corners), [corners]);
  const material = useMemo(() => layeredRoadMaterial('Avenue'), []);
  useEffect(() => () => geometry.dispose(), [geometry]);
  useEffect(() => () => material.dispose(), [material]);

  return (
    <mesh
      geometry={geometry}
      material={material}
      receiveShadow
      renderOrder={roadRenderOrder('Avenue')}
    />
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
