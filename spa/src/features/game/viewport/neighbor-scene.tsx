import { useMemo } from 'react';

import type { NeighborSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { Roads } from './boundary-scene';
import { boundaryTowers, boundaryWalls, headingToYaw, toScenePosition } from './layout-math';
import { buildingStyle } from './model-styles';
import {
  neighborBoundary,
  neighborBuildings,
  neighborProps,
  neighborRoads,
} from './neighbor-layout';
import { Boxes, Walls } from './viewport-scene';

export function NeighborDistricts({ neighbors }: { neighbors: NeighborSnapshot[] }) {
  const roads = useMemo(() => neighborRoads(neighbors), [neighbors]);
  const buildings = useMemo(() => neighborBuildings(neighbors), [neighbors]);
  const props = useMemo(() => neighborProps(neighbors), [neighbors]);
  const boundary = useMemo(() => neighborBoundary(neighbors), [neighbors]);
  const walls = useMemo(() => boundaryWalls(boundary), [boundary]);
  const towers = useMemo(() => boundaryTowers(boundary), [boundary]);

  return (
    <>
      <Roads roads={roads} />
      <Walls walls={walls} towers={towers} />
      <Boxes props={props} buildings={[]} />
      {buildings.map(({ id, type, placement, footprint, floorCount }) => {
        const { height, color } = buildingStyle(type, floorCount);

        return (
          <mesh
            key={id}
            position={toScenePosition(placement.x, placement.y, height / 2)}
            rotation={[0, headingToYaw(placement.angle), 0]}
          >
            <boxGeometry args={[footprint.width, height, footprint.depth]} />
            <meshLambertMaterial color={color} />
          </mesh>
        );
      })}
    </>
  );
}
