import { QueryClientProvider } from '@tanstack/react-query';
import {
  Outlet,
  RouterProvider,
  createRouter,
  createRoute,
  createRootRoute,
} from '@tanstack/react-router';
import { TanStackRouterDevtools } from '@tanstack/react-router-devtools';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import './index.css';
import { queryClient } from '@/lib/query-client';

import { client } from './api/client/client.gen.ts';
import App from './App.tsx';

const baseUrl = import.meta.env.VITE_API_BASE_URL as string;

client.setConfig({
  baseUrl,
});

const rootRoute = createRootRoute({
  component: () => (
    <>
      <Outlet />
      <TanStackRouterDevtools />
    </>
  ),
});

const appRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/',
  component: App,
});

const routeTree = rootRoute.addChildren([appRoute]);

const router = createRouter({ routeTree });

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}

const rootElement = document.getElementById('root')!;

if (!rootElement.innerHTML) {
  const root = createRoot(rootElement);
  root.render(
    <StrictMode>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  );
}
