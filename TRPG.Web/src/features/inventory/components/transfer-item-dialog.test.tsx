import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { byPlaceholderText, byRole, byTitle } from 'testing-library-selector';
import { describe, expect, it, vi } from 'vitest';

import type { InventoryTransferRequest, ItemDetail, ItemDetailGoldDetail } from '@/api/client';
import { handleGetCreatureInventory, handleTransferInventory } from '@/api/client/msw.gen';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { TransferItemDialog } from './transfer-item-dialog';

const ui = {
  dialog: byRole('dialog', { name: 'Transfer Items' }),
  confirm: byRole('button', { name: 'Confirm Transfer' }),
  cancel: byRole('button', { name: 'Cancel' }),
  clearFilters: byRole('button', { name: 'Clear filters' }),
  item: (name: string) => byRole('checkbox', { name: `Select ${name}` }),
  moveToPlayer: byTitle('Move selected items to your inventory'),
  moveToTarget: (name: string) => byTitle(`Move selected items to ${name}`),
  search: byPlaceholderText('Search...'),
  category: (name: string) => byRole('button', { name: `Your inventory ${name}` }),
  sort: (name: string) => byRole('button', { name }),
};

const item = (overrides: Partial<ItemDetailGoldDetail> = {}): ItemDetail => ({
  $type: 'Gold',
  itemId: 'item-1',
  name: 'Gold coins',
  description: 'A small pile of coins.',
  weight: 0,
  quantity: 10,
  equippedSlot: null,
  type: 'Gold',
  rarity: null,
  goldValue: null,
  modifiers: [],
  ...overrides,
});

const sword = (overrides: Partial<ItemDetail> = {}): ItemDetail =>
  ({
    $type: 'Weapon',
    itemId: 'sword-1',
    name: 'Iron Sword',
    description: 'A sturdy sword.',
    weight: 2,
    quantity: 1,
    equippedSlot: null,
    type: 'Sword',
    rarity: null,
    goldValue: 20,
    modifiers: [],
    minDamage: 3,
    maxDamage: 5,
    range: 1,
    attacksPerTurn: 1,
    isTwoHanded: false,
    ...overrides,
  }) as ItemDetail;

function renderDialog(onClose = vi.fn()) {
  return renderWithProviders(
    <TransferItemDialog
      playerId="player-id"
      target={{ id: 'target-id', name: 'Goblin' }}
      open
      onClose={onClose}
    />,
  );
}

describe('TransferItemDialog', () => {
  it('does not allow transfer until an item is selected', async () => {
    server.use(handleGetCreatureInventory({ body: { gold: 0, items: [] } }));

    renderDialog();

    expect(await screen.findAllByText('Nothing here.')).toHaveLength(2);
    expect(ui.confirm.get()).toBeDisabled();
  });

  it('moves a selected stack and submits the transfer', async () => {
    let requestBody: InventoryTransferRequest | undefined;
    server.use(
      handleGetCreatureInventory(async ({ params }) => {
        return HttpResponse.json({
          gold: 0,
          items: params.creatureId === 'player-id' ? [item()] : [],
        });
      }),
      handleTransferInventory(async ({ request }) => {
        requestBody = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const onClose = vi.fn();
    const { user } = renderDialog(onClose);
    await ui.dialog.find();
    const row = await ui.item('Gold coins').find();

    await user.click(row);
    expect(screen.getByText('No changes yet.')).toBeVisible();
    expect(ui.confirm.get()).toBeDisabled();

    await user.click(ui.moveToTarget('Goblin').get());
    expect(screen.getByText('1 stack to transfer.')).toBeVisible();
    expect(ui.confirm.get()).toBeEnabled();

    await user.click(ui.confirm.get());

    await waitFor(() =>
      expect(requestBody).toEqual({
        fromId: 'player-id',
        toId: 'target-id',
        items: [{ itemId: 'item-1', quantity: 10 }],
      }),
    );
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('filters items and clears the filter', async () => {
    server.use(
      handleGetCreatureInventory(async ({ params }) => {
        return HttpResponse.json({
          gold: 0,
          items:
            params.creatureId === 'player-id'
              ? [item(), item({ itemId: 'item-2', name: 'Healing potion' })]
              : [],
        });
      }),
    );

    const { user } = renderDialog();
    await ui.dialog.find();
    expect(await ui.item('Gold coins').find()).toBeVisible();
    expect(ui.item('Healing potion').get()).toBeVisible();

    await user.type(ui.search.getAll()[0], 'missing');
    expect(ui.item('Gold coins').query()).not.toBeInTheDocument();
    expect(ui.item('Healing potion').query()).not.toBeInTheDocument();

    await user.click(ui.clearFilters.get());
    expect(ui.item('Gold coins').get()).toBeVisible();
    expect(ui.item('Healing potion').get()).toBeVisible();
  });

  it('filters items by category', async () => {
    server.use(
      handleGetCreatureInventory(async ({ params }) =>
        HttpResponse.json({
          gold: 0,
          items: params.creatureId === 'player-id' ? [item(), sword()] : [],
        }),
      ),
    );

    const { user } = renderDialog();
    await ui.dialog.find();
    expect(await ui.item('Gold coins').find()).toBeVisible();
    expect(ui.item('Iron Sword').get()).toBeVisible();

    await user.click(ui.category('Gold').get());

    expect(ui.item('Gold coins').get()).toBeVisible();
    expect(ui.item('Iron Sword').query()).not.toBeInTheDocument();
  });

  it('sorts items by weight', async () => {
    server.use(
      handleGetCreatureInventory(async ({ params }) =>
        HttpResponse.json({
          gold: 0,
          items:
            params.creatureId === 'player-id'
              ? [item({ itemId: 'light', name: 'Light coins', weight: 0.1 }), sword()]
              : [],
        }),
      ),
    );

    const { user } = renderDialog();
    await ui.dialog.find();
    await ui.item('Light coins').find();

    await user.click(ui.sort('Weight').get());

    const itemRows = screen.getAllByRole('row').slice(1, 3);
    expect(itemRows[0]).toHaveTextContent('Iron Sword');
    expect(itemRows[1]).toHaveTextContent('Light coins');
  });

  it('transfers selected items from the other inventory to the player', async () => {
    let requestBody: InventoryTransferRequest | undefined;
    server.use(
      handleGetCreatureInventory(async ({ params }) =>
        HttpResponse.json({
          gold: 0,
          items: params.creatureId === 'target-id' ? [item({ name: 'Target coins' })] : [],
        }),
      ),
      handleTransferInventory(async ({ request }) => {
        requestBody = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const onClose = vi.fn();
    const { user } = renderDialog(onClose);
    await ui.dialog.find();
    await user.click(await ui.item('Target coins').find());
    await user.click(ui.moveToPlayer.get());
    await user.click(ui.confirm.get());

    await waitFor(() =>
      expect(requestBody).toEqual({
        fromId: 'target-id',
        toId: 'player-id',
        items: [{ itemId: 'item-1', quantity: 10 }],
      }),
    );
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('closes without transferring when cancelled', async () => {
    server.use(handleGetCreatureInventory({ body: { gold: 0, items: [] } }));
    const onClose = vi.fn();
    const { user } = renderDialog(onClose);

    await ui.dialog.find();
    await user.click(ui.cancel.get());

    expect(onClose).toHaveBeenCalledOnce();
  });
});
