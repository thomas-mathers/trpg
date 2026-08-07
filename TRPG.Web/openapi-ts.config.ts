import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
  input: '../TRPG/obj/TRPG.json',
  output: './src/api/client',
  plugins: [
    '@hey-api/typescript',
    '@hey-api/sdk',
    '@hey-api/client-fetch',
    'msw',
    {
      name: '@tanstack/react-query',
      exportFromIndex: true,
    },
  ],
});
