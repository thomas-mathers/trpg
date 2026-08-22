import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';

import { dropWorldMutation, listWorldsOptions, createSessionMutation } from '../../../api/client';
import { Button } from '../../../components/ui/button';
import { NewWorldDialog } from '../../world-generation/components/new-world-dialog';

function TitleScreen() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const worldsQuery = useQuery(listWorldsOptions());
  const worlds = worldsQuery.data ?? [];

  const dropWorld = useMutation(dropWorldMutation());
  const startSession = useMutation(createSessionMutation());

  const handleDrop = (worldId: string) => {
    dropWorld.mutate(
      { path: { worldId } },
      {
        onSuccess: () => {
          queryClient.invalidateQueries({ queryKey: listWorldsOptions().queryKey });
        },
      },
    );
  };

  const handleContinue = (worldId: string) => {
    startSession.mutate(
      { body: { worldId } },
      {
        onSuccess: (data) => {
          navigate({ to: '/session/$sessionId', params: { sessionId: data.sessionId } });
        },
      },
    );
  };

  return (
    <div className="flex h-screen flex-col items-center justify-center gap-8">
      <div className="flex flex-col items-center gap-2">
        <h1 className="font-heading text-primary text-5xl tracking-widest">TRPG</h1>
        <div className="h-px w-32 bg-[linear-gradient(to_right,transparent,var(--primary),transparent)]" />
      </div>

      <div className="border-border bg-card flex w-full max-w-xl flex-col gap-3 rounded-xl border p-4 shadow-[0_8px_30px_-8px_color-mix(in_oklch,var(--foreground)_20%,transparent)]">
        <div className="flex items-center justify-between">
          <h2 className="font-heading text-muted-foreground text-sm tracking-wide">Worlds</h2>
          <NewWorldDialog />
        </div>

        {worldsQuery.isLoading && (
          <p className="text-muted-foreground text-sm">Loading worlds...</p>
        )}
        {!worldsQuery.isLoading && worlds.length === 0 && (
          <p className="text-muted-foreground text-sm">You haven't created a world yet.</p>
        )}

        <ul className="flex flex-col gap-2">
          {worlds.map((world) => (
            <li
              key={world.worldId}
              className="border-border bg-secondary/40 flex items-center justify-between gap-2 rounded-md border p-2"
            >
              <span className="font-heading tracking-wide">{world.name}</span>
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

      <p className="text-muted-foreground/60 text-xs">
        Game icons by{' '}
        <a className="underline" href="https://game-icons.net" target="_blank" rel="noreferrer">
          game-icons.net
        </a>{' '}
        (CC BY 3.0)
      </p>
    </div>
  );
}

export default TitleScreen;
