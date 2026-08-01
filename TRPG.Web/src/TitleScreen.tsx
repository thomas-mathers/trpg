import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';

import {
  deleteWorldsByWorldIdMutation,
  getWorldsOptions,
  postSessionsMutation,
} from './api/client';
import { Button } from './components/ui/button';

function TitleScreen() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const worldsQuery = useQuery(getWorldsOptions());
  const worlds = worldsQuery.data ?? [];

  const dropWorld = useMutation(deleteWorldsByWorldIdMutation());
  const startSession = useMutation(postSessionsMutation());

  const handleDrop = (worldId: string) => {
    dropWorld.mutate(
      { path: { worldId } },
      {
        onSuccess: () => {
          queryClient.invalidateQueries({ queryKey: getWorldsOptions().queryKey });
        },
      },
    );
  };

  const handleContinue = (worldId: string) => {
    startSession.mutate(
      { query: { worldId } },
      {
        onSuccess: (data) => {
          navigate({ to: '/session/$sessionId', params: { sessionId: data.sessionId } });
        },
      },
    );
  };

  return (
    <div className="flex h-screen flex-col items-center justify-center gap-4">
      <h1 className="font-cinzel text-2xl font-semibold">TRPG</h1>

      {worldsQuery.isLoading && <p>Loading worlds...</p>}
      {!worldsQuery.isLoading && worlds.length === 0 && <p>No worlds found.</p>}

      <ul className="flex w-80 flex-col gap-2">
        {worlds.map((world) => (
          <li
            key={world.worldId}
            className="flex items-center justify-between gap-2 rounded-md border p-2"
          >
            <span>{world.name}</span>
            <div className="flex gap-2">
              {world.hasPlayer && (
                <Button
                  onClick={() => handleContinue(world.worldId)}
                  disabled={startSession.isPending}
                >
                  Continue
                </Button>
              )}
              <Button
                variant="destructive"
                onClick={() => handleDrop(world.worldId)}
                disabled={dropWorld.isPending}
              >
                Drop
              </Button>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}

export default TitleScreen;
