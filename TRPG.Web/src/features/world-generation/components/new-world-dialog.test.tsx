import {
  Outlet,
  RouterProvider,
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
} from '@tanstack/react-router';
import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { byRole } from 'testing-library-selector';
import { describe, expect, it, vi } from 'vitest';

import {
  handleCreateSession,
  handleCreateWorld,
  handleGetCreatureGenerationOptions,
  handleGetJob,
} from '@/api/client/msw.gen';
import { server } from '@/test/server';
import { renderWithProviders } from '@/test/test-utils';

import { NewWorldDialog } from './new-world-dialog';

const generationOptions = {
  pointsPerLevel: 3,
  baseAttributes: {
    strength: 10,
    defense: 5,
    dexterity: 10,
    endurance: 10,
    stamina: 10,
    mana: 10,
    intelligence: 10,
  },
};

function mockGenerationOptions() {
  server.use(handleGetCreatureGenerationOptions({ body: generationOptions }));
}

const ui = {
  newWorldButton: byRole('button', { name: 'New World' }),
  dialog: byRole('dialog', { name: 'New World' }),
  name: byRole('textbox', { name: 'Name' }),
  createWorld: byRole('button', { name: 'Create World' }),
  back: byRole('button', { name: 'Back' }),
};

function renderDialog() {
  const rootRoute = createRootRoute({ component: Outlet });
  const titleRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/',
    component: NewWorldDialog,
  });
  const sessionRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/session/$sessionId',
    component: () => <div>Session</div>,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([titleRoute, sessionRoute]),
    history: createMemoryHistory({ initialEntries: ['/'] }),
  });
  const result = renderWithProviders(<RouterProvider router={router} />);

  return { router, ...result };
}

describe('NewWorldDialog', () => {
  it('keeps world creation disabled until a character name is entered', async () => {
    mockGenerationOptions();
    const { user } = renderDialog();

    await user.click(await ui.newWorldButton.find());
    expect(await ui.dialog.find()).toBeVisible();

    const createButton = ui.createWorld.get();
    expect(createButton).toBeDisabled();

    await user.type(ui.name.get(), 'Aria');

    expect(createButton).toBeEnabled();
    expect(screen.getByText('Aria the Human Knight')).toBeVisible();
  });

  it('shows the generation error and lets the user return to the form', async () => {
    mockGenerationOptions();
    server.use(handleCreateWorld(() => new HttpResponse(null, { status: 500 })));
    const { user } = renderDialog();

    await user.click(await ui.newWorldButton.find());
    await ui.dialog.find();
    await user.type(ui.name.get(), 'Aria');
    await user.click(ui.createWorld.get());

    expect(await screen.findByText('Failed to start world generation.')).toBeVisible();
    await user.click(ui.back.get());
    expect(ui.name.get()).toBeVisible();
  });

  it('creates a world, starts its session, and navigates to the session', async () => {
    mockGenerationOptions();
    const worldRequest = vi.fn();
    const sessionRequest = vi.fn();
    server.use(
      handleCreateWorld(async ({ request }) => {
        worldRequest(await request.json());
        return HttpResponse.json({ jobId: 'job-1' }, { status: 202 });
      }),
      handleGetJob({
        body: {
          jobId: 'job-1',
          status: 'Done',
          resultJson: JSON.stringify({
            worldId: 'world-1',
            playerId: 'player-1',
            worldName: 'A New World',
          }),
          errorMessage: null,
        },
      }),
      handleCreateSession(async ({ request }) => {
        sessionRequest(await request.json());
        return HttpResponse.json({ sessionId: 'session-1', playerId: 'player-1' });
      }),
    );

    const { user, router } = renderDialog();
    await user.click(await ui.newWorldButton.find());
    await ui.dialog.find();
    await user.type(ui.name.get(), 'Aria');
    await user.click(ui.createWorld.get());

    await waitFor(() => expect(sessionRequest).toHaveBeenCalledWith({ worldId: 'world-1' }));
    expect(worldRequest).toHaveBeenCalledWith(
      expect.objectContaining({
        playerName: 'Aria',
        gender: 'Male',
        age: 30,
        race: 'Human',
        playerClass: 'Knight',
        startingAttributeAllocation: {},
      }),
    );
    await waitFor(() => expect(router.state.location.pathname).toBe('/session/session-1'));
  });
});
