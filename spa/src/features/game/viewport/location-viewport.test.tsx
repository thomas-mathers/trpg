import { screen } from '@testing-library/react';
import { expect, it, vi } from 'vitest';

import { createSceneSnapshot, SceneContext } from '@/features/game/contexts/scene-context';
import { renderWithProviders } from '@/test/test-utils';

import { LocationViewport } from './location-viewport';

vi.mock('@react-three/fiber', () => ({
  Canvas: ({ shadows }: { shadows?: boolean }) => (
    <div data-testid="viewport-canvas" data-shadows={shadows} />
  ),
}));
vi.mock('@/features/encounters/hooks/use-has-active-encounter', () => ({
  useHasActiveEncounter: () => false,
}));
vi.mock('../hooks/use-game-hub-connection', () => ({ useChatHub: () => ({}) }));
vi.mock('./use-seat-interaction', () => ({ useSeatInteraction: () => () => {} }));

it('enables shadow mapping for an outdoor district', () => {
  const scene = createSceneSnapshot();
  scene.size = { width: 100, depth: 100 };
  renderWithProviders(
    <SceneContext.Provider value={{ scene }}>
      <LocationViewport onQuestDialogRequested={() => {}} onDeliverItemDialogRequested={() => {}} />
    </SceneContext.Provider>,
  );

  expect(screen.getByTestId('viewport-canvas')).toHaveAttribute('data-shadows', 'true');
});
