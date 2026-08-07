import type { StorybookConfig } from '@storybook/react-vite';

const config: StorybookConfig = {
  stories: ['../src/**/*.stories.@(ts|tsx)'],
  addons: ['msw-storybook-addon'],
  staticDirs: ['./public'],
  framework: '@storybook/react-vite',
};

export default config;
