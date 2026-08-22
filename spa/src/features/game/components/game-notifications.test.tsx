import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { gameEventBus } from '@/lib/game-event-bus';

import { GameNotifications } from './game-notifications';

describe('GameNotifications', () => {
  it('shows crime witness notifications', async () => {
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: () => ({
        matches: false,
        addEventListener: () => {},
        removeEventListener: () => {},
      }),
    });

    render(<GameNotifications />);

    gameEventBus.emit('CrimeWitnessed', { crimeName: 'theft' });

    expect(await screen.findByText('Someone saw your theft.')).toBeVisible();

    gameEventBus.emit('CrimeWitnessesRemoved', { crimeName: 'killing' });

    expect(await screen.findByText('A killing will go unreported.')).toBeVisible();

    gameEventBus.emit('QuestJournalUpdated', 'Recover the Ledger');

    expect(await screen.findByText('Recover the Ledger')).toBeVisible();
  });
});
