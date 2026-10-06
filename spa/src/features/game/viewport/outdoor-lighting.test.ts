import { CSMShadowNode } from 'three/addons/csm/CSMShadowNode.js';
import { expect, it } from 'vitest';

import { blendedSplitBreaks, createOutdoorSun } from './outdoor-lighting';

it('uses a cascaded shadow node with the outdoor sun', () => {
  const { light, shadowNode } = createOutdoorSun(150);

  expect(light.castShadow).toBe(true);
  expect(light.shadow.shadowNode).toBe(shadowNode);
  expect(shadowNode).toBeInstanceOf(CSMShadowNode);
  expect(shadowNode.cascades).toBe(3);
  expect(shadowNode.fade).toBe(true);
});

it('splits evenly when the blend is zero', () => {
  const breaks: number[] = [];

  blendedSplitBreaks(4, 0.1, 100, 0, breaks);

  expect(breaks[0]).toBeCloseTo((0.1 + 99.9 / 4) / 100);
  expect(breaks[1]).toBeCloseTo((0.1 + 99.9 / 2) / 100);
  expect(breaks[3]).toBe(1);
});

it('packs cascades toward the camera as the blend rises and always ends at the far limit', () => {
  const even: number[] = [];
  const nearHeavy: number[] = [];

  blendedSplitBreaks(3, 0.1, 100, 0, even);
  blendedSplitBreaks(3, 0.1, 100, 1, nearHeavy);

  expect(nearHeavy[0]).toBeLessThan(even[0]!);
  expect(nearHeavy[2]).toBe(1);
});
