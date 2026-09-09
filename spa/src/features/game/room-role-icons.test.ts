import { describe, expect, it } from 'vitest';

import type { RoomRole } from '@/api/client';

import { ROOM_ROLE_ICONS } from './room-role-icons';

const ALL_ROLES: RoomRole[] = [
  'Entrance',
  'BossChamber',
  'Passage',
  'GuardPost',
  'Storeroom',
  'TreasureRoom',
  'Shrine',
  'Study',
  'CellBlock',
  'CollapsedGallery',
  'FloodedSump',
];

describe('ROOM_ROLE_ICONS', () => {
  it('has an icon for every role, so no exit falls back to a generic glyph', () => {
    for (const role of ALL_ROLES) {
      expect(ROOM_ROLE_ICONS[role]).toBeDefined();
    }
  });

  it('draws the rooms worth telling apart differently', () => {
    const distinguishing: RoomRole[] = [
      'BossChamber',
      'TreasureRoom',
      'Study',
      'CellBlock',
      'Passage',
    ];
    const icons = new Set(distinguishing.map((role) => ROOM_ROLE_ICONS[role]));

    expect(icons.size).toBe(distinguishing.length);
  });
});
