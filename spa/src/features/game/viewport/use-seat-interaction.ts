import { useRef } from 'react';

import { useGameChat } from '../hooks/use-game-chat';
import { useChatHub } from '../hooks/use-game-hub-connection';
import { toastReply } from '../reply-toast';
import type { ViewportSeat } from './seat-interaction';

export function useSeatInteraction(seated: boolean, canInteract: boolean) {
  const pending = useRef(false);
  const chatHub = useChatHub();
  const { submitNarratedTurn } = useGameChat();
  return (seat: ViewportSeat | undefined) => {
    if (pending.current || !canInteract || (!seated && (!seat || seat.isOccupied))) return;
    pending.current = true;
    const release = () => {
      pending.current = false;
    };
    try {
      const stream = seated ? chatHub.sendStandUp() : chatHub.sendSitDown(seat!.id);
      submitNarratedTurn(null, toastReply(stream), release, release);
    } catch (error) {
      release();
      throw error;
    }
  };
}
