import { describe, expect, it } from 'vitest';

import { creatureAppearance } from './creature-appearance';

describe('creatureAppearance', () => {
  it('shrinks children relative to adults of the same species', () => {
    const child = creatureAppearance('a', 'Human', 8);
    const adult = creatureAppearance('a', 'Human', 30);

    expect(child.height).toBeLessThan(adult.height);
  });

  it('marks creatures past their species elder age as elderly', () => {
    expect(creatureAppearance('a', 'Human', 60).elderly).toBe(true);
    expect(creatureAppearance('a', 'Human', 59).elderly).toBe(false);
  });

  it('keeps the same garment color for the same id', () => {
    expect(creatureAppearance('npc-1', 'Elf', 40).garment).toBe(
      creatureAppearance('npc-1', 'Elf', 41).garment,
    );
  });

  it('gives dwarves a beard and demons horns', () => {
    expect(creatureAppearance('a', 'Dwarf', 30).feature).toBe('beard');
    expect(creatureAppearance('a', 'Demon', 30).feature).toBe('horns');
  });
});
