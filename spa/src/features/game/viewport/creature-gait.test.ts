import { describe, expect, it } from 'vitest';

import { advanceGait } from './creature-gait';

describe('advanceGait', () => {
  it('advances the stride phase with distance travelled', () => {
    // Arrange
    const gait = { phase: 0, amount: 0 };

    // Act
    advanceGait(gait, 0.5, 0.1, 1);

    // Assert
    expect(gait.phase).toBeGreaterThan(0);
  });

  it('keeps the phase when the creature is standing still', () => {
    // Arrange
    const gait = { phase: 1, amount: 0 };

    // Act
    advanceGait(gait, 0, 0.1, 1);

    // Assert
    expect(gait.phase).toBe(1);
  });

  it('settles back to idle after the creature stops', () => {
    // Arrange
    const gait = { phase: 1, amount: 1 };

    // Act
    for (let frame = 0; frame < 60; frame++) advanceGait(gait, 0, 1 / 60, 1);

    // Assert
    expect(gait.amount).toBeLessThan(0.01);
  });

  it('ignores a teleport instead of spinning the legs', () => {
    // Arrange
    const gait = { phase: 0, amount: 0 };

    // Act
    advanceGait(gait, 40, 0.016, 1);

    // Assert
    expect(gait.phase).toBe(0);
    expect(gait.amount).toBe(0);
  });

  it('takes shorter strides for shorter creatures', () => {
    // Arrange
    const tall = { phase: 0, amount: 0 };
    const short = { phase: 0, amount: 0 };

    // Act
    advanceGait(tall, 0.1, 0.1, 1);
    advanceGait(short, 0.1, 0.1, 0.5);

    // Assert
    expect(short.phase).toBeGreaterThan(tall.phase);
  });
});
