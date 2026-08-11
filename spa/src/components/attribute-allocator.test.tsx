import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '@/test/test-utils';

import { MINIMUM_ATTRIBUTE_VALUES, toAttributeValues } from './attribute-allocation';
import { AttributeAllocator } from './attribute-allocator';

const baseValues = toAttributeValues({
  strength: 10,
  defense: 5,
  dexterity: 10,
  endurance: 10,
  stamina: 10,
  mana: 10,
  intelligence: 10,
});

describe('AttributeAllocator', () => {
  it('disables decreases when an attribute is at its minimum', () => {
    // Arrange
    renderWithProviders(
      <AttributeAllocator
        baseValues={baseValues}
        deltas={{}}
        minimumValues={baseValues}
        availablePoints={3}
        remainingPoints={3}
        onDeltaChange={vi.fn()}
      />,
    );

    // Act
    const decreaseStrength = screen.getByRole('button', { name: 'Decrease Strength' });

    // Assert
    expect(decreaseStrength).toBeDisabled();
  });

  it('allows new-world attributes to decrease below their generated base values', async () => {
    // Arrange
    const onDeltaChange = vi.fn();
    const { user } = renderWithProviders(
      <AttributeAllocator
        baseValues={baseValues}
        deltas={{}}
        minimumValues={MINIMUM_ATTRIBUTE_VALUES}
        availablePoints={3}
        remainingPoints={3}
        onDeltaChange={onDeltaChange}
      />,
    );

    // Act
    await user.click(screen.getByRole('button', { name: 'Decrease Strength' }));

    // Assert
    expect(onDeltaChange).toHaveBeenCalledWith('strength', -1);
  });
});
