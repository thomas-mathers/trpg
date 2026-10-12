import { describe, expect, it } from 'vitest';

import { buildingWallColor, roofShape } from './building-roof';

describe('roofShape', () => {
  it('has no roof for fortified or underground buildings', () => {
    // Act
    const shapes = (['Castle', 'Tower', 'Cave', 'Mine', 'Crypt', 'Ruins'] as const).map((type) =>
      roofShape('id', type, { width: 8, depth: 6 }),
    );

    // Assert
    expect(shapes.every((shape) => shape === undefined)).toBe(true);
  });

  it('runs the ridge along the longer side', () => {
    // Act
    const deep = roofShape('id', 'House', { width: 6, depth: 10 });
    const wide = roofShape('id', 'House', { width: 10, depth: 6 });

    // Assert
    expect(deep?.yaw).toBe(0);
    expect(wide?.yaw).toBeCloseTo(Math.PI / 2);
  });

  it('overhangs the walls', () => {
    // Act
    const roof = roofShape('id', 'House', { width: 6, depth: 10 });

    // Assert
    expect(roof?.span).toBeGreaterThan(6);
    expect(roof?.length).toBeGreaterThan(10);
  });

  it('pitches the roof steeply', () => {
    // Act
    const roof = roofShape('id', 'House', { width: 6, depth: 10 });

    // Assert
    expect(roof!.rise).toBeGreaterThanOrEqual(roof!.span * 0.4);
  });

  it('places a chimney on hearth buildings inside the roof footprint', () => {
    // Act
    const roof = roofShape('inn-1', 'Inn', { width: 6, depth: 10 });

    // Assert
    expect(Math.abs(roof!.chimney!.x)).toBeLessThan(roof!.span / 2);
    expect(Math.abs(roof!.chimney!.z)).toBeLessThan(roof!.length / 2);
  });

  it('gives storage buildings no chimney', () => {
    // Act
    const roof = roofShape('stable-1', 'Stable', { width: 6, depth: 10 });

    // Assert
    expect(roof!.chimney).toBeUndefined();
  });

  it('keeps the chimney in the same place for the same building', () => {
    // Act
    const first = roofShape('inn-1', 'Inn', { width: 6, depth: 10 });
    const second = roofShape('inn-1', 'Inn', { width: 6, depth: 10 });

    // Assert
    expect(first!.chimney).toEqual(second!.chimney);
  });

  it('keeps the roof color stable for the same building', () => {
    // Act
    const first = roofShape('house-1', 'House', { width: 6, depth: 10 });
    const second = roofShape('house-1', 'House', { width: 6, depth: 10 });

    // Assert
    expect(first?.color).toBe(second?.color);
  });
});

describe('buildingWallColor', () => {
  it('differs between neighboring buildings of the same type', () => {
    // Act
    const colors = new Set(
      ['b1', 'b2', 'b3', 'b4', 'b5'].map((id) => buildingWallColor('#b4a58c', id)),
    );

    // Assert
    expect(colors.size).toBeGreaterThan(1);
  });
});
