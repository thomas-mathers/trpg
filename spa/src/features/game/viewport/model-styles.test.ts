import { describe, expect, it } from 'vitest';

import { BUILDING_STYLES, buildingStyle } from './model-styles';

describe('buildingStyle', () => {
  it('keeps the base height for a single-floor building', () => {
    expect(buildingStyle('Cave', 1).height).toBe(BUILDING_STYLES.Cave.height);
  });

  it('grows with the floor count', () => {
    expect(buildingStyle('Inn', 3).height).toBeGreaterThan(BUILDING_STYLES.Inn.height);
  });

  it('never shrinks a tall building with few floors', () => {
    expect(buildingStyle('Castle', 2).height).toBe(BUILDING_STYLES.Castle.height);
  });
});
