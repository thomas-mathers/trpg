import { fireEvent, screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { byRole, byText } from 'testing-library-selector';
import { describe, expect, it, vi } from 'vitest';

import type { AllocateAttributePointsRequest } from '@/api/client';
import {
  handleAllocateCreatureAttributePoints,
  handleGetCreatureAttributePoints,
  handleGetCreatureBaseAttributes,
} from '@/api/client/msw.gen';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { CharacterDialog } from './character-dialog';

const baseAttributes = {
  strength: 10,
  defense: 5,
  dexterity: 10,
  endurance: 10,
  stamina: 10,
  mana: 10,
  intelligence: 10,
};

const ui = {
  dialog: byRole('dialog', { name: 'Character' }),
  attribute: (name: string) => byRole('spinbutton', { name }),
  increase: (name: string) => byRole('button', { name: `Increase ${name}` }),
  confirm: byRole('button', { name: 'Confirm' }),
  cancel: byRole('button', { name: 'Cancel' }),
  points: (text: string) => byText(text),
};

function mockCharacterQueries(unallocatedPoints = 3) {
  server.use(
    handleGetCreatureBaseAttributes({ body: baseAttributes }),
    handleGetCreatureAttributePoints({ body: { unallocatedPoints } }),
  );
}

describe('CharacterDialog', () => {
  it('renders the character attributes and available points', async () => {
    mockCharacterQueries();

    renderWithProviders(<CharacterDialog open onClose={vi.fn()} />);

    expect(await ui.dialog.find()).toBeVisible();
    expect(ui.points('3 of 3 points remaining').get()).toBeVisible();
    expect(ui.attribute('Strength').get()).toHaveValue(10);
    expect(ui.attribute('Intelligence').get()).toHaveValue(10);
  });

  it('updates the remaining points and clamps allocation to the available points', async () => {
    mockCharacterQueries();

    renderWithProviders(<CharacterDialog open onClose={vi.fn()} />);
    const strength = await ui.attribute('Strength').find();
    await ui.points('3 of 3 points remaining').find();

    fireEvent.change(strength, { target: { value: '13' } });

    expect(strength).toHaveValue(13);
    expect(ui.points('0 of 3 points remaining').get()).toBeVisible();
    expect(ui.increase('Strength').get()).toBeDisabled();
  });

  it('closes without submitting when cancelled', async () => {
    mockCharacterQueries();
    const onClose = vi.fn();

    const { user } = renderWithProviders(<CharacterDialog open onClose={onClose} />);
    await ui.dialog.find();

    await user.click(ui.cancel.get());

    expect(onClose).toHaveBeenCalledOnce();
  });

  it('allows confirming without allocating any points', async () => {
    mockCharacterQueries(0);
    const onClose = vi.fn();
    let requestBody: AllocateAttributePointsRequest | undefined;

    server.use(
      handleAllocateCreatureAttributePoints(async ({ request }) => {
        requestBody = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const { user } = renderWithProviders(<CharacterDialog open onClose={onClose} />);
    await ui.dialog.find();
    await ui.points('0 of 0 points remaining').find();

    await user.click(ui.confirm.get());

    await waitFor(() =>
      expect(requestBody).toEqual({
        deltas: {
          strength: 0,
          dexterity: 0,
          endurance: 0,
          stamina: 0,
          mana: 0,
          intelligence: 0,
        },
      }),
    );
    await waitFor(() => expect(onClose).toHaveBeenCalledOnce());
  });

  it('submits the selected attribute deltas', async () => {
    mockCharacterQueries();
    const onClose = vi.fn();
    let requestBody: AllocateAttributePointsRequest | undefined;

    server.use(
      handleAllocateCreatureAttributePoints(async ({ request }) => {
        requestBody = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const { user } = renderWithProviders(<CharacterDialog open onClose={onClose} />);
    const strength = await ui.attribute('Strength').find();
    await ui.points('3 of 3 points remaining').find();
    fireEvent.change(strength, { target: { value: '12' } });
    await user.click(ui.confirm.get());

    await waitFor(() =>
      expect(requestBody).toEqual({
        deltas: {
          strength: 2,
          dexterity: 0,
          endurance: 0,
          stamina: 0,
          mana: 0,
          intelligence: 0,
        },
      }),
    );
    await waitFor(() => expect(onClose).toHaveBeenCalledOnce());
  });

  it('shows an error and remains open when allocation fails', async () => {
    mockCharacterQueries();
    const onClose = vi.fn();

    server.use(
      handleAllocateCreatureAttributePoints(() => new HttpResponse(null, { status: 500 })),
    );

    const { user } = renderWithProviders(<CharacterDialog open onClose={onClose} />);
    await ui.dialog.find();

    await user.click(ui.confirm.get());

    expect(await screen.findByText('Failed to allocate attribute points.')).toBeVisible();
    expect(onClose).not.toHaveBeenCalled();
  });
});
