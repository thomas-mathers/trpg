import { screen, waitFor, within } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import type { ItemDetail, ItemDetailGoldDetail, TradeRequest, TradeSnapshot } from '@/api/client';
import { handleCompleteTrade, handleGetTrade, handleProposeTrade } from '@/api/client/msw.gen';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { TradeDialog } from './trade-dialog';

const gold = (overrides: Partial<ItemDetailGoldDetail> = {}): ItemDetail => ({
  $type: 'Gold',
  itemId: 'gold-id',
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
    itemId: 'sword-id',
    name: 'Iron Sword',
    description: 'A sturdy sword.',
    weight: 2,
    quantity: 1,
    equippedSlot: null,
    type: 'Sword',
    rarity: null,
    goldValue: 10,
    modifiers: [],
    minDamage: 3,
    maxDamage: 5,
    range: 1,
    attacksPerTurn: 1,
    isTwoHanded: false,
    ...overrides,
    isStackable: false,
  }) as ItemDetail;

function tradeSnapshot(
  playerItems: ItemDetail[] = [gold()],
  shopItems: ItemDetail[] = [sword()],
): TradeSnapshot {
  return {
    playerInventory: { gold: 0, items: playerItems },
    shopInventory: { gold: 0, items: shopItems },
  };
}

function renderDialog(onClose = vi.fn()) {
  return renderWithProviders(
    <TradeDialog
      playerId="player-id"
      worldId="world-id"
      workstationId="workstation-id"
      workerName="Tessa"
      shopName="The General Store"
      open
      onClose={onClose}
    />,
  );
}

describe('TradeDialog', () => {
  it('disables proposing when both offers are empty', async () => {
    server.use(handleGetTrade({ body: tradeSnapshot() }));

    renderDialog();

    expect(await screen.findByRole('dialog')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Propose trade' })).toBeDisabled();
  });

  it('filters and sorts each source inventory', async () => {
    server.use(
      handleGetTrade({
        body: tradeSnapshot([gold(), sword({ goldValue: 50 })]),
      }),
    );
    const { user } = renderDialog();

    await screen.findByRole('dialog');
    const playerInventory = screen.getByLabelText('Your available items');
    await user.click(within(playerInventory).getByRole('button', { name: 'Value' }));

    expect(within(playerInventory).getAllByRole('row')[1]).toHaveTextContent('Iron Sword');

    const search = within(playerInventory).getByRole('textbox', { name: 'Search' });
    await user.type(search, 'sword');

    expect(
      within(playerInventory).getByRole('button', { name: 'Add Iron Sword to offer' }),
    ).toBeVisible();
    expect(
      within(playerInventory).queryByRole('button', { name: 'Add Gold coins to offer' }),
    ).not.toBeInTheDocument();

    await user.clear(search);
    await user.click(within(playerInventory).getByRole('button', { name: 'Gold' }));

    expect(
      within(playerInventory).getByRole('button', { name: 'Add Gold coins to offer' }),
    ).toBeVisible();
    expect(
      within(playerInventory).queryByRole('button', { name: 'Add Iron Sword to offer' }),
    ).not.toBeInTheDocument();
  });

  it('shows the equipped chip for equipped inventory items', async () => {
    server.use(handleGetTrade({ body: tradeSnapshot([sword({ equippedSlot: 'RightHand' })]) }));

    renderDialog();

    const playerInventory = await screen.findByLabelText('Your available items');

    expect(within(playerInventory).getByText('Equipped', { selector: 'span' })).toBeVisible();
  });

  it('updates a stack quantity and removes it from the offer', async () => {
    server.use(handleGetTrade({ body: tradeSnapshot() }));
    const { user } = renderDialog();

    await screen.findByRole('dialog');
    await user.click(screen.getByRole('button', { name: 'Add Gold coins to offer' }));
    await user.click(screen.getByRole('button', { name: 'Decrease Gold coins quantity' }));

    expect(screen.getByRole('spinbutton', { name: 'Gold coins quantity' })).toHaveValue(9);

    await user.click(screen.getByRole('button', { name: 'Remove Gold coins from offer' }));

    expect(
      screen.queryByRole('spinbutton', { name: 'Gold coins quantity' }),
    ).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add Gold coins to offer' })).toBeVisible();
  });

  it('proposes an offer then completes an accepted trade', async () => {
    let proposal: TradeRequest | undefined;
    let completion: TradeRequest | undefined;
    server.use(
      handleGetTrade({ body: tradeSnapshot() }),
      handleProposeTrade(async ({ request, params }) => {
        proposal = await request.json();
        expect(params).toMatchObject({ playerId: 'player-id', workstationId: 'workstation-id' });
        return HttpResponse.json({ status: 'Accepted' });
      }),
      handleCompleteTrade(async ({ request, params }) => {
        completion = await request.json();
        expect(params).toMatchObject({ playerId: 'player-id', workstationId: 'workstation-id' });
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const onClose = vi.fn();
    const { user } = renderDialog(onClose);

    await screen.findByRole('dialog');
    await user.click(screen.getByRole('button', { name: 'Add Gold coins to offer' }));
    await user.click(screen.getByRole('button', { name: 'Add Iron Sword to offer' }));
    await user.click(screen.getByRole('button', { name: 'Propose trade' }));

    await waitFor(() =>
      expect(proposal).toEqual({
        playerOffer: [{ itemId: 'gold-id', quantity: 10 }],
        shopOffer: [{ itemId: 'sword-id', quantity: 1 }],
      }),
    );
    expect(screen.getByRole('status')).toHaveTextContent('Tessa accepts your offer.');

    await user.click(screen.getByRole('button', { name: 'Complete trade' }));

    await waitFor(() =>
      expect(completion).toEqual({
        playerOffer: [{ itemId: 'gold-id', quantity: 10 }],
        shopOffer: [{ itemId: 'sword-id', quantity: 1 }],
      }),
    );
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('does not complete a rejected proposal', async () => {
    let completion: TradeRequest | undefined;
    server.use(
      handleGetTrade({ body: tradeSnapshot() }),
      handleProposeTrade({ body: { status: 'Rejected' } }),
      handleCompleteTrade(async ({ request }) => {
        completion = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const { user } = renderDialog();

    await screen.findByRole('dialog');
    await user.click(screen.getByRole('button', { name: 'Add Gold coins to offer' }));
    await user.click(screen.getByRole('button', { name: 'Propose trade' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Tessa declines your offer.');
    expect(screen.queryByRole('button', { name: 'Complete trade' })).not.toBeInTheDocument();
    expect(completion).toBeUndefined();
  });

  it('does not complete a refused proposal', async () => {
    let completion: TradeRequest | undefined;
    server.use(
      handleGetTrade({ body: tradeSnapshot() }),
      handleProposeTrade({ body: { status: 'Refused' } }),
      handleCompleteTrade(async ({ request }) => {
        completion = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const { user } = renderDialog();

    await screen.findByRole('dialog');
    await user.click(screen.getByRole('button', { name: 'Add Gold coins to offer' }));
    await user.click(screen.getByRole('button', { name: 'Propose trade' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Tessa refuses to deal with you.');
    expect(screen.queryByRole('button', { name: 'Complete trade' })).not.toBeInTheDocument();
    expect(completion).toBeUndefined();
  });
});
