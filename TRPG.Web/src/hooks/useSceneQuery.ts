import { useQuery } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';

import { getSessionsBySessionIdSceneOptions } from '@/api/client';

export function useSceneQuery(sessionId: string, turnCount: number) {
  const query = useQuery(getSessionsBySessionIdSceneOptions({ path: { sessionId } }));

  const previousTurnCount = useRef(turnCount);
  useEffect(() => {
    if (turnCount !== previousTurnCount.current) {
      previousTurnCount.current = turnCount;
      query.refetch();
    }
  }, [turnCount, query]);

  return query;
}
