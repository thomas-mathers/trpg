import { Info } from 'lucide-react';
import { useCallback, useState } from 'react';
import { toast } from 'sonner';

import type { ActionResult } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { actionFailureCopy } from './action-failure-copy';
import { GameToast } from './components/game-toast';

const FAILURE_RESULT: ActionResult = { succeeded: false };
const GENERIC_FAILURE = 'Something went wrong.';

function toastFailure(description: string) {
  toast.custom(
    (toastId) => (
      <GameToast toastId={toastId} icon={Info} title="Blocked" description={description} />
    ),
    { duration: 3800 },
  );
}

export async function runAction(action: Promise<ActionResult>): Promise<ActionResult> {
  try {
    const result = await action;
    if (!result.succeeded) {
      toastFailure(result.reason ? actionFailureCopy[result.reason] : GENERIC_FAILURE);
    }
    return result;
  } catch (error) {
    console.error('Action failed', error);
    toastFailure(GENERIC_FAILURE);
    return FAILURE_RESULT;
  }
}

export function useAction() {
  const [pending, setPending] = useState(false);

  const run = useCallback(async (action: Promise<ActionResult>) => {
    setPending(true);
    try {
      return await runAction(action);
    } finally {
      setPending(false);
    }
  }, []);

  return { pending, run };
}
