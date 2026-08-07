import { QueryClient } from '@tanstack/react-query';
import { render, type RenderOptions } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactElement } from 'react';

import { TestProviders } from './test-providers';

export function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: Infinity,
      },
      mutations: {
        retry: false,
      },
    },
  });
}

interface RenderWithProvidersOptions extends Omit<RenderOptions, 'wrapper'> {
  playerId?: string;
  queryClient?: QueryClient;
}

export function renderWithProviders(
  ui: ReactElement,
  { playerId, queryClient = createTestQueryClient(), ...options }: RenderWithProvidersOptions = {},
) {
  const user = userEvent.setup();
  const renderResult = render(ui, {
    wrapper: ({ children }) => (
      <TestProviders playerId={playerId} queryClient={queryClient}>
        {children}
      </TestProviders>
    ),
    ...options,
  });

  return { user, ...renderResult };
}
