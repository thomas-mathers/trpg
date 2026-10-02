import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { ActionResult } from '@/api/signalr-client/TRPG.GameSessions.Responses';
import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { renderWithProviders } from '@/test/test-utils';

import { GameHubConnectionContext, type GameHubConnection } from '../hooks/use-game-hub-connection';
import type { ViewportSeat } from './seat-interaction';
import { useSeatInteraction } from './use-seat-interaction';

const chair = { id: 'chair', isOccupied: false } as ViewportSeat;
function Interaction({
  seated,
  canInteract,
  seat,
}: {
  seated: boolean;
  canInteract: boolean;
  seat?: ViewportSeat;
}) {
  const interact = useSeatInteraction(seated, canInteract);
  return <button onClick={() => interact(seat)}>Interact</button>;
}
function setup(seated = false, seat: ViewportSeat | undefined = chair, canInteract = true) {
  const pending: Array<(result: ActionResult) => void> = [];
  const respond = () =>
    new Promise<ActionResult>((resolve) => {
      pending.push(resolve);
    });
  const hub = { sendSitDown: vi.fn(respond), sendStandUp: vi.fn(respond) };
  const view = renderWithProviders(
    <GameHubConnectionContext.Provider
      value={{ chatHub: hub as unknown as IChatHub } as GameHubConnection}
    >
      <Interaction seated={seated} canInteract={canInteract} seat={seat} />
    </GameHubConnectionContext.Provider>,
  );
  return { ...view, hub, finish: (result: ActionResult) => pending.shift()?.(result) };
}
describe('seat interaction', () => {
  it('sends the selected chair once until the call settles', async () => {
    const { user, hub, finish } = setup();
    await user.dblClick(screen.getByRole('button', { name: 'Interact' }));
    expect(hub.sendSitDown).toHaveBeenCalledExactlyOnceWith('chair');
    finish({ succeeded: true });
    await vi.waitFor(async () => {
      await user.click(screen.getByRole('button', { name: 'Interact' }));
      expect(hub.sendSitDown).toHaveBeenCalledTimes(2);
    });
  });
  it('stands up when the server snapshot says sitting', async () => {
    const { user, hub } = setup(true);
    await user.click(screen.getByRole('button', { name: 'Interact' }));
    expect(hub.sendStandUp).toHaveBeenCalledOnce();
    expect(hub.sendSitDown).not.toHaveBeenCalled();
  });
  it.each([
    { seat: { ...chair, isOccupied: true }, allowed: true },
    { seat: chair, allowed: false },
  ])('does not send a blocked seat interaction', async ({ seat, allowed }) => {
    const { user, hub } = setup(false, seat, allowed);
    await user.click(screen.getByRole('button', { name: 'Interact' }));
    expect(hub.sendSitDown).not.toHaveBeenCalled();
    expect(hub.sendStandUp).not.toHaveBeenCalled();
  });
  it('allows retrying after a refused sit', async () => {
    const { user, hub, finish } = setup();
    await user.click(screen.getByRole('button', { name: 'Interact' }));
    finish({ succeeded: false, reason: 'SeatOccupied' });
    await vi.waitFor(async () => {
      await user.click(screen.getByRole('button', { name: 'Interact' }));
      expect(hub.sendSitDown).toHaveBeenCalledTimes(2);
    });
  });
});
