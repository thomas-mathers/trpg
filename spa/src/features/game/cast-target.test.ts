import { describe, expect, it } from 'vitest';

import { resolveCastTarget } from './cast-target';

describe('resolveCastTarget', () => {
  it('casts an ability that needs no target on the player', () => {
    expect(resolveCastTarget({ requiresTarget: false, category: 'Offensive' }, 'foe', 'me')).toBe(
      'me',
    );
  });

  it('casts a targeted ability on the current target', () => {
    expect(resolveCastTarget({ requiresTarget: true, category: 'Offensive' }, 'foe', 'me')).toBe(
      'foe',
    );
  });

  it('falls back to the player for a support ability without a target', () => {
    expect(resolveCastTarget({ requiresTarget: true, category: 'Support' }, undefined, 'me')).toBe(
      'me',
    );
  });

  it('has no target for a non-support ability without a target', () => {
    expect(
      resolveCastTarget({ requiresTarget: true, category: 'Offensive' }, undefined, 'me'),
    ).toBeUndefined();
  });
});
