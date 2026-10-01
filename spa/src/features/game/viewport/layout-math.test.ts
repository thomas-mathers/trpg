import { describe, expect, it } from 'vitest';

import type {
  BuildingLayoutWire,
  PropLayoutWire,
  SceneSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import {
  buildEntityNames,
  buildObstacles,
  buildWalls,
  clampToBounds,
  computeMovement,
  findConnectorInRange,
  findPlayerPlacement,
  headingToYaw,
  pushOutOfObstacles,
  toScenePosition,
  yawToHeading,
} from './layout-math';

const move = (heading: number, forward: number, strafe: number) =>
  computeMovement({ heading, forward, strafe, speed: 2, deltaSeconds: 1 });

describe('toScenePosition', () => {
  it('maps south to the positive z axis and lifts by height', () => {
    // Arrange
    const [x, y] = [4, 9];

    // Act
    const position = toScenePosition(x, y, 1.5);

    // Assert
    expect(position).toEqual([4, 1.5, 9]);
  });
});

describe('headingToYaw', () => {
  it('round trips through yawToHeading', () => {
    // Arrange
    const heading = 1.25;

    // Act
    const roundTripped = yawToHeading(headingToYaw(heading));

    // Assert
    expect(roundTripped).toBeCloseTo(heading);
  });
});

describe('computeMovement', () => {
  it('walks north when facing north', () => {
    // Act
    const delta = move(0, 1, 0);

    // Assert
    expect(delta.x).toBeCloseTo(0);
    expect(delta.y).toBeCloseTo(-2);
  });

  it('walks east when facing east', () => {
    // Act
    const delta = move(Math.PI / 2, 1, 0);

    // Assert
    expect(delta.x).toBeCloseTo(2);
    expect(delta.y).toBeCloseTo(0);
  });

  it('strafes to the right of the heading', () => {
    // Act
    const delta = move(0, 0, 1);

    // Assert
    expect(delta.x).toBeCloseTo(2);
    expect(delta.y).toBeCloseTo(0);
  });

  it('walks backwards opposite the heading', () => {
    // Act
    const delta = move(0, -1, 0);

    // Assert
    expect(delta.y).toBeCloseTo(2);
  });

  it('scales with elapsed time', () => {
    // Act
    const delta = computeMovement({
      heading: 0,
      forward: 1,
      strafe: 0,
      speed: 3,
      deltaSeconds: 0.5,
    });

    // Assert
    expect(delta.y).toBeCloseTo(-1.5);
  });
});

describe('clampToBounds', () => {
  const size = { width: 10, depth: 8 };

  it('keeps a point inside the location unchanged', () => {
    // Act
    const clamped = clampToBounds({ x: 5, y: 4 }, size);

    // Assert
    expect(clamped).toEqual({ x: 5, y: 4 });
  });

  it('pulls a point past the far edges back inside', () => {
    // Act
    const clamped = clampToBounds({ x: 50, y: -5 }, size);

    // Assert
    expect(clamped.x).toBeLessThan(10);
    expect(clamped.y).toBeGreaterThan(0);
  });

  it('centers on a location narrower than the margin', () => {
    // Act
    const clamped = clampToBounds({ x: 3, y: 3 }, { width: 0.2, depth: 0.2 });

    // Assert
    expect(clamped).toEqual({ x: 0.1, y: 0.1 });
  });
});

describe('pushOutOfObstacles', () => {
  const crate = {
    placement: { x: 10, y: 10, angle: 0 },
    footprint: { width: 4, depth: 2 },
  };

  it('leaves a point clear of every obstacle unchanged', () => {
    // Act
    const resolved = pushOutOfObstacles({ x: 5, y: 5 }, [crate], 0.5);

    // Assert
    expect(resolved).toEqual({ x: 5, y: 5 });
  });

  it('keeps a point that walked into a face one radius away from it', () => {
    // Act
    const resolved = pushOutOfObstacles({ x: 10, y: 8.8 }, [crate], 0.5);

    // Assert
    expect(resolved.x).toBeCloseTo(10);
    expect(resolved.y).toBeCloseTo(8.5);
  });

  it('rounds a corner by keeping the radius from the corner point', () => {
    // Act
    const resolved = pushOutOfObstacles({ x: 12.1, y: 8.9 }, [crate], 0.5);

    // Assert
    expect(Math.hypot(resolved.x - 12, resolved.y - 9)).toBeCloseTo(0.5);
  });

  it('ejects a point inside the obstacle through the nearest face', () => {
    // Act
    const resolved = pushOutOfObstacles({ x: 10, y: 9.5 }, [crate], 0.5);

    // Assert
    expect(resolved).toEqual({ x: 10, y: 8.5 });
  });

  it('respects the obstacle rotation', () => {
    // Arrange
    const rotated = { ...crate, placement: { x: 10, y: 10, angle: Math.PI / 2 } };

    // Act
    const resolved = pushOutOfObstacles({ x: 10, y: 11.9 }, [rotated], 0.5);

    // Assert
    expect(resolved.x).toBeCloseTo(10);
    expect(resolved.y).toBeCloseTo(12.5);
  });
});

describe('buildObstacles', () => {
  const placement = { x: 1, y: 1, angle: 0 };
  const footprint = { width: 1, depth: 1 };

  it('treats tall props and buildings as solid but lets the player cross flat props', () => {
    // Arrange
    const props = [
      { id: 'chair', model: 'SeatChair', placement, footprint },
      { id: 'trap', model: 'TrapMechanical', placement, footprint },
    ] as PropLayoutWire[];
    const buildings = [{ id: 'inn', type: 'Inn', placement, footprint }] as BuildingLayoutWire[];

    // Act
    const obstacles = buildObstacles(props, buildings);

    // Assert
    expect(obstacles).toEqual([props[0], buildings[0]]);
  });
});

describe('buildWalls', () => {
  const size = { width: 10, depth: 8 };
  const door = (exitX: number, exitY: number) => ({
    connectorId: 'door',
    destinationLocationId: 'a',
    exitX,
    exitY,
  });
  const northWalls = (walls: ReturnType<typeof buildWalls>) =>
    walls
      .filter(({ placement }) => placement.y === 0)
      .sort((a, b) => a.placement.x - b.placement.x);

  it('closes the room with one wall per side when there are no connectors', () => {
    // Act
    const walls = buildWalls(size, []);

    // Assert
    expect(walls).toHaveLength(4);
  });

  it('spans each wall across the full side', () => {
    // Act
    const walls = buildWalls(size, []);

    // Assert
    const [north] = northWalls(walls);
    expect(north.placement.x).toBe(5);
    expect(north.footprint.width).toBe(10);
  });

  it('leaves a doorway gap centred on a connector exit', () => {
    // Act
    const walls = buildWalls(size, [door(4, 0)]);

    // Assert
    const [left, right] = northWalls(walls);
    expect(left.placement.x + left.footprint.width / 2).toBeCloseTo(3.25);
    expect(right.placement.x - right.footprint.width / 2).toBeCloseTo(4.75);
  });

  it('opens the wall the connector sits on and leaves the other walls whole', () => {
    // Act
    const walls = buildWalls(size, [door(10, 4)]);

    // Assert
    const east = walls.filter(({ placement }) => placement.x === 10);
    expect(east).toHaveLength(2);
    expect(walls).toHaveLength(5);
  });

  it('drops a wall segment too short to be visible', () => {
    // Act
    const walls = buildWalls(size, [door(0.1, 0)]);

    // Assert
    expect(northWalls(walls)).toHaveLength(1);
  });
});

describe('findConnectorInRange', () => {
  const near = { connectorId: 'near', destinationLocationId: 'a', exitX: 4, exitY: 0 };
  const far = { connectorId: 'far', destinationLocationId: 'b', exitX: 40, exitY: 0 };

  it('returns nothing when every connector is out of range', () => {
    // Act
    const found = findConnectorInRange({ x: 0, y: 0 }, [far], 3);

    // Assert
    expect(found).toBeUndefined();
  });

  it('returns the closest connector within range', () => {
    // Arrange
    const closer = { ...near, connectorId: 'closer', exitX: 2 };

    // Act
    const found = findConnectorInRange({ x: 0, y: 0 }, [near, closer, far], 5);

    // Assert
    expect(found?.connectorId).toBe('closer');
  });
});

describe('buildEntityNames', () => {
  const scene = {
    playerStatus: { id: 'player' },
    nearbyCreatures: [{ id: 'c1', name: 'Mira' }],
    nearbyBuildings: [{ id: 'b1', name: 'The Cold Rest' }],
    nearbyProps: [{ id: 'p1', name: 'Well' }],
    exits: [{ connectorId: 'x1', destination: { name: 'Old Road' } }],
    layout: {
      creatures: [{ id: 'player', placement: { x: 1, y: 2, angle: 0.5 } }],
      buildings: [],
      connectors: [],
    },
  } as unknown as SceneSnapshot;

  it('names creatures, buildings, props and exits by id', () => {
    // Act
    const names = buildEntityNames(scene);

    // Assert
    expect(Object.fromEntries(names)).toEqual({
      c1: 'Mira',
      b1: 'The Cold Rest',
      p1: 'Well',
      x1: 'Old Road',
    });
  });

  it('names a building door after the building it sits on', () => {
    // Arrange
    const outdoors = {
      ...scene,
      nearbyBuildings: [
        { id: 'b1', name: 'The Cold Rest' },
        { id: 'b2', name: 'The Far Forge' },
      ],
      exits: [],
      layout: {
        creatures: [],
        buildings: [
          { id: 'b1', placement: { x: 10, y: 10, angle: 0 }, footprint: { width: 6, depth: 6 } },
          { id: 'b2', placement: { x: 30, y: 10, angle: 0 }, footprint: { width: 6, depth: 6 } },
        ],
        connectors: [{ connectorId: 'door', exitX: 10, exitY: 13.2 }],
      },
    } as unknown as SceneSnapshot;

    // Act
    const names = buildEntityNames(outdoors);

    // Assert
    expect(names.get('door')).toBe('The Cold Rest');
  });

  it('finds the player placement in the layout', () => {
    // Act
    const placement = findPlayerPlacement(scene);

    // Assert
    expect(placement).toEqual({ x: 1, y: 2, angle: 0.5 });
  });
});
