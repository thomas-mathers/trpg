import { describe, expect, it } from 'vitest';

import { shadeColor, stringHash, varyColor } from './color-variation';

describe('varyColor', () => {
  it('returns the same color for the same id', () => {
    // Act
    const first = varyColor('#8a7b66', 'building-1', 0.05);
    const second = varyColor('#8a7b66', 'building-1', 0.05);

    // Assert
    expect(first).toBe(second);
  });

  it('returns different colors for different ids', () => {
    // Act
    const colors = new Set(['a', 'b', 'c', 'd', 'e', 'f'].map((id) => varyColor('#8a7b66', id, 1)));

    // Assert
    expect(colors.size).toBeGreaterThan(1);
  });

  it('returns a hex color', () => {
    // Act
    const color = varyColor('#8a7b66', 'x', 0.05);

    // Assert
    expect(color).toMatch(/^#[0-9a-f]{6}$/);
  });
});

describe('shadeColor', () => {
  it('lightens with a positive offset', () => {
    // Act
    const lighter = shadeColor('#506b42', 0.1);

    // Assert
    expect(parseInt(lighter.slice(1, 3), 16)).toBeGreaterThan(0x50);
  });
});

describe('stringHash', () => {
  it('is stable and non-negative', () => {
    // Act
    const hash = stringHash('some-id');

    // Assert
    expect(hash).toBe(stringHash('some-id'));
    expect(hash).toBeGreaterThanOrEqual(0);
  });
});
