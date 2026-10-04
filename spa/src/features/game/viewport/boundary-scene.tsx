import { useEffect, useMemo } from 'react';
import { BufferGeometry, Color, Float32BufferAttribute } from 'three';

import type {
  FootprintWire,
  LocationBoundarySnapshot,
  NearbyExitSnapshot,
  RoadSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import {
  extrudedQuad,
  isOnOpenEdge,
  openEdgeSegment,
  outwardNormal,
  roadRibbon,
} from './boundary-geometry';
import { toScenePosition, type PlanarPoint } from './layout-math';

export const OUTDOOR_FLOOR_COLOR = '#6f6350';
const ROAD_COLOR = '#9a8b72';
const HORIZON_COLOR = '#9bb7d4';
const FADE_DEPTH = 10;
const FADE_LIFT = 0.02;
const POST_SIZE = 0.3;
const POST_HEIGHT = 2.4;
const POST_COLOR = '#5b4a36';
const POST_HALF_SPAN = 1;

interface FadedQuad {
  corners: PlanarPoint[];
  inner: string;
  outer: string;
  lift: number;
}

function fadedQuadGeometry({ corners, inner, outer, lift }: FadedQuad): BufferGeometry {
  const geometry = new BufferGeometry();
  const colors = [inner, inner, outer, outer].flatMap((hex) => new Color(hex).toArray());
  geometry.setAttribute(
    'position',
    new Float32BufferAttribute(
      corners.flatMap(({ x, y }) => [x, lift, y]),
      3,
    ),
  );
  geometry.setAttribute('color', new Float32BufferAttribute(colors, 3));
  geometry.setIndex([0, 1, 2, 0, 2, 3]);
  geometry.computeVertexNormals();
  if (geometry.getAttribute('normal').getY(0) < 0) {
    geometry.setIndex([0, 2, 1, 0, 3, 2]);
    geometry.computeVertexNormals();
  }
  return geometry;
}

function FadedQuadMesh({ corners, inner, outer, lift }: FadedQuad) {
  const geometry = useMemo(
    () => fadedQuadGeometry({ corners, inner, outer, lift }),
    [corners, inner, outer, lift],
  );
  useEffect(() => () => geometry.dispose(), [geometry]);

  return (
    <mesh geometry={geometry} receiveShadow>
      <meshLambertMaterial vertexColors />
    </mesh>
  );
}

export function Roads({ roads }: { roads: RoadSnapshot[] }) {
  return (
    <>
      {roads.map(({ points, width }) => (
        <RoadMesh key={`${points[0]?.x}:${points[0]?.y}`} points={points} width={width} />
      ))}
    </>
  );
}

function RoadMesh({ points, width }: RoadSnapshot) {
  const geometry = useMemo(() => {
    const { positions, indices } = roadRibbon(points, width);
    const ribbon = new BufferGeometry();
    ribbon.setAttribute('position', new Float32BufferAttribute(positions, 3));
    ribbon.setIndex(indices);
    ribbon.computeVertexNormals();
    return ribbon;
  }, [points, width]);
  useEffect(() => () => geometry.dispose(), [geometry]);

  return (
    <mesh geometry={geometry} receiveShadow>
      <meshLambertMaterial color={ROAD_COLOR} />
    </mesh>
  );
}

export function RoadAprons({ roads, size }: { roads: RoadSnapshot[]; size: FootprintWire }) {
  const quads = useMemo(
    () =>
      roads.flatMap(({ points, width }) => {
        const start = points[0];
        const normal = start && outwardNormal(start, size);
        if (!start || !normal) return [];
        const half = width / 2;
        const from = { x: start.x - normal.y * half, y: start.y + normal.x * half };
        const to = { x: start.x + normal.y * half, y: start.y - normal.x * half };
        return [
          { key: `${start.x}:${start.y}`, corners: extrudedQuad(from, to, normal, FADE_DEPTH) },
        ];
      }),
    [roads, size],
  );

  return (
    <>
      {quads.map(({ key, corners }) => (
        <FadedQuadMesh
          key={key}
          corners={corners}
          inner={ROAD_COLOR}
          outer={HORIZON_COLOR}
          lift={FADE_LIFT * 2}
        />
      ))}
    </>
  );
}

export function OpenEdgeFades({
  boundary,
  size,
}: {
  boundary: LocationBoundarySnapshot;
  size: FootprintWire;
}) {
  const { openEdges } = boundary;
  const quads = useMemo(
    () =>
      openEdges.flatMap((direction) => {
        const segment = openEdgeSegment(direction, size);
        if (!segment) return [];
        const [from, to] = segment;
        const normal = outwardNormal(midpoint(from, to), size);
        return normal ? [{ direction, corners: extrudedQuad(from, to, normal, FADE_DEPTH) }] : [];
      }),
    [openEdges, size],
  );

  return (
    <>
      {quads.map(({ direction, corners }) => (
        <FadedQuadMesh
          key={direction}
          corners={corners}
          inner={OUTDOOR_FLOOR_COLOR}
          outer={HORIZON_COLOR}
          lift={FADE_LIFT}
        />
      ))}
    </>
  );
}

export function OpenEdgeGatePosts({
  exits,
  boundary,
  size,
}: {
  exits: NearbyExitSnapshot[];
  boundary: LocationBoundarySnapshot;
  size: FootprintWire;
}) {
  const gateIds = new Set(boundary.gates.map(({ connectorId }) => connectorId));
  const open = exits.filter(
    ({ connectorId, stairs, placement }) =>
      !stairs && !gateIds.has(connectorId) && isOnOpenEdge(placement, size, boundary.openEdges),
  );

  return (
    <>
      {open.flatMap(({ connectorId, placement }) =>
        [-POST_HALF_SPAN, POST_HALF_SPAN].map((offset) => {
          const normal = outwardNormal(placement, size);
          const across = normal ? { x: -normal.y, y: normal.x } : { x: 1, y: 0 };
          return (
            <mesh
              castShadow
              receiveShadow
              key={`${connectorId}:${offset}`}
              position={toScenePosition(
                placement.x + across.x * offset,
                placement.y + across.y * offset,
                POST_HEIGHT / 2,
              )}
            >
              <boxGeometry args={[POST_SIZE, POST_HEIGHT, POST_SIZE]} />
              <meshLambertMaterial color={POST_COLOR} />
            </mesh>
          );
        }),
      )}
    </>
  );
}

function midpoint(from: PlanarPoint, to: PlanarPoint): PlanarPoint {
  return { x: (from.x + to.x) / 2, y: (from.y + to.y) / 2 };
}
