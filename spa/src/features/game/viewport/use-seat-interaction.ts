import { useChatHub } from '../hooks/use-game-hub-connection';
import { useAction } from '../run-action';
import type { ViewportSeat } from './seat-interaction';

export function useSeatInteraction(seated: boolean, canInteract: boolean) {
  const { pending, run } = useAction();
  const chatHub = useChatHub();
  return (seat: ViewportSeat | undefined) => {
    if (pending || !canInteract || (!seated && (!seat || seat.isOccupied))) return;
    void run(seated ? chatHub.sendStandUp() : chatHub.sendSitDown(seat!.id));
  };
}
