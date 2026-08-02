import { Compass } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { useSidebar } from '@/components/ui/sidebar';

export function NearbyToggleButton() {
  const { toggleSidebar } = useSidebar();

  return (
    <Button variant="ghost" size="icon" aria-label="What's nearby" onClick={toggleSidebar}>
      <Compass />
    </Button>
  );
}
