import { describe, expect, it } from 'vitest';

import type { NeighborSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import {
  neighborBoundary,
  neighborBuildings,
  neighborProps,
  neighborRoads,
} from './neighbor-layout';

const placement = { x: 1, y: 2, angle: 0 };
const footprint = { width: 3, depth: 4 };

function neighbor(
  buildingCount: number,
  segmentCount: number,
  propCount = 0,
  roadCount = 0,
): NeighborSnapshot {
  return {
    locationId: crypto.randomUUID(),
    roads: Array.from({ length: roadCount }, () => ({
      points: [
        { x: 0, y: 0 },
        { x: 5, y: 0 },
      ],
      width: 2.5,
      class: 'Street' as const,
    })),
    props: Array.from({ length: propCount }, () => ({
      id: crypto.randomUUID(),
      name: 'Crate',
      description: 'A crate',
      type: 'Furniture',
      isOccupied: false,
      isOccupiedByPlayer: false,
      model: 'ContainerCrate' as const,
      placement,
      footprint,
    })),
    buildings: Array.from({ length: buildingCount }, () => ({
      id: crypto.randomUUID(),
      name: 'House',
      type: 'House',
      typeDescription: 'A house',
      placement,
      footprint,
      floorCount: 1,
    })),
    segments: Array.from({ length: segmentCount }, () => ({
      kind: 'Wall' as const,
      placement,
      footprint,
    })),
  };
}

describe('neighborBuildings', () => {
  it('gathers the buildings of every neighbor', () => {
    // Act
    const buildings = neighborBuildings([neighbor(2, 0), neighbor(1, 0)]);

    // Assert
    expect(buildings).toHaveLength(3);
  });

  it('is empty when the scene has no neighbors', () => {
    // Act
    const buildings = neighborBuildings(undefined);

    // Assert
    expect(buildings).toEqual([]);
  });
});

describe('neighborProps', () => {
  it('gathers the props of every neighbor', () => {
    // Act
    const props = neighborProps([neighbor(0, 0, 2), neighbor(0, 0, 1)]);

    // Assert
    expect(props).toHaveLength(3);
  });
});

describe('neighborRoads', () => {
  it('gathers the roads of every neighbor', () => {
    // Act
    const roads = neighborRoads([neighbor(0, 0, 0, 2), neighbor(0, 0, 0, 1)]);

    // Assert
    expect(roads).toHaveLength(3);
  });

  it('is empty when the scene has no neighbors', () => {
    // Act
    const roads = neighborRoads(undefined);

    // Assert
    expect(roads).toEqual([]);
  });
});

describe('neighborBoundary', () => {
  it('gathers the walls of every neighbor without gates', () => {
    // Act
    const boundary = neighborBoundary([neighbor(0, 3), neighbor(0, 2)]);

    // Assert
    expect(boundary.segments).toHaveLength(5);
    expect(boundary.gates).toEqual([]);
  });
});
