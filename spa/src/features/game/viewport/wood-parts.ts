import type { PropModel } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { DARK_WOOD, type FurniturePart } from './furniture-parts';

const WOODEN_MODELS: ReadonlySet<PropModel> = new Set<PropModel>([
  'Sign',
  'ContainerBasic',
  'ContainerBarrel',
  'ContainerChest',
  'ContainerCrate',
  'ContainerFootlocker',
  'ContainerWeaponRack',
  'FurnitureTable',
  'FurnitureWorkTable',
  'FurnitureStall',
  'FurnitureNoticeBoard',
  'FurnitureDisplayShelf',
  'FurnitureHerbRack',
  'FurnitureLectern',
  'FurnitureStaffRack',
  'FurnitureClothShelf',
  'FurnitureDisplayCase',
  'FurnitureBreadRack',
  'FurnitureLumberStack',
  'FurnitureTimberRack',
  'FurnitureBookcase',
  'FurnitureChair',
  'FurnitureBench',
  'FurniturePew',
  'FurnitureHerbTub',
  'FurnitureHitchingRail',
  'FurnitureCart',
  'FurnitureTrough',
  'SeatBasic',
  'SeatChair',
  'SeatPew',
  'SeatBench',
  'WorkstationCarpentry',
  'WorkstationReading',
]);

function isGrainShape({ shape }: FurniturePart) {
  return shape === 'box' || shape === 'cylinder';
}

function isUnfinished({ finish, opacity }: FurniturePart) {
  return finish === undefined && opacity === undefined;
}

function isWoodPart(part: FurniturePart, model: PropModel, bodyColor: string) {
  if (!isGrainShape(part) || !isUnfinished(part)) return false;
  return part.color === DARK_WOOD || (WOODEN_MODELS.has(model) && part.color === bodyColor);
}

export function withWoodFinish(parts: FurniturePart[], model: PropModel, bodyColor: string) {
  return parts.map((part): FurniturePart => {
    if (!isWoodPart(part, model, bodyColor)) return part;
    return { ...part, finish: 'wood' };
  });
}
