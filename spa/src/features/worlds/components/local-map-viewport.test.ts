import { describe, expect, it } from 'vitest';

import { initialMapViewport } from './local-map-viewport';

describe('initial local map viewport', () => {
  const canvas = { width: 1000, height: 600 };
  const current = { x: 1800, y: 900, width: 200, height: 120, isCurrent: true, hasCorpse: false };
  const far = { ...current, x: -1800, y: -900, isCurrent: false };

  it('keeps text readable and centers the player on large floors', () => {
    const viewport = initialMapViewport([current, far], canvas);
    expect(viewport.zoom).toBe(0.9);
    expect(viewport.x + 1900 * viewport.zoom).toBe(500);
    expect(viewport.y + 960 * viewport.zoom).toBe(300);
  });

  it('centers the corpse when viewing a different floor', () => {
    const viewport = initialMapViewport(
      [{ ...current, isCurrent: false, hasCorpse: true }, far],
      canvas,
    );
    expect(viewport.x + 1900 * viewport.zoom).toBe(500);
  });

  it('fits a small floor without excessive magnification', () => {
    expect(initialMapViewport([current], canvas).zoom).toBe(1.1);
  });
});
