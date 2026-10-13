import { Color } from 'three';
import { describe, expect, it } from 'vitest';

import type { BuildingType } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { buildingFacade, facadePalette } from './building-facade';

describe('buildingFacade', () => {
  it('builds domestic and trade buildings as half-timbered over a low stone plinth', () => {
    // Arrange
    const types: BuildingType[] = ['House', 'Inn', 'Tavern', 'Bakery', 'Carpenter', 'Library'];

    // Act
    const facades = types.map(buildingFacade);

    // Assert
    expect(facades.every(({ timbered }) => timbered)).toBe(true);
    expect(facades.every(({ plinth }) => plinth > 0 && plinth <= 1.2)).toBe(true);
  });

  it('keeps fortified and civic buildings fully stone', () => {
    // Arrange
    const types: BuildingType[] = ['Castle', 'Tower', 'Barracks', 'Jail', 'Temple', 'Crypt'];

    // Act
    const facades = types.map(buildingFacade);

    // Assert
    expect(facades.every(({ timbered }) => !timbered)).toBe(true);
  });

  it('gives hearth buildings a chimney and storage buildings none', () => {
    // Act
    const inn = buildingFacade('Inn');
    const stable = buildingFacade('Stable');

    // Assert
    expect(inn.chimney).toBe(true);
    expect(stable.chimney).toBe(false);
  });

  it('whitewashes timbered plaster while keeping the type hue', () => {
    // Act
    const { plaster } = facadePalette('Apothecary', '#5f8a6b', 'a1');

    // Assert
    expect(new Color(plaster).getHSL({ h: 0, s: 0, l: 0 }).l).toBeGreaterThan(
      new Color('#5f8a6b').getHSL({ h: 0, s: 0, l: 0 }).l,
    );
  });

  it('uses the type color as the stone of fully stone buildings', () => {
    // Act
    const palette = facadePalette('Castle', '#8a8a92', 'c1');

    // Assert
    expect(palette.stone).toBe('#8a8a92');
  });

  it('defines a facade for every building type', () => {
    // Arrange
    const types: BuildingType[] = [
      'ArcaneShop',
      'Apothecary',
      'Bakery',
      'Barracks',
      'Blacksmith',
      'Carpenter',
      'Castle',
      'Cave',
      'Crypt',
      'GeneralGoods',
      'GuildHall',
      'House',
      'Inn',
      'Jail',
      'Jeweler',
      'Library',
      'Mine',
      'Ruins',
      'Stable',
      'Tailor',
      'Tavern',
      'Temple',
      'Tower',
    ];

    // Act
    const facades = types.map(buildingFacade);

    // Assert
    expect(facades.every((facade) => facade !== undefined)).toBe(true);
  });
});
