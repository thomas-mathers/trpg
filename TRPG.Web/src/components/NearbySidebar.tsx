import { NearbyPanel } from '@/components/NearbyPanel';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { useSceneQuery } from '@/hooks/useSceneQuery';

interface NearbySidebarProps {
  sessionId: string;
  turnCount: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function NearbySidebar({ sessionId, turnCount, open, onOpenChange }: NearbySidebarProps) {
  const query = useSceneQuery(sessionId, turnCount);

  if (!query.data) {
    return null;
  }

  return (
    <>
      <Sheet open={open} onOpenChange={onOpenChange}>
        <SheetContent side="right" className="w-80 gap-0 overflow-y-auto p-0 lg:hidden">
          <SheetHeader>
            <SheetTitle>Nearby</SheetTitle>
          </SheetHeader>
          <NearbyPanel sessionId={sessionId} scene={query.data} />
        </SheetContent>
      </Sheet>

      <aside className="hidden w-80 shrink-0 overflow-y-auto border-l lg:block">
        <NearbyPanel sessionId={sessionId} scene={query.data} />
      </aside>
    </>
  );
}
