import { describe, expect, it } from 'vitest';

import type { CountryMapResponse, StateMapResponse } from '@/api/client';

import { normalizeWorldMapGeometry } from './world-map-geometry';

describe('normalizeWorldMapGeometry', () => {
  it('expands a retuned world into the stable display coordinate space', () => {
    const countries: CountryMapResponse[] = [
      {
        id: 'country',
        name: 'Country',
        boundary: [
          { x: 0, y: 0 },
          { x: 400, y: 0 },
          { x: 400, y: 400 },
        ],
      },
    ];
    const states: StateMapResponse[] = [
      {
        id: 'state',
        countryId: 'country',
        name: 'State',
        description: 'Description',
        center: { x: 200, y: 100 },
        boundary: [
          { x: 0, y: 0 },
          { x: 400, y: 0 },
          { x: 400, y: 400 },
        ],
      },
    ];

    const result = normalizeWorldMapGeometry(countries, states);

    expect(result.countries[0].boundary).toEqual([
      { x: 0, y: 0 },
      { x: 10_000, y: 0 },
      { x: 10_000, y: 10_000 },
    ]);
    expect(result.states[0].center).toEqual({ x: 5_000, y: 2_500 });
  });

  it('preserves the proportions of an existing large world', () => {
    const states: StateMapResponse[] = [
      {
        id: 'state',
        countryId: 'country',
        name: 'State',
        description: 'Description',
        center: { x: 5_000, y: 2_500 },
        boundary: [
          { x: 0, y: 0 },
          { x: 10_000, y: 0 },
          { x: 10_000, y: 10_000 },
        ],
      },
    ];

    const result = normalizeWorldMapGeometry([], states);

    expect(result.states).toEqual(states);
  });
});
