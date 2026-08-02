import { NearbyPanel } from '@/components/NearbyPanel';
import { Sidebar, SidebarContent, useSidebar } from '@/components/ui/sidebar';
import { useSceneQuery } from '@/hooks/useSceneQuery';
import { cn } from '@/lib/utils';

interface NearbySidebarProps {
  sessionId: string;
  turnCount: number;
}

export function NearbySidebar({ sessionId, turnCount }: NearbySidebarProps) {
  const query = useSceneQuery(sessionId, turnCount);
  const { open, isMobile } = useSidebar();

  const panel = query.data && <NearbyPanel sessionId={sessionId} scene={query.data} />;

  // Sidebar's desktop rendering path always reserves layout space for the docked panel via a
  // sibling gap-spacer, with no way to reach that specific element through the component's own
  // className prop. Since we want the panel to float over the content rather than push it, the
  // Sheet-based mobile branch (which already behaves correctly) is reused as-is, but desktop gets
  // its own minimal fixed-position panel instead of going through Sidebar's docked machinery.
  if (isMobile) {
    return (
      <Sidebar side="right">
        <SidebarContent>{panel}</SidebarContent>
      </Sidebar>
    );
  }

  return (
    <div
      className={cn(
        'bg-sidebar text-sidebar-foreground fixed inset-y-0 right-0 z-10 flex w-(--sidebar-width) flex-col shadow-lg transition-transform duration-200 ease-linear',
        open ? 'translate-x-0' : 'translate-x-full',
      )}
    >
      <SidebarContent>{panel}</SidebarContent>
    </div>
  );
}
