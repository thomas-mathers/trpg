import { useEffect, useRef, useState } from 'react';

const FLOAT_DURATION_MS = 900;

// Detects a change in a stat between renders so the card can pop a floating
// +/-N number over the bar it belongs to — CombatUpdated only ever gives a
// fresh snapshot, never a delta, so the delta has to be derived client-side.
export function useStatDelta(current: number): number | null {
  const previous = useRef(current);
  const [delta, setDelta] = useState<number | null>(null);

  useEffect(() => {
    if (previous.current === current) {
      return;
    }

    const change = current - previous.current;
    previous.current = current;
    setDelta(change);

    const timeout = setTimeout(() => setDelta(null), FLOAT_DURATION_MS);
    return () => clearTimeout(timeout);
  }, [current]);

  return delta;
}
