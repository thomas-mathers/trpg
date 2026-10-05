import type {
  LocationBoundarySnapshot,
  NearbyBuildingSnapshot,
  NearbyPropSnapshot,
  NeighborSnapshot,
  RoadSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

export function neighborBuildings(
  neighbors: NeighborSnapshot[] | undefined,
): NearbyBuildingSnapshot[] {
  return (neighbors ?? []).flatMap(({ buildings }) => buildings);
}

export function neighborProps(neighbors: NeighborSnapshot[] | undefined): NearbyPropSnapshot[] {
  return (neighbors ?? []).flatMap(({ props }) => props);
}

export function neighborRoads(neighbors: NeighborSnapshot[] | undefined): RoadSnapshot[] {
  return (neighbors ?? []).flatMap(({ roads }) => roads);
}

export function neighborBoundary(
  neighbors: NeighborSnapshot[] | undefined,
): LocationBoundarySnapshot {
  return {
    segments: (neighbors ?? []).flatMap(({ segments }) => segments),
    gates: [],
    openEdges: [],
  };
}
