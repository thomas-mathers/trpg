import { useEffect, useRef, useState } from 'react';

const FLOAT_DURATION_MS = 900;

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
