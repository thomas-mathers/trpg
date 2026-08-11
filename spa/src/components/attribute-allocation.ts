import type { BaseAttributesResponse } from '@/api/client';

export const ALLOCATABLE_ATTRIBUTES = [
  {
    name: 'Strength',
    allocationKey: 'strength',
    description: 'Increases physical attack damage.',
  },
  {
    name: 'Dexterity',
    allocationKey: 'dexterity',
    description: 'Increases evasion and critical hit chance.',
  },
  {
    name: 'Endurance',
    allocationKey: 'endurance',
    description: 'Increases maximum HP.',
  },
  {
    name: 'Stamina',
    allocationKey: 'stamina',
    description: 'Increases maximum AP, spent on physical abilities.',
  },
  {
    name: 'Mana',
    allocationKey: 'mana',
    description: 'Increases maximum MP, spent on magic abilities.',
  },
  {
    name: 'Intelligence',
    allocationKey: 'intelligence',
    description: 'Increases magic attack damage.',
  },
] as const;

type AllocatableAttribute = (typeof ALLOCATABLE_ATTRIBUTES)[number]['name'];
export type AllocatableAttributeKey = (typeof ALLOCATABLE_ATTRIBUTES)[number]['allocationKey'];
export type AttributeValues = Record<AllocatableAttribute, number>;

export const MINIMUM_ATTRIBUTE_VALUES: AttributeValues = {
  Strength: 1,
  Dexterity: 1,
  Endurance: 1,
  Stamina: 1,
  Mana: 1,
  Intelligence: 1,
};

export function toAttributeValues(attributes: BaseAttributesResponse | undefined): AttributeValues {
  return {
    Strength: Number(attributes?.strength ?? 0),
    Dexterity: Number(attributes?.dexterity ?? 0),
    Endurance: Number(attributes?.endurance ?? 0),
    Stamina: Number(attributes?.stamina ?? 0),
    Mana: Number(attributes?.mana ?? 0),
    Intelligence: Number(attributes?.intelligence ?? 0),
  };
}
