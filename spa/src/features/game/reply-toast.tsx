import type { IStreamResult } from '@microsoft/signalr';
import { Info } from 'lucide-react';
import { toast } from 'sonner';

import { GameToast } from './components/game-toast';

export function toastReply(stream: IStreamResult<string>): IStreamResult<string> {
  return {
    subscribe(subscriber) {
      let text = '';
      return stream.subscribe({
        next(token) {
          text += token;
          subscriber.next(token);
        },
        error(error) {
          subscriber.error(error);
        },
        complete() {
          if (text.trim()) {
            toast.custom(
              (toastId) => (
                <GameToast toastId={toastId} icon={Info} title="Blocked" description={text} />
              ),
              { duration: 3800 },
            );
          }
          subscriber.complete();
        },
      });
    },
  };
}
