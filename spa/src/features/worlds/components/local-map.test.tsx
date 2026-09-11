import { screen, within } from '@testing-library/react';
import { ReactFlowProvider } from '@xyflow/react';
import { expect, it } from 'vitest';

import type { LocalMapResponse, LocalMapRoomResponse } from '@/api/client';
import { renderWithProviders } from '@/test/test-utils';

import { LocalMap } from './local-map';

it('indicates a corpse on another floor and clears the indicator after recovery', async () => {
  const current: LocalMapRoomResponse = {
    id: 'current',
    name: 'Entry',
    floorNumber: 0,
    bounds: null,
    role: null,
    isVisited: true,
    isFrontier: false,
    markers: [],
  };
  const lower: LocalMapRoomResponse = {
    ...current,
    id: 'lower',
    name: 'Cellar',
    floorNumber: -1,
    markers: [
      {
        id: 'corpse',
        name: 'Your corpse',
        kind: 'PlayerCorpse',
        state: 'RecoverableLoot',
        isLocked: false,
        itemCount: 3,
      },
    ],
  };
  const map: LocalMapResponse = {
    buildingId: 'building',
    buildingName: 'Mine',
    buildingType: 'Mine',
    currentRoomId: current.id,
    rooms: [current, lower],
    passages: [],
  };
  const { user, rerender } = renderWithProviders(
    <ReactFlowProvider>
      <LocalMap map={map} />
    </ReactFlowProvider>,
  );
  const floor = screen.getByRole('button', { name: /Lower 1/ });
  expect(within(floor).getByLabelText('Your corpse on this floor')).toBeVisible();
  await user.click(floor);
  expect(floor).toHaveAttribute('aria-pressed', 'true');
  expect(screen.getByRole('button', { name: 'Fit floor' })).toBeVisible();
  rerender(
    <ReactFlowProvider>
      <LocalMap map={{ ...map, rooms: [current, { ...lower, markers: [] }] }} />
    </ReactFlowProvider>,
  );
  expect(screen.queryByLabelText('Your corpse on this floor')).not.toBeInTheDocument();
});
