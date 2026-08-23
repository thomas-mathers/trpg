import { GiCompass } from 'react-icons/gi';

import { useSidebar } from '@/components/ui/sidebar';
import { Toggle } from '@/components/ui/toggle';

export function NearbyToggleButton() {
  const { isMobile, open, openMobile, toggleSidebar } = useSidebar();
  const isOpen = isMobile ? openMobile : open;

  return (
    <Toggle
      size="default"
      className="size-8 p-0"
      pressed={isOpen}
      onPressedChange={toggleSidebar}
      aria-label="What's nearby"
    >
      <GiCompass />
    </Toggle>
  );
}
