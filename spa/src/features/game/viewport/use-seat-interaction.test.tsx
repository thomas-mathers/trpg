import { act, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { IChatHub } from '@/api/signalr-client/TypedSignalR.Client/TRPG.GameSessions.Hubs';
import { renderWithProviders } from '@/test/test-utils';

import { GameChatContext, type GameChat } from '../hooks/use-game-chat';
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
  const stream = {};
  const hub = { sendSitDown: vi.fn(() => stream), sendStandUp: vi.fn(() => stream) };
  const submit = vi.fn<GameChat['submitNarratedTurn']>();
  const view = renderWithProviders(
    <GameHubConnectionContext.Provider
      value={{ chatHub: hub as unknown as IChatHub } as GameHubConnection}
    >
      <GameChatContext.Provider
        value={{ messages: [], isStreaming: false, submitNarratedTurn: submit }}
      >
        <Interaction seated={seated} canInteract={canInteract} seat={seat} />
      </GameChatContext.Provider>
    </GameHubConnectionContext.Provider>,
  );
  return { ...view, hub, stream, submit };
}
describe('seat turn handoff', () => {
  it('submits the selected chair once until the turn settles', async () => {
    const { user, hub, stream, submit } = setup();
    await user.dblClick(screen.getByRole('button', { name: 'Interact' }));
    expect(hub.sendSitDown).toHaveBeenCalledExactlyOnceWith('chair');
    expect(submit).toHaveBeenCalledWith(null, stream, expect.any(Function), expect.any(Function));
    act(() => submit.mock.calls[0][3]?.());
    await user.click(screen.getByRole('button', { name: 'Interact' }));
    expect(hub.sendSitDown).toHaveBeenCalledTimes(2);
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
  it('allows retrying after a failed turn', async () => {
    const { user, hub, submit } = setup();
    await user.click(screen.getByRole('button', { name: 'Interact' }));
    act(() => submit.mock.calls[0][2]?.(new Error('Seat taken')));
    await user.click(screen.getByRole('button', { name: 'Interact' }));
    expect(hub.sendSitDown).toHaveBeenCalledTimes(2);
  });
});
