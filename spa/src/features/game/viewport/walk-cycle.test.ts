import { describe, expect, it } from 'vitest';

import { walkCycle } from './walk-cycle';

describe('walkCycle', () => {
  it('holds the neutral pose when the creature is not walking', () => {
    // Act
    const { rootY, pitch } = walkCycle(1.3, 0);

    // Assert
    expect(rootY).toBeCloseTo(0);
    expect(Object.values(pitch).every((angle) => Math.abs(angle) < 1e-9)).toBe(true);
  });

  it('swings the legs in opposition', () => {
    // Act
    const { pitch } = walkCycle(Math.PI / 2, 1);

    // Assert
    expect(pitch.thighR!).toBeGreaterThan(0.3);
    expect(pitch.thighL!).toBeCloseTo(-pitch.thighR!);
  });

  it('swings each arm against the leg on the same side', () => {
    // Act
    const { pitch } = walkCycle(Math.PI / 2, 1);

    // Assert
    expect(Math.sign(pitch.upperArmR!)).toBe(-Math.sign(pitch.thighR!));
  });

  it('bends only the knee of the leg that is swinging forward', () => {
    // Act
    const { pitch } = walkCycle(0, 1);

    // Assert
    expect(pitch.shinR!).toBeLessThan(-0.5);
    expect(pitch.shinL!).toBeCloseTo(0);
  });

  it('sits lowest when both feet are on the ground', () => {
    // Act
    const doubleSupport = walkCycle(0, 1).rootY;
    const midStance = walkCycle(Math.PI / 2, 1).rootY;

    // Assert
    expect(doubleSupport).toBeLessThan(midStance);
  });
});
