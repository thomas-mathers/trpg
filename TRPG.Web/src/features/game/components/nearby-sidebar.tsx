import { Sidebar, SidebarContent, useSidebar } from '@/components/ui/sidebar';
import { NearbyPanel } from '@/features/game/components/nearby-panel';
import { useSceneQuery } from '@/features/game/hooks/use-scene-query';
import { cn } from '@/lib/utils';

interface NearbySidebarProps {
  sessionId: string;
}

export function NearbySidebar({ sessionId }: NearbySidebarProps) {
  const query = useSceneQuery(sessionId);
  const { open, isMobile } = useSidebar();

  const panel = query.data && <NearbyPanel sessionId={sessionId} scene={query.data} />;

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
