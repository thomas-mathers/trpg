import { describe, expect, it } from 'vitest';

import { glidePosition, glideTo, MAX_GLIDE_MS, MIN_GLIDE_MS, settledAt } from './glide';

describe('glide', () => {
  it('holds still when settled', () => {
    const glide = settledAt(3, 4, 0);

    expect(glidePosition(glide, 10_000)).toEqual({ x: 3, y: 4 });
  });

  it('moves linearly across the gap since the previous update', () => {
    const glide = glideTo(settledAt(0, 0, 0), 10, 0, 5000, 0);

    expect(glidePosition(glide, 7500)).toEqual({ x: 5, y: 0 });
  });

  it('stops at the target once the glide has run its course', () => {
    const glide = glideTo(settledAt(0, 0, 0), 10, 2, 5000, 0);

    expect(glidePosition(glide, 60_000)).toEqual({ x: 10, y: 2 });
  });

  it('restarts from where the figure currently is when retargeted mid-glide', () => {
    const first = glideTo(settledAt(0, 0, 0), 10, 0, 5000, 0);

    const second = glideTo(first, 10, 10, 7500, 5000);

    expect(glidePosition(second, 7500)).toEqual({ x: 5, y: 0 });
  });

  it('keeps very short update gaps from becoming a teleport', () => {
    const glide = glideTo(settledAt(0, 0, 0), 10, 0, 10, 0);

    expect(glidePosition(glide, 10 + MIN_GLIDE_MS / 2).x).toBe(5);
  });

  it('caps a long idle gap so the next glide does not crawl', () => {
    const glide = glideTo(settledAt(0, 0, 0), 10, 0, 100_000, 0);

    expect(glidePosition(glide, 100_000 + MAX_GLIDE_MS / 2).x).toBe(5);
  });
});
