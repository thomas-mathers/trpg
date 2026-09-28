# SPA Guide

- `spa` is a React + TypeScript Vite app. Use `pnpm` for all SPA commands.
- Feature code lives under `spa/src/features/<feature>`; shared UI primitives live in `spa/src/components/ui`.
- `spa/src/api/client` is generated. After API endpoint or contract changes, run `pnpm generate-client`; do not hand-edit generated files.
- Prefer generated API/TanStack Query helpers from `@/api/client` over custom fetch wrappers.
- A dialog owns its content and live state in its exported component. Extract a separate content component only when that content is genuinely reused outside the dialog.
- Destructure component props in the parameter list and destructure nested object values when they are read locally; avoid repeated property chains.
- Validate SPA changes with `pnpm run fmt:check`, `pnpm run typecheck`, and `pnpm test`.

---

## Tests

Vitest + React Testing Library. Read this before writing or reviewing any test file in this project.

- Use Vitest, React Testing Library, `user-event`, MSW, and the generated Hey API MSW handlers for frontend tests
- Render components through `spa/src/test/test-utils.tsx`; it provides a fresh `QueryClient`, the shared providers, and an isolated `userEvent` instance per test
- Configure test query clients with retries disabled so failed queries and mutations fail promptly instead of waiting through exponential backoff
- Prefer accessible queries (`getByRole`, `getByLabelText`, and accessible names) over DOM order, CSS selectors, or implementation details
- Add an `aria-label` or other accessible name to a control when that makes it meaningfully testable and improves the component's accessibility; testability is a valid reason to add one
- When multiple controls have the same visible label, give the intended control a distinct accessible name; do not disambiguate with `getAll()[0]` or DOM order
- Prefer real components backed by generated MSW handlers over mocking child components or hooks just to avoid configuring their API requests; reserve mocks for true external boundaries or dependencies outside the behavior being tested
- Assert user-visible behavior and network contracts at the MSW boundary, not internal component state
