import { useMemo } from 'react';

import type { NeighborSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { Roads } from './boundary-scene';
import { DoorConnector } from './door-connector';
import { GreenSpaces } from './green-spaces';
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
      <GreenSpaces spaces={neighbors.flatMap(({ greenSpaces }) => greenSpaces)} />
      <Walls walls={walls} towers={towers} />
      <Boxes props={props} buildings={[]} />
      {buildings.map(({ id, type, placement, footprint, floorCount }) => {
        const { height, color } = buildingStyle(type, floorCount);
        const distance = footprint.depth / 2;
        const doorX = placement.x + distance * Math.sin(placement.angle);
        const doorY = placement.y - distance * Math.cos(placement.angle);

        return (
          <group key={id}>
            <mesh
              position={toScenePosition(placement.x, placement.y, height / 2)}
              rotation={[0, headingToYaw(placement.angle), 0]}
            >
              <boxGeometry args={[footprint.width, height, footprint.depth]} />
              <meshLambertMaterial color={color} />
            </mesh>
            <group
              position={toScenePosition(doorX, doorY)}
              rotation={[0, headingToYaw(placement.angle) + Math.PI, 0]}
            >
              <DoorConnector />
            </group>
          </group>
        );
      })}
    </>
  );
}
