import { useEffect, useState } from 'react';

const REVEAL_DELAY_MS = 1500;

// Delays unconditionally on every ready transition, including reconnect-restored state with no narration to read.
export function useDelayedReveal(ready: boolean): boolean {
  const [isRevealed, setIsRevealed] = useState(false);

  useEffect(() => {
    if (!ready) {
      setIsRevealed(false);
      return;
    }
    const timeout = setTimeout(() => setIsRevealed(true), REVEAL_DELAY_MS);
    return () => clearTimeout(timeout);
  }, [ready]);

  return isRevealed;
}
