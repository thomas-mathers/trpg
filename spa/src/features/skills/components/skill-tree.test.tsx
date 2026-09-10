import { screen } from '@testing-library/react';
import { ReactFlowProvider } from '@xyflow/react';
import { describe, expect, it } from 'vitest';

import type { AbilitySummary } from '@/api/client';
import { renderWithProviders } from '@/test/test-utils';

import { SkillTree } from './skill-tree';

const abilities: AbilitySummary[] = [
  {
    name: 'Slash',
    skill: 'Melee',
    description: 'A committed melee attack.',
    apCost: 2,
    mpCost: 0,
    cooldown: 0,
    category: 'Offensive',
    requiredSkillLevel: 1,
    prerequisites: [],
  },
  {
    name: 'Power Strike',
    skill: 'Melee',
    description: 'A heavy melee attack.',
    apCost: 4,
    mpCost: 0,
    cooldown: 1,
    category: 'Offensive',
    requiredSkillLevel: 2,
    prerequisites: ['Slash'],
  },
];

describe('SkillTree', () => {
  it('uses the creature ability result as the unlock source of truth', async () => {
    renderWithProviders(
      <ReactFlowProvider>
        <SkillTree
          abilities={abilities}
          unlockedAbilityNames={new Set(['Slash', 'Power Strike'])}
        />
      </ReactFlowProvider>,
    );

    const ability = await screen.findByText('Power Strike');
    expect(ability.closest('[data-unlocked]')).toHaveAttribute('data-unlocked', 'true');
  });
});
