import { describe, expect, it } from 'vitest';

import type { SceneSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { formatLocation } from './scene-format';

const scene = (overrides: Partial<SceneSnapshot>): SceneSnapshot =>
  ({
    stateName: 'Ravenhollow Territory',
    cityName: 'Ravencrest',
    ...overrides,
  }) as SceneSnapshot;

describe('formatLocation', () => {
  it('names the room first, so each one is distinguishable when scrolling back', () => {
    const label = formatLocation(
      scene({ roomName: 'Strongroom', buildingName: 'The Cold Rest', districtName: 'Hearthside' }),
    );

    expect(label).toBe('Strongroom, The Cold Rest');
  });

  it('distinguishes two rooms of the same building', () => {
    const inside = (roomName: string) =>
      formatLocation(scene({ roomName, buildingName: 'The Cold Rest' }));

    expect(inside('Flooded Sump')).not.toBe(inside('Watch Post'));
  });

  it('falls back to the district outdoors, where there is no room', () => {
    const label = formatLocation(scene({ districtName: 'The Merchant Quarter' }));

    expect(label).toBe('The Merchant Quarter, Ravencrest');
  });
});
