import type { Meta, StoryObj } from '@storybook/react-vite';

import { DistrictPreview } from './layout-preview/district-preview';

const meta = {
  title: 'Game/Viewport/District Layout',
  component: DistrictPreview,
  parameters: { layout: 'fullscreen' },
} satisfies Meta<typeof DistrictPreview>;

export default meta;
type Story = StoryObj<typeof meta>;
export const Workshop: Story = {};
