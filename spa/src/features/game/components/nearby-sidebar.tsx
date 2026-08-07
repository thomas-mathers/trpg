import { Sidebar, SidebarContent, useSidebar } from '@/components/ui/sidebar';
import { NearbyPanel } from '@/features/game/components/nearby-panel';
import { useScene } from '@/features/game/contexts/scene-context';
import { cn } from '@/lib/utils';

export function NearbySidebar() {
  const scene = useScene();
  const { open, isMobile } = useSidebar();

  const panel = scene && <NearbyPanel scene={scene} />;

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
