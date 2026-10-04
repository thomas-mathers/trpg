import { describe, expect, it } from 'vitest';

import type {
  NearbyBuildingSnapshot,
  NearbyExitSnapshot,
  NearbyPropSnapshot,
  SceneSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import {
  buildEntityNames,
  buildObstacles,
  buildDoorHeaders,
  buildWalls,
  clampToBounds,
  computeMovement,
  findConnectorInRange,
  findPlayerPlacement,
  headingToYaw,
  isWalledScene,
  pushOutOfObstacles,
  toScenePosition,
  walkSpeedFor,
  WALK_SPEED,
  BASE_MOVEMENT_SPEED,
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
    ] as NearbyPropSnapshot[];
    const buildings = [
      { id: 'inn', type: 'Inn', placement, footprint },
    ] as NearbyBuildingSnapshot[];

    // Act
    const obstacles = buildObstacles(props, buildings);

    // Assert
    expect(obstacles).toEqual([props[0], buildings[0]]);
  });
});

describe('buildWalls', () => {
  const size = { width: 10, depth: 8 };
  const door = (x: number, y: number) =>
    ({
      connectorId: 'door',
      destinationLocationId: 'a',
      placement: { x, y, angle: 0 },
    }) as NearbyExitSnapshot;
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

describe('buildDoorHeaders', () => {
  const size = { width: 10, depth: 8 };
  const door = (x: number, y: number) =>
    ({
      connectorId: 'door',
      destinationLocationId: 'a',
      placement: { x, y, angle: 0 },
    }) as NearbyExitSnapshot;

  it('closes the wall above a doorway on the north side', () => {
    // Act
    const headers = buildDoorHeaders(size, [door(4, 0)]);

    // Assert
    expect(headers).toEqual([
      { placement: { x: 4, y: 0, angle: 0 }, footprint: { width: 1.5, depth: 0.2 } },
    ]);
  });

  it('closes the wall above a doorway on the east side', () => {
    // Act
    const headers = buildDoorHeaders(size, [door(10, 3)]);

    // Assert
    expect(headers).toEqual([
      { placement: { x: 10, y: 3, angle: 0 }, footprint: { width: 0.2, depth: 1.5 } },
    ]);
  });

  it('adds nothing for a connector that is not on a wall', () => {
    // Act
    const headers = buildDoorHeaders(size, [door(5, 4)]);

    // Assert
    expect(headers).toEqual([]);
  });
});

describe('findConnectorInRange', () => {
  const near = {
    connectorId: 'near',
    destinationLocationId: 'a',
    placement: { x: 4, y: 0, angle: 0 },
  } as NearbyExitSnapshot;
  const far = {
    connectorId: 'far',
    destinationLocationId: 'b',
    placement: { x: 40, y: 0, angle: 0 },
  } as NearbyExitSnapshot;

  it('returns nothing when every connector is out of range', () => {
    // Act
    const found = findConnectorInRange({ x: 0, y: 0 }, [far], 3);

    // Assert
    expect(found).toBeUndefined();
  });

  it('returns the closest connector within range', () => {
    // Arrange
    const closer = { ...near, connectorId: 'closer', placement: { ...near.placement, x: 2 } };

    // Act
    const found = findConnectorInRange({ x: 0, y: 0 }, [near, closer, far], 5);

    // Assert
    expect(found?.connectorId).toBe('closer');
  });
});

describe('buildEntityNames', () => {
  const scene = {
    playerStatus: { id: 'player', placement: { x: 1, y: 2, angle: 0.5 } },
    nearbyCreatures: [{ id: 'c1', name: 'Mira' }],
    nearbyBuildings: [{ id: 'b1', name: 'The Cold Rest' }],
    nearbyProps: [{ id: 'p1', name: 'Well' }],
    exits: [{ connectorId: 'x1', destination: { name: 'Old Road' } }],
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

  it('names a building entrance from its exit destination', () => {
    // Arrange
    const outdoors = {
      ...scene,
      nearbyBuildings: [
        { id: 'b1', name: 'The Cold Rest' },
        { id: 'b2', name: 'The Far Forge' },
      ],
      exits: [{ connectorId: 'door', destination: { name: 'The Cold Rest' } }],
    } as unknown as SceneSnapshot;

    // Act
    const names = buildEntityNames(outdoors);

    // Assert
    expect(names.get('door')).toBe('The Cold Rest');
  });

  it('finds the player placement on the player status', () => {
    // Act
    const placement = findPlayerPlacement(scene);

    // Assert
    expect(placement).toEqual({ x: 1, y: 2, angle: 0.5 });
  });
});

describe('walkSpeedFor', () => {
  it('walks at the base pace for the base movement speed', () => {
    // Act
    const speed = walkSpeedFor(BASE_MOVEMENT_SPEED);

    // Assert
    expect(speed).toBe(WALK_SPEED);
  });

  it('halves the pace when the movement speed is halved', () => {
    // Act
    const speed = walkSpeedFor(BASE_MOVEMENT_SPEED / 2);

    // Assert
    expect(speed).toBe(WALK_SPEED / 2);
  });
});

describe('isWalledScene', () => {
  const scene = (parts: Partial<SceneSnapshot>) => parts as SceneSnapshot;

  it('walls in a room', () => {
    // Act
    const walled = isWalledScene(scene({ districtName: 'Old Town', roomName: 'Lobby' }));

    // Assert
    expect(walled).toBe(true);
  });

  it('walls in a district so its edge exits stand in a gate', () => {
    // Act
    const walled = isWalledScene(scene({ districtName: 'Grand Bazaar' }));

    // Assert
    expect(walled).toBe(true);
  });

  it('leaves open ground without walls', () => {
    // Act
    const walled = isWalledScene(scene({ cityName: undefined, districtName: undefined }));

    // Assert
    expect(walled).toBe(false);
  });
});
