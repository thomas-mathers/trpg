import type { Preview } from '@storybook/react-vite';
import { mswLoader } from 'msw-storybook-addon/csf3';

import '../src/index.css';

const preview: Preview = {
  loaders: [mswLoader()],
  parameters: {
    layout: 'centered',
    controls: { disable: true },
  },
};

export default preview;
