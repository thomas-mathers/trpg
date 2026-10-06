import { describe, expect, it } from 'vitest';

import { resolveGear } from './creature-gear';

const gear = (slot: string, modelClass: string) => ({ itemId: slot, slot, modelClass });

describe('resolveGear', () => {
  it('colors armor pieces by armor class', () => {
    const { armor } = resolveGear([gear('Chest', 'PlateChest'), gear('Helm', 'LeatherHelm')]);

    expect(armor.Chest).not.toBe(armor.Helm);
    expect(Object.keys(armor).sort()).toEqual(['Chest', 'Helm']);
  });

  it('places weapons and shields in the hand of their slot', () => {
    const { weapons, shields } = resolveGear([
      gear('RightHand', 'Sword'),
      gear('LeftHand', 'Shield'),
    ]);

    expect(weapons.map(({ hand }) => hand)).toEqual(['right']);
    expect(shields).toEqual(['left']);
  });

  it('ignores unknown model classes and non-hand weapons', () => {
    const resolved = resolveGear([gear('Necklace', 'Amulet'), gear('Belt', 'Sword')]);

    expect(resolved).toEqual({ armor: {}, weapons: [], shields: [] });
  });
});
