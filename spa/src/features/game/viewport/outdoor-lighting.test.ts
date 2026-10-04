import { CSMShadowNode } from 'three/addons/csm/CSMShadowNode.js';
import { expect, it } from 'vitest';

import { createOutdoorSun } from './outdoor-lighting';

it('uses a cascaded shadow node with the outdoor sun', () => {
  const { light, shadowNode } = createOutdoorSun(150);

  expect(light.castShadow).toBe(true);
  expect(light.shadow.shadowNode).toBe(shadowNode);
  expect(shadowNode).toBeInstanceOf(CSMShadowNode);
  expect(shadowNode.cascades).toBe(3);
  expect(shadowNode.fade).toBe(true);
});
