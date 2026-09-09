import type { IconType } from 'react-icons';
import {
  GiBlackBook,
  GiBrokenWall,
  GiCrossedSwords,
  GiDoorway,
  GiPrisoner,
  GiStoneStack,
  GiTreasureMap,
  GiTwoCoins,
  GiWaterSplash,
  GiWindHole,
} from 'react-icons/gi';

import type { RoomRole } from '@/api/client';

// A dungeon is a list of names without these. An icon per role is the fastest way to tell a
// storeroom from a shrine while scanning the exits.
export const ROOM_ROLE_ICONS: Record<RoomRole, IconType> = {
  Entrance: GiDoorway,
  BossChamber: GiCrossedSwords,
  Passage: GiWindHole,
  GuardPost: GiStoneStack,
  Storeroom: GiTwoCoins,
  TreasureRoom: GiTreasureMap,
  Shrine: GiWaterSplash,
  Study: GiBlackBook,
  CellBlock: GiPrisoner,
  CollapsedGallery: GiBrokenWall,
  FloodedSump: GiWaterSplash,
};
