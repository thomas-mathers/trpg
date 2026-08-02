import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';

import {
  getSessionsBySessionIdSceneOptions,
  getSessionsBySessionIdSceneQueryKey,
} from '@/api/client';
import { gameEventBus } from '@/lib/gameEventBus';

export function useSceneQuery(sessionId: string) {
  const queryClient = useQueryClient();
  const query = useQuery(getSessionsBySessionIdSceneOptions({ path: { sessionId } }));

  useEffect(
    () =>
      gameEventBus.on('SceneChanged', (snapshot) => {
        queryClient.setQueryData(
          getSessionsBySessionIdSceneQueryKey({ path: { sessionId } }),
          snapshot,
        );
      }),
    [sessionId, queryClient],
  );

  return query;
}
