import { MenuIcon } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenu,
} from '@/components/ui/dropdown-menu';

interface GameMenuProps {
  hasSceneData: boolean;
  onOpenEquipment: () => void;
  onOpenAbilities: () => void;
  onExitToMenu: () => void;
}

export function GameMenu({
  hasSceneData,
  onOpenEquipment,
  onOpenAbilities,
  onExitToMenu,
}: GameMenuProps) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" className="ml-auto">
          <MenuIcon />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem disabled>Character</DropdownMenuItem>
        <DropdownMenuItem disabled={!hasSceneData} onClick={onOpenEquipment}>
          Inventory
        </DropdownMenuItem>
        <DropdownMenuItem disabled={!hasSceneData} onClick={onOpenAbilities}>
          Abilities
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={onExitToMenu}>Exit to Main Menu</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
