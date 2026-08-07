import { useNavigate, useParams } from '@tanstack/react-router';
import { useEffect, useState } from 'react';

import { CharacterDialog } from '@/features/character/components/character-dialog';

import { SidebarInset, SidebarProvider } from '../../../components/ui/sidebar';
import { gameEventBus } from '../../../lib/game-event-bus';
import { clearStoredMessages } from '../../../lib/session-storage';
import { InventoryDialog } from '../../inventory/components/inventory-dialog';
import { SkillTreeDialog } from '../../skills/components/skill-tree-dialog';
import { usePlayerId } from '../contexts/scene-context';
import { GameChatContext } from '../game-chat-context';
import { useGameChat } from '../hooks/use-game-chat';
import { useIsInCombat } from '../hooks/use-is-in-combat';
import { SceneProvider } from '../providers/scene-provider';
import { ConnectionLostDialog } from './connection-lost-dialog';
import { GameChat } from './game-chat';
import { GameMenu } from './game-menu';
import { NearbySidebar } from './nearby-sidebar';
import { NearbyToggleButton } from './nearby-toggle-button';
import { StatusBar } from './status-bar';

type OpenDialog = 'character' | 'inventory' | 'skillTree' | 'disconnected' | null;

function GameScreen() {
  const navigate = useNavigate();
  const { sessionId } = useParams({ from: '/session/$sessionId' });
  const gameChat = useGameChat(sessionId);
  const isInCombat = useIsInCombat();
  const [isNearbyOpen, setIsNearbyOpen] = useState(true);
  const [openDialog, setOpenDialog] = useState<OpenDialog>(null);

  useEffect(() => {
    setIsNearbyOpen(!isInCombat);
  }, [isInCombat]);

  useEffect(
    () =>
      gameEventBus.on('ConnectionStatusChanged', (status) => {
        if (status === 'disconnected') {
          setOpenDialog('disconnected');
        }
      }),
    [],
  );

  const handleReturnToMenu = () => {
    clearStoredMessages(sessionId);
    setOpenDialog(null);
    navigate({ to: '/' });
  };

  const handleExitToMenu = async () => {
    clearStoredMessages(sessionId);
    await gameChat.endSession();
    navigate({ to: '/' });
  };

  return (
    <GameChatContext.Provider value={gameChat}>
      <SceneProvider key={sessionId} sessionId={sessionId}>
        <GameScreenContent
          isInCombat={isInCombat}
          isNearbyOpen={isNearbyOpen}
          openDialog={openDialog}
          onNearbyOpenChange={setIsNearbyOpen}
          onOpenDialog={setOpenDialog}
          onQuit={handleExitToMenu}
          onConnectionLostClose={handleReturnToMenu}
        />
      </SceneProvider>
    </GameChatContext.Provider>
  );
}

interface GameScreenContentProps {
  isInCombat: boolean;
  isNearbyOpen: boolean;
  openDialog: OpenDialog;
  onNearbyOpenChange: (open: boolean) => void;
  onOpenDialog: (dialog: OpenDialog) => void;
  onQuit: () => void;
  onConnectionLostClose: () => void;
}

function GameScreenContent({
  isInCombat,
  isNearbyOpen,
  openDialog,
  onNearbyOpenChange,
  onOpenDialog,
  onQuit,
  onConnectionLostClose,
}: GameScreenContentProps) {
  const playerId = usePlayerId();

  return (
    <SidebarProvider
      open={isNearbyOpen}
      onOpenChange={onNearbyOpenChange}
      className="h-screen flex-col"
    >
      <div className="flex items-center gap-4 border-b px-4 py-2">
        <StatusBar isInCombat={isInCombat} />
        {!isInCombat && <NearbyToggleButton />}
        <GameMenu
          onOpenCharacterDialog={() => onOpenDialog('character')}
          onOpenInventoryDialog={() => onOpenDialog('inventory')}
          onOpenSkillTreeDialog={() => onOpenDialog('skillTree')}
          onQuit={onQuit}
        />
      </div>

      <div className="relative flex min-h-0 flex-1 overflow-hidden will-change-transform">
        <SidebarInset>
          <GameChat />
        </SidebarInset>

        <NearbySidebar />
      </div>

      {playerId && (
        <>
          <CharacterDialog open={openDialog === 'character'} onClose={() => onOpenDialog(null)} />
          <InventoryDialog
            playerId={playerId}
            open={openDialog === 'inventory'}
            onClose={() => onOpenDialog(null)}
          />
          <SkillTreeDialog
            playerId={playerId}
            open={openDialog === 'skillTree'}
            onClose={() => onOpenDialog(null)}
          />
        </>
      )}

      <ConnectionLostDialog open={openDialog === 'disconnected'} onClose={onConnectionLostClose} />
    </SidebarProvider>
  );
}

export default GameScreen;
