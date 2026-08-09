import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { byRole, byText } from 'testing-library-selector';
import { describe, expect, it, vi } from 'vitest';

import type { EquipItemRequest, ItemDetail, ItemDetailGoldDetail } from '@/api/client';
import {
  handleEquipCreatureItem,
  handleGetCreatureInventory,
  handleUnequipCreatureItem,
} from '@/api/client/msw.gen';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { InventoryDialog } from './inventory-dialog';

vi.mock('@/features/inventory/components/character-stats-panel', () => ({
  CharacterStatsPanel: () => <div aria-label="Character stats" />,
}));

const ui = {
  dialog: byRole('dialog', { name: 'Inventory' }),
  item: (name: string) => byText(name),
  equip: byRole('button', { name: 'Equip' }),
  unequip: byRole('button', { name: 'Unequip' }),
  close: byRole('button', { name: 'Close inventory' }),
  clearFilters: byRole('button', { name: 'Clear filters' }),
  category: (name: string) => byRole('button', { name }),
  sort: (name: string) => byRole('button', { name }),
};

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

const gold = (overrides: Partial<ItemDetailGoldDetail> = {}): ItemDetail => ({
  $type: 'Gold',
  itemId: 'gold-1',
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
});

const armor = (overrides: Partial<ItemDetail> = {}): ItemDetail =>
  ({
    $type: 'Armor',
    itemId: 'armor-1',
    name: 'Leather Armor',
    description: 'A protective leather vest.',
    weight: 4,
    quantity: 1,
    equippedSlot: null,
    type: 'Chest',
    rarity: null,
    goldValue: 15,
    modifiers: [],
    defense: 4,
    armorClass: 'Light',
    durabilityCurrent: 10,
    durabilityMax: 10,
    ...overrides,
  }) as ItemDetail;

function renderDialog(onClose = vi.fn()) {
  return renderWithProviders(<InventoryDialog playerId="player-id" open onClose={onClose} />);
}

describe('InventoryDialog', () => {
  it('shows an empty state and closes when requested', async () => {
    server.use(handleGetCreatureInventory({ body: { gold: 0, items: [] } }));
    const onClose = vi.fn();
    const { user } = renderDialog(onClose);

    expect(await ui.dialog.find()).toBeVisible();
    expect(await ui.item('Your inventory is empty.').find()).toBeVisible();
    await user.click(ui.close.get());

    expect(onClose).toHaveBeenCalledOnce();
  });

  it('filters items and clears the filter', async () => {
    server.use(
      handleGetCreatureInventory({
        body: {
          gold: 0,
          items: [sword(), sword({ itemId: 'steel-sword-1', name: 'Steel Sword' })],
        },
      }),
    );
    const { user } = renderDialog();
    await ui.dialog.find();
    expect(await ui.item('Iron Sword').find()).toBeVisible();
    expect(ui.item('Steel Sword').get()).toBeVisible();

    await user.type(screen.getByPlaceholderText('Search...'), 'missing');
    expect(await ui.item('No items match your filters.').find()).toBeVisible();
    await user.click(ui.clearFilters.get());

    expect(ui.item('Iron Sword').get()).toBeVisible();
    expect(ui.item('Steel Sword').get()).toBeVisible();
  });

  it('submits an equip request for a selected item', async () => {
    let requestBody: EquipItemRequest | undefined;
    server.use(
      handleGetCreatureInventory({ body: { gold: 0, items: [sword()] } }),
      handleEquipCreatureItem(async ({ request }) => {
        requestBody = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const { user } = renderDialog();
    await ui.dialog.find();
    await user.click(await ui.item('Iron Sword').find());
    await user.click(ui.equip.get());

    await waitFor(() => expect(requestBody).toEqual({ itemId: 'sword-1', slot: 'RightHand' }));
  });

  it('submits an unequip request for an equipped item', async () => {
    let slot: string | undefined;
    server.use(
      handleGetCreatureInventory({
        body: { gold: 0, items: [sword({ equippedSlot: 'RightHand' })] },
      }),
      handleUnequipCreatureItem(({ params }) => {
        slot = params.slot;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const { user } = renderDialog();
    await ui.dialog.find();

    await user.click(await ui.unequip.find());

    await waitFor(() => expect(slot).toBe('RightHand'));
  });

  it('filters items by category', async () => {
    server.use(handleGetCreatureInventory({ body: { gold: 0, items: [sword(), gold()] } }));
    const { user } = renderDialog();
    await ui.dialog.find();
    expect(await ui.item('Iron Sword').find()).toBeVisible();
    expect(ui.item('Gold coins').get()).toBeVisible();

    await user.click(ui.category('Gold').get());

    expect(ui.item('Iron Sword').query()).not.toBeInTheDocument();
    expect(ui.item('Gold coins').get()).toBeVisible();
  });

  it('sorts items by value', async () => {
    server.use(
      handleGetCreatureInventory({
        body: {
          gold: 0,
          items: [
            sword({ itemId: 'low-value', name: 'Low Value Sword', goldValue: 10 }),
            sword({ itemId: 'high-value', name: 'High Value Sword', goldValue: 100 }),
          ],
        },
      }),
    );
    const { user } = renderDialog();
    await ui.dialog.find();
    await ui.item('Low Value Sword').find();

    await user.click(ui.sort('Value').get());

    const itemRows = screen.getAllByRole('row').slice(1);
    expect(itemRows[0]).toHaveTextContent('High Value Sword');
    expect(itemRows[1]).toHaveTextContent('Low Value Sword');
  });

  it('shows sortable weapon and defense columns for matching filters', async () => {
    server.use(
      handleGetCreatureInventory({
        body: {
          gold: 0,
          items: [
            sword({ itemId: 'low-damage', name: 'Low Damage Sword', minDamage: 2 }),
            sword({ itemId: 'high-damage', name: 'High Damage Sword', minDamage: 6 }),
            armor(),
          ],
        },
      }),
    );
    const { user } = renderDialog();
    await ui.dialog.find();
    await ui.item('Low Damage Sword').find();

    expect(screen.getAllByRole('columnheader').map((header) => header.textContent?.trim())).toEqual(
      ['Item', 'Damage', 'Defense', 'Qty', 'Value', 'Weight', ''],
    );

    await user.click(ui.category('Weapons').get());
    expect(screen.getAllByRole('columnheader').map((header) => header.textContent?.trim())).toEqual(
      ['Item', 'Damage', 'Qty', 'Value', 'Weight', ''],
    );
    await user.click(ui.sort('Damage').get());
    expect(screen.getAllByRole('row')[1]).toHaveTextContent('High Damage Sword');

    await user.click(ui.category('Armor').get());
    expect(screen.getAllByRole('columnheader').map((header) => header.textContent?.trim())).toEqual(
      ['Item', 'Damage', 'Defense', 'Qty', 'Value', 'Weight', ''],
    );
  });
});
