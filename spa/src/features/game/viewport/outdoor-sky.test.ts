import { describe, expect, it } from 'vitest';

import { createOutdoorSky } from './outdoor-sky';

describe('createOutdoorSky', () => {
  it('fades the graded day sky to the night colour and starts with neutral weather grading', () => {
    // Arrange
    const { mesh, daylight, saturation, brightness } = createOutdoorSky();

    // Act
    const colorNode = mesh.material.colorNode as unknown as {
      node: { isMathNode: boolean; method: string };
    };

    // Assert
    expect(colorNode.node.isMathNode).toBe(true);
    expect(colorNode.node.method).toBe('mix');
    expect([daylight.value, saturation.value, brightness.value]).toEqual([1, 1, 1]);

    mesh.geometry.dispose();
    mesh.material.dispose();
  });
});
