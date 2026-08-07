import { faker } from '@faker-js/faker';
import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { byRole, byText } from 'testing-library-selector';
import { describe, expect, it, vi } from 'vitest';

import type { AbilityAvailability, AbilityCategory, AbilitySummary } from '@/api/client';
import { handleGetCreatureAbilities, handleGetPlayerFightAbilities } from '@/api/client/msw.gen';
import { gameEventBus } from '@/lib/game-event-bus';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { AbilityPicker, type AbilityPickerProps } from './ability-picker';

const ui = {
  ability: (name: string) => byRole('button', { name: new RegExp(name) }),
  search: byRole('textbox'),
  back: byRole('button', { name: /Back/ }),
  reason: (text: string) => byText(text),
  empty: byText('No abilities of that type.'),
};

function ability(
  name: string,
  category: AbilityCategory = 'Offensive',
  overrides: Partial<AbilitySummary> = {},
): AbilitySummary {
  return {
    name,
    skill: 'Melee',
    description: `${name} description`,
    apCost: 2,
    mpCost: 0,
    cooldown: 0,
    category,
    requiredSkillLevel: 0,
    prerequisites: [],
    ...overrides,
  };
}

function mockAbilityData(abilities: AbilitySummary[], availability: AbilityAvailability[] = []) {
  server.use(
    handleGetCreatureAbilities({ body: abilities }),
    handleGetPlayerFightAbilities({ body: availability }),
  );
}

function renderComponent({
  category = 'Offensive',
  onBack = vi.fn(),
  onChoose = vi.fn(),
  ...rest
}: Partial<AbilityPickerProps> = {}) {
  return renderWithProviders(
    <AbilityPicker
      playerId={faker.string.uuid()}
      category={category}
      onBack={onBack}
      onChoose={onChoose}
      {...rest}
    />,
  );
}

describe('AbilityPicker', () => {
  it('shows abilities for the selected category and disables unusable abilities', async () => {
    const usable = ability('Power Strike');
    const unavailable = ability('Heavy Blow');
    const support = ability('First Aid', 'Support');

    mockAbilityData(
      [usable, unavailable, support],
      [{ name: unavailable.name, isUsable: false, reason: 'Not enough AP.' }],
    );

    renderComponent();

    expect(await ui.ability('Power Strike').find()).toBeEnabled();
    expect(ui.ability('First Aid').query()).not.toBeInTheDocument();
    expect(ui.ability('Heavy Blow').get()).toBeDisabled();
    expect(ui.reason('Not enough AP.').get()).toBeVisible();
  });

  it('filters abilities by a trimmed, case-insensitive search query', async () => {
    const abilities = [
      ability('Fire Bolt'),
      ability('Fire Wall'),
      ability('Power Strike'),
      ability('Heavy Blow'),
      ability('Quick Slash'),
      ability('Whirlwind'),
      ability('Rend'),
    ];
    mockAbilityData(abilities);

    const { user } = renderComponent();

    const search = await ui.search.find();
    await user.type(search, '  FIRE  ');

    expect(ui.ability('Fire Bolt').get()).toBeVisible();
    expect(ui.ability('Fire Wall').get()).toBeVisible();
    expect(ui.ability('Power Strike').query()).not.toBeInTheDocument();
  });

  it('shows a no-match message when the search has no results', async () => {
    const abilities = [
      ability('Fire Bolt'),
      ability('Fire Wall'),
      ability('Power Strike'),
      ability('Heavy Blow'),
      ability('Quick Slash'),
      ability('Whirlwind'),
      ability('Rend'),
    ];
    mockAbilityData(abilities);

    const { user } = renderComponent();

    const search = await ui.search.find();
    await user.type(search, 'healing');

    expect(await screen.findByText('No matches for “healing”.')).toBeVisible();
  });

  it('shows an empty state when the category has no abilities', async () => {
    mockAbilityData([ability('First Aid', 'Support')]);

    renderComponent();

    expect(await ui.empty.find()).toBeVisible();
  });

  it('passes the selected ability to onChoose and supports back navigation', async () => {
    const selected = ability('Power Strike');
    mockAbilityData([selected]);
    const onChoose = vi.fn();
    const onBack = vi.fn();

    const { user } = renderComponent({ onBack, onChoose });

    await user.click(await ui.ability('Power Strike').find());
    await user.click(ui.back.get());

    expect(onChoose).toHaveBeenCalledWith(selected);
    expect(onBack).toHaveBeenCalledOnce();
  });

  it('refetches ability availability when combat updates', async () => {
    const selected = ability('Power Strike');
    let requestCount = 0;
    server.use(
      handleGetCreatureAbilities({ body: [selected] }),
      handleGetPlayerFightAbilities(() => {
        requestCount += 1;
        return HttpResponse.json<AbilityAvailability[]>([
          { name: selected.name, isUsable: requestCount < 2, reason: 'On cooldown.' },
        ]);
      }),
    );

    renderComponent();

    await ui.ability('Power Strike').find();
    expect(requestCount).toBe(1);

    gameEventBus.emit('CombatUpdated', {} as never);

    await waitFor(() => expect(requestCount).toBe(2));
    expect(ui.ability('Power Strike').get()).toBeDisabled();
  });
});
