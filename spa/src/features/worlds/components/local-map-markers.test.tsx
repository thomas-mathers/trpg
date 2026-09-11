import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { LocalMapMarkerResponse } from '@/api/client';
import { renderWithProviders } from '@/test/test-utils';

import { MapLock } from './local-map-lock';
import { RoomMarkers } from './local-map-markers';

const chest: LocalMapMarkerResponse = {
  id: 'chest',
  name: 'Oak chest',
  kind: 'Chest',
  state: 'ContainsItems',
  isLocked: false,
  itemCount: null,
};

describe('local map markers', () => {
  it.each([
    ['ContainsItems', 'Contains items'],
    ['Empty', 'Empty'],
  ] as const)('describes the chest state %s', (state, label) => {
    renderWithProviders(<RoomMarkers markers={[{ ...chest, state }]} />);
    expect(screen.getByRole('img', { name: `Oak chest: ${label}` })).toBeVisible();
  });

  it.each([
    ['Unactivated', 'Not activated'],
    ['Activated', 'Activated'],
  ] as const)('describes the lever state %s', (state, label) => {
    renderWithProviders(
      <RoomMarkers markers={[{ ...chest, name: 'Brass lever', kind: 'Lever', state }]} />,
    );
    expect(screen.getByRole('img', { name: `Brass lever: ${label}` })).toBeVisible();
  });

  it('groups chests into one legend marker', () => {
    renderWithProviders(
      <RoomMarkers
        markers={[
          chest,
          { ...chest, id: 'empty', name: 'Iron chest', state: 'Empty', isLocked: true },
        ]}
      />,
    );
    expect(screen.getByRole('img', { name: '2 chests' })).toBeVisible();
  });

  it('removes recovered corpse markers when refreshed', () => {
    const { rerender } = renderWithProviders(
      <RoomMarkers
        markers={[
          {
            ...chest,
            kind: 'PlayerCorpse',
            state: 'RecoverableLoot',
            name: 'Your corpse',
            itemCount: 2,
          },
        ]}
      />,
    );
    expect(
      screen.getByRole('img', { name: 'Your corpse: Recoverable items remain (2 item stacks)' }),
    ).toBeVisible();
    rerender(<RoomMarkers markers={[]} />);
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it.each([
    ['KeyLockedDoor', 'Locked door · Requires a key'],
    ['Portcullis', 'Locked portcullis · Operated by a lever'],
    ['LockedDoor', 'Locked door'],
  ] as const)('labels %s without naming an undiscovered controller', (kind, label) => {
    renderWithProviders(<MapLock kind={kind} angle={90} />);
    expect(screen.getByRole('img', { name: label })).toBeVisible();
  });
});
