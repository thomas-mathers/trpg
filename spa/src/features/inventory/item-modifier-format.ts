import type { ItemModifierSummary } from '@/api/client';
import { formatAmount } from '@/lib/formatting';

import {
  ATTRIBUTE_LABEL,
  COMBAT_SPEED_LABEL,
  SPECIAL_HIT_LABEL,
  PROC_TRIGGER_LABEL,
} from './display-names';

const RESISTANCE_ATTRIBUTES = new Set([
  'PhysicalResistance',
  'FireResistance',
  'IceResistance',
  'LightningResistance',
  'PoisonResistance',
  'MagicResistance',
]);

export function formatItemModifier(modifier: ItemModifierSummary): string {
  switch (modifier.$type) {
    case 'Attribute': {
      if (modifier.amountType === 'Flat' && RESISTANCE_ATTRIBUTES.has(modifier.attribute)) {
        return `+${Number(modifier.amount) * 100}% ${ATTRIBUTE_LABEL[modifier.attribute]}`;
      }
      return `${formatAmount(Number(modifier.amount), modifier.amountType)} ${ATTRIBUTE_LABEL[modifier.attribute]}`;
    }
    case 'CombatSpeed':
      return `+${modifier.amount}% ${COMBAT_SPEED_LABEL[modifier.speedType]}`;
    case 'ElementalDamage':
      return `Adds ${modifier.minDamage}-${modifier.maxDamage} ${modifier.damageType} Damage`;
    case 'Leech':
      return `${modifier.percent}% ${modifier.leechType} Leech`;
    case 'SpecialHit':
      return `${modifier.chance}% Chance of ${SPECIAL_HIT_LABEL[modifier.hitType]}`;
    case 'SkillBonus':
      return `+${modifier.amount} to ${modifier.skill ?? 'All'} Skills`;
    case 'Proc':
      return `${modifier.chance}% Chance to Cast ${modifier.abilityName} ${PROC_TRIGGER_LABEL[modifier.trigger]}`;
  }
}
