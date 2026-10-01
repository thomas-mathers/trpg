import { describe, expect, it } from 'vitest';

import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import {
  buildEntityNames,
  clampToBounds,
  computeMovement,
  findPlayerPlacement,
  headingToYaw,
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

describe('buildEntityNames', () => {
  const scene = {
    playerStatus: { id: 'player' },
    nearbyCreatures: [{ id: 'c1', name: 'Mira' }],
    nearbyBuildings: [{ id: 'b1', name: 'The Cold Rest' }],
    nearbyProps: [{ id: 'p1', name: 'Well' }],
    exits: [{ connectorId: 'x1', destination: { name: 'Old Road' } }],
    layout: {
      creatures: [{ id: 'player', placement: { x: 1, y: 2, angle: 0.5 } }],
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

  it('finds the player placement in the layout', () => {
    // Act
    const placement = findPlayerPlacement(scene);

    // Assert
    expect(placement).toEqual({ x: 1, y: 2, angle: 0.5 });
  });
});
