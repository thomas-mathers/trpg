import { screen, waitFor, within } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { byRole, byTitle } from 'testing-library-selector';
import { describe, expect, it, vi } from 'vitest';

import type {
  InventoryTransferRequest,
  ItemDetail,
  ItemDetailConsumableItemDetail,
  ItemDetailGoldDetail,
} from '@/api/client';
import {
  handleGetContainerInventory,
  handleGetCreatureInventory,
  handleGetTheftDetectionChance,
  handleTransferInventory,
} from '@/api/client/msw.gen';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { TransferItemDialog } from './transfer-item-dialog';

const ui = {
  dialog: byRole('dialog'),
  confirm: byRole('button', { name: 'Confirm Transfer' }),
  cancel: byRole('button', { name: 'Cancel' }),
  clearFilters: byRole('button', { name: 'Clear filters' }),
  item: (name: string) => byRole('checkbox', { name: `Select ${name}` }),
  moveToPlayer: byTitle('Move selected items to your inventory'),
  moveToTarget: (name: string) => byTitle(`Move selected items to ${name}`),
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
  goldValue: 1,
  modifiers: [],
  ...overrides,
  isStackable: true,
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
    isStackable: false,
  }) as ItemDetail;

const potion = (overrides: Partial<ItemDetailConsumableItemDetail> = {}): ItemDetail => ({
  $type: 'Consumable',
  itemId: 'potion-1',
  name: 'Healing potion',
  description: 'Restores health.',
  resource: 'Hp',
  restoreAmount: 20,
  duration: 0,
  weight: 0.1,
  quantity: 10,
  equippedSlot: null,
  type: 'Consumable',
  rarity: null,
  goldValue: 5,
  modifiers: [],
  ...overrides,
  isStackable: true,
});

function renderDialog(
  onClose = vi.fn(),
  transfersEnabled = true,
  ownerType: 'Creature' | 'Container' = 'Creature',
  onTheftEncounter?: (encounterId: string) => void,
) {
  return renderWithProviders(
    <TransferItemDialog
      playerId="player-id"
      target={{ id: 'target-id', name: 'Goblin', ownerType }}
      open
      transfersEnabled={transfersEnabled}
      onClose={onClose}
      onTheftEncounter={onTheftEncounter}
    />,
  );
}

