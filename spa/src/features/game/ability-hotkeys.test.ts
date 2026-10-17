import { describe, expect, it } from 'vitest';

import { abilityIndexForKey, hotkeyLabel } from './ability-hotkeys';

describe('abilityIndexForKey', () => {
  it('maps the number row onto the first ten abilities', () => {
    expect(['Digit1', 'Digit9', 'Digit0'].map(abilityIndexForKey)).toEqual([0, 8, 9]);
  });

  it('ignores other keys', () => {
    expect(abilityIndexForKey('KeyW')).toBeUndefined();
  });
});

describe('hotkeyLabel', () => {
  it('labels slots with the key that casts them', () => {
    expect([hotkeyLabel(0), hotkeyLabel(9), hotkeyLabel(10)]).toEqual(['1', '0', undefined]);
  });
});
