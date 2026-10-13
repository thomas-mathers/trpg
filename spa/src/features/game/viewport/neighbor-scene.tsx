import { useLayoutEffect, useMemo, useRef } from 'react';
import type { Group } from 'three';

import type {
  NearbyBuildingSnapshot,
  NeighborSnapshot,
  PlacementWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { Roads } from './boundary-scene';
import { BuildingMesh } from './building-mesh';
import { DoorBatch } from './door-instances';
import { boundaryTowers, boundaryWalls, headingToYaw, toScenePosition } from './layout-math';
import { buildingStyle } from './model-styles';
import {
  neighborBoundary,
  neighborBuildings,
  neighborProps,
  neighborRoads,
} from './neighbor-layout';
import { Boxes, Walls } from './viewport-scene';

function neighborDoorPlacement({ placement, footprint }: NearbyBuildingSnapshot): PlacementWire {
  const distance = footprint.depth / 2;
  return {
    x: placement.x + distance * Math.sin(placement.angle),
    y: placement.y - distance * Math.cos(placement.angle),
    angle: placement.angle,
  };
}

export function NeighborDistricts({ neighbors }: { neighbors: NeighborSnapshot[] }) {
  const group = useRef<Group>(null);
  const roads = useMemo(() => neighborRoads(neighbors), [neighbors]);
  const buildings = useMemo(() => neighborBuildings(neighbors), [neighbors]);
  const doors = useMemo(() => buildings.map(neighborDoorPlacement), [buildings]);
  const props = useMemo(() => neighborProps(neighbors), [neighbors]);
  const boundary = useMemo(() => neighborBoundary(neighbors), [neighbors]);
  const walls = useMemo(() => boundaryWalls(boundary), [boundary]);
  const towers = useMemo(() => boundaryTowers(boundary), [boundary]);

  useLayoutEffect(() => {
    group.current?.traverse((object) => {
      object.castShadow = false;
    });
  }, [neighbors]);

  return (
    <group ref={group}>
      <Roads roads={roads} />
      <Walls walls={walls} towers={towers} />
      <Boxes props={props} buildings={[]} shadows={false} />
      <DoorBatch placements={doors} />
      {buildings.map(({ id, type, placement, footprint, floorCount }) => {
        const style = buildingStyle(type, floorCount);
        return (
          <group
            key={id}
            position={toScenePosition(placement.x, placement.y, style.height / 2)}
            rotation={[0, headingToYaw(placement.angle), 0]}
          >
            <BuildingMesh id={id} type={type} footprint={footprint} style={style} shadows={false} />
          </group>
        );
      })}
    </group>
  );
}