describe('TransferItemDialog', () => {
  it('does not allow transfer until an item is selected', async () => {
    server.use(
      handleGetCreatureInventory({
        body: { gold: 0, items: [], weight: 0, carryingCapacity: null },
      }),
    );

    renderDialog();

    expect(await screen.findAllByText('Nothing here.')).toHaveLength(2);
    expect(ui.confirm.get()).toBeDisabled();
  });

  it('disables transfers when viewing a living creature inventory', async () => {
    server.use(
      handleGetCreatureInventory(async ({ params }) =>
        HttpResponse.json({
          gold: 0,
          items: params.creatureId === 'player-id' ? [item()] : [],
        }),
      ),
    );

    const { user } = renderDialog(vi.fn(), false);
    await ui.dialog.find();
    expect(screen.getByRole('heading', { name: 'Inspect Inventory' })).toBeVisible();
    await user.click(await ui.item('Gold coins').find());

    expect(ui.item('Gold coins').get()).toBeDisabled();
    expect(ui.moveToTarget('Goblin').get()).toBeDisabled();
    expect(ui.moveToPlayer.get()).toBeDisabled();
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
        return HttpResponse.json({ theftEncounterId: null });
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
        from: { id: 'player-id', type: 'Creature' },
        to: { id: 'target-id', type: 'Creature' },
        items: [{ itemId: 'item-1', quantity: 10 }],
      }),
    );
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('merges a returned partial stack with its source stack', async () => {
    // Arrange
    server.use(
      handleGetCreatureInventory(async ({ params }) =>
        HttpResponse.json({
          gold: 0,
          items: params.creatureId === 'player-id' ? [potion()] : [],
        }),
      ),
    );
    const { user } = renderDialog();
    await ui.dialog.find();
    await user.click(await ui.item('Healing potion').find());
    const quantity = screen.getByRole('spinbutton', { name: 'Transfer Healing potion quantity' });
    await user.clear(quantity);
    await user.type(quantity, '2');

    // Act
    await user.click(ui.moveToTarget('Goblin').get());
    const targetInventory = screen.getByRole('region', { name: "Goblin's inventory" });
    await user.click(
      within(targetInventory).getByRole('checkbox', { name: 'Select Healing potion' }),
    );
    await user.click(ui.moveToPlayer.get());

    // Assert
    expect(screen.getByRole('row', { name: /Healing potion/ })).toHaveTextContent('10');
    expect(targetInventory).not.toHaveTextContent('Healing potion');
    expect(screen.getByText('No changes yet.')).toBeVisible();
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
    const playerInventory = screen.getByRole('region', { name: 'Your inventory' });

    await user.type(within(playerInventory).getByRole('textbox', { name: 'Search' }), 'missing');
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
    const playerInventory = screen.getByRole('region', { name: 'Your inventory' });

    await user.click(within(playerInventory).getByRole('button', { name: 'Gold' }));

    expect(ui.item('Gold coins').get()).toBeVisible();
    expect(ui.item('Iron Sword').query()).not.toBeInTheDocument();
  });

  it('filters items by equipped status', async () => {
    server.use(
      handleGetCreatureInventory(async ({ params }) =>
        HttpResponse.json({
          gold: 0,
          items:
            params.creatureId === 'player-id' ? [item(), sword({ equippedSlot: 'RightHand' })] : [],
        }),
      ),
    );

    const { user } = renderDialog();
    await ui.dialog.find();
    expect(await ui.item('Gold coins').find()).toBeVisible();
    expect(ui.item('Iron Sword').get()).toBeVisible();
    const playerInventory = screen.getByRole('region', { name: 'Your inventory' });

    await user.click(within(playerInventory).getByRole('button', { name: 'Equipped' }));

    expect(ui.item('Gold coins').query()).not.toBeInTheDocument();
    expect(ui.item('Iron Sword').get()).toBeVisible();
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
        return HttpResponse.json({ theftEncounterId: null });
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
        from: { id: 'target-id', type: 'Creature' },
        to: { id: 'player-id', type: 'Creature' },
        items: [{ itemId: 'item-1', quantity: 10 }],
      }),
    );
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('shows the detection chance when items are selected from the other inventory', async () => {
    let requestBody: unknown;
    server.use(
      handleGetCreatureInventory(async ({ params }) =>
        HttpResponse.json({
          gold: 0,
          items: params.creatureId === 'target-id' ? [item({ name: 'Target coins' })] : [],
        }),
      ),
      handleGetTheftDetectionChance(async ({ request }) => {
        requestBody = await request.json();
        return HttpResponse.json({ successChance: 0.75 });
      }),
    );

    const { user } = renderDialog();
    await ui.dialog.find();
    await user.click(await ui.item('Target coins').find());

    expect(await screen.findByText('75% chance to avoid detection.')).toBeVisible();
    expect(requestBody).toEqual({
      from: { id: 'target-id', type: 'Creature' },
      items: [{ itemId: 'item-1', quantity: 10 }],
    });
  });

  it('loots a container by reading and transferring against its own inventory endpoint', async () => {
    let requestBody: InventoryTransferRequest | undefined;
    server.use(
      handleGetCreatureInventory({
        body: { gold: 0, items: [], weight: 0, carryingCapacity: null },
      }),
      handleGetContainerInventory(async () =>
        HttpResponse.json({
          gold: 0,
          items: [item({ name: 'Chest coins' })],
          weight: 0,
          carryingCapacity: null,
        }),
      ),
      handleTransferInventory(async ({ request }) => {
        requestBody = await request.json();
        return HttpResponse.json({ theftEncounterId: null });
      }),
    );

    const onClose = vi.fn();
    const { user } = renderDialog(onClose, true, 'Container');
    await ui.dialog.find();
    await user.click(await ui.item('Chest coins').find());
    await user.click(ui.moveToPlayer.get());
    await user.click(ui.confirm.get());

    await waitFor(() =>
      expect(requestBody).toEqual({
        from: { id: 'target-id', type: 'Container' },
        to: { id: 'player-id', type: 'Creature' },
        items: [{ itemId: 'item-1', quantity: 10 }],
      }),
    );
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('disables moving an item to the player when it would exceed carrying capacity', async () => {
    server.use(
      handleGetCreatureInventory({
        body: { gold: 0, items: [], weight: 0, carryingCapacity: 1 },
      }),
      handleGetContainerInventory({
        body: {
          gold: 0,
          items: [sword({ weight: 5 })],
          weight: 5,
          carryingCapacity: null,
        },
      }),
    );

    const { user } = renderDialog(vi.fn(), true, 'Container');
    await ui.dialog.find();
    await user.click(await ui.item('Iron Sword').find());

    expect(byTitle('Carrying too much weight to take this').get()).toBeDisabled();
  });

  it('closes before starting a caught theft encounter', async () => {
    server.use(
      handleGetCreatureInventory({
        body: { gold: 0, items: [], weight: 0, carryingCapacity: null },
      }),
      handleGetContainerInventory({
        body: {
          gold: 0,
          items: [item({ name: 'Chest coins' })],
          weight: 0,
          carryingCapacity: null,
        },
      }),
      handleTransferInventory(() => HttpResponse.json({ theftEncounterId: 'theft-encounter-id' })),
    );

    const onClose = vi.fn();
    const onTheftEncounter = vi.fn(() => expect(onClose).toHaveBeenCalledOnce());
    const { user } = renderDialog(onClose, true, 'Container', onTheftEncounter);
    await ui.dialog.find();
    await user.click(await ui.item('Chest coins').find());
    await user.click(ui.moveToPlayer.get());
    await user.click(ui.confirm.get());

    await waitFor(() => expect(onTheftEncounter).toHaveBeenCalledWith('theft-encounter-id'));
  });

  it('closes without transferring when cancelled', async () => {
    server.use(
      handleGetCreatureInventory({
        body: { gold: 0, items: [], weight: 0, carryingCapacity: null },
      }),
    );
    const onClose = vi.fn();
    const { user } = renderDialog(onClose);

    await ui.dialog.find();
    await user.click(await ui.cancel.find());

    expect(onClose).toHaveBeenCalledOnce();
  });
});
