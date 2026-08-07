import {
  Outlet,
  createRouter,
  createRoute,
  createRootRoute,
  lazyRouteComponent,
} from '@tanstack/react-router';
import { TanStackRouterDevtools } from '@tanstack/react-router-devtools';

const rootRoute = createRootRoute({
  component: () => (
    <>
      <Outlet />
      <TanStackRouterDevtools />
    </>
  ),
});

const titleRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/',
  component: lazyRouteComponent(() => import('./features/game/components/title-screen.tsx')),
});

const sessionRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/session/$sessionId',
  component: lazyRouteComponent(() => import('./features/game/components/game-screen.tsx')),
});

const routeTree = rootRoute.addChildren([titleRoute, sessionRoute]);

export const router = createRouter({ routeTree });

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}
