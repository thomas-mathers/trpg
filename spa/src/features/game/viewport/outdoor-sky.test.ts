import { describe, expect, it } from 'vitest';

import { createOutdoorSky } from './outdoor-sky';

describe('createOutdoorSky', () => {
  it('reduces sky radiance before tone mapping', () => {
    const sky = createOutdoorSky();
    const colorNode = sky.material.colorNode as unknown as {
      node: { isOperatorNode: boolean; op: string; bNode: { value: number } };
    };

    expect(colorNode.node.isOperatorNode).toBe(true);
    expect(colorNode.node.op).toBe('*');
    expect(colorNode.node.bNode.value).toBe(0.05);

    sky.geometry.dispose();
    sky.material.dispose();
  });
});
