import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
  input: 'https://localhost:5001/openapi/v1.json',
  output: './src/api/client',
  plugins: [
    '@hey-api/typescript',
    '@hey-api/sdk',
    '@hey-api/client-fetch',
    {
      name: '@tanstack/react-query',
      exportFromIndex: true,
    },
  ],
});
