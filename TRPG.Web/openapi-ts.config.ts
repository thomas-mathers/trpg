import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
  input: 'http://localhost:5000/openapi/v1.json',
  output: './src/api/client',
  plugins: [
    '@hey-api/client-fetch',
    {
      name: '@tanstack/react-query',
      exportFromIndex: true,
    },
  ],
});
