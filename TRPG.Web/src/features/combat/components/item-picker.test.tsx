import { faker } from '@faker-js/faker';
import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { byRole, byText } from 'testing-library-selector';
import { describe, expect, it, vi } from 'vitest';

import type { ConsumableSummary, ResourceType } from '@/api/client';
import { handleGetCreatureConsumables } from '@/api/client/msw.gen';
import { gameEventBus } from '@/lib/game-event-bus';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { ItemPicker, type ItemPickerProps } from './item-picker';

const ui = {
  item: (name: string) => byRole('button', { name: new RegExp(name) }),
  search: byRole('textbox'),
  back: byRole('button', { name: /Back/ }),
  empty: byText('No usable items.'),
};

function item(name: string, resource: ResourceType = 'Hp'): ConsumableSummary {
  return {
    itemId: faker.string.uuid(),
    name,
    quantity: 2,
    resource,
    restoreAmount: 25,
  };
}

function mockItems(items: ConsumableSummary[]) {
  server.use(handleGetCreatureConsumables({ body: items }));
}

function renderComponent({
  onBack = vi.fn(),
  onChoose = vi.fn(),
  ...rest
}: Partial<ItemPickerProps> = {}) {
  return renderWithProviders(
    <ItemPicker playerId={faker.string.uuid()} onBack={onBack} onChoose={onChoose} {...rest} />,
  );
}

describe('ItemPicker', () => {
  it('renders items with their resource and quantity', async () => {
    const potion = item('Healing Potion');
    const elixir = item('Mana Elixir', 'Mp');
    mockItems([potion, elixir]);

    renderComponent();

    expect(await ui.item('Healing Potion').find()).toBeVisible();
    expect(screen.getByText('+25 HP')).toBeVisible();
    expect(screen.getByText('+25 MP')).toBeVisible();
    expect(screen.getAllByText(/×2/)).toHaveLength(2);
  });

  it('filters items when the inventory is large enough to search', async () => {
    mockItems([
      item('Healing Potion'),
      item('Mana Elixir', 'Mp'),
      item('Stamina Tonic', 'Ap'),
      item('Antidote'),
      item('Iron Skin Draught'),
      item('Swift Brew'),
      item('Focus Tea'),
    ]);

    const { user } = renderComponent();
    const search = await ui.search.find();
    await user.type(search, '  mana  ');

    expect(ui.item('Mana Elixir').get()).toBeVisible();
    expect(ui.item('Healing Potion').query()).not.toBeInTheDocument();
  });

  it('shows an empty state for an empty inventory', async () => {
    mockItems([]);
    renderComponent();
    expect(await ui.empty.find()).toBeVisible();
  });

  it('shows a no-match state when the search has no results', async () => {
    mockItems([
      item('Healing Potion'),
      item('Mana Elixir', 'Mp'),
      item('Stamina Tonic', 'Ap'),
      item('Antidote'),
      item('Iron Skin Draught'),
      item('Swift Brew'),
      item('Focus Tea'),
    ]);

    const { user } = renderComponent();
    const search = await ui.search.find();
    await user.type(search, 'unknown');

    expect(await screen.findByText('No matches for “unknown”.')).toBeVisible();
  });

  it('passes the selected item to onChoose and supports back navigation', async () => {
    const selected = item('Healing Potion');
    mockItems([selected]);
    const onChoose = vi.fn();
    const onBack = vi.fn();
    const { user } = renderComponent({ onChoose, onBack });

    await user.click(await ui.item('Healing Potion').find());
    await user.click(ui.back.get());

    expect(onChoose).toHaveBeenCalledWith(selected);
    expect(onBack).toHaveBeenCalledOnce();
  });

  it('refetches items when combat starts', async () => {
    const potion = item('Healing Potion');
    let requestCount = 0;
    server.use(
      handleGetCreatureConsumables(() => {
        requestCount += 1;
        return HttpResponse.json([potion]);
      }),
    );

    renderComponent();
    await ui.item('Healing Potion').find();
    expect(requestCount).toBe(1);

    gameEventBus.emit('CombatStarted', {} as never);

    await waitFor(() => expect(requestCount).toBe(2));
  });
});
