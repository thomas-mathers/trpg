import { useNavigate, useParams } from '@tanstack/react-router';
import { useEffect, useState } from 'react';

import { CharacterDialog } from '@/features/character/components/character-dialog';

import { SidebarInset, SidebarProvider } from '../../../components/ui/sidebar';
import { clearStoredMessages } from '../../../lib/session-storage';
import { InventoryDialog } from '../../inventory/components/inventory-dialog';
import { SkillTreeDialog } from '../../skills/components/skill-tree-dialog';
import { GameChatContext } from '../game-chat-context';
import { useGameChat } from '../hooks/use-game-chat';
import { useIsInCombat } from '../hooks/use-is-in-combat';
import { usePlayerId } from '../contexts/scene-context';
import { SceneProvider } from '../providers/scene-provider';
import { ConnectionLostDialog } from './connection-lost-dialog';
import { GameChat } from './game-chat';
import { GameMenu } from './game-menu';
import { NearbySidebar } from './nearby-sidebar';
import { NearbyToggleButton } from './nearby-toggle-button';
import { StatusBar } from './status-bar';

function GameScreen() {
  const navigate = useNavigate();
  const { sessionId } = useParams({ from: '/session/$sessionId' });
  const gameChat = useGameChat(sessionId);
  const isInCombat = useIsInCombat();
  const [isNearbyOpen, setIsNearbyOpen] = useState(true);
  const [isCharacterDialogOpen, setIsCharacterDialogOpen] = useState(false);
  const [isInventoryDialogOpen, setIsInventoryDialogOpen] = useState(false);
  const [isSkillTreeDialogOpen, setIsSkillTreeDialogOpen] = useState(false);
  const [isDisconnectedDialogOpen, setIsDisconnectedDialogOpen] = useState(false);

  useEffect(() => {
    setIsNearbyOpen(!isInCombat);
  }, [isInCombat]);

  const handleReturnToMenu = () => {
    clearStoredMessages(sessionId);
    setIsDisconnectedDialogOpen(false);
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
          isCharacterDialogOpen={isCharacterDialogOpen}
          isInventoryDialogOpen={isInventoryDialogOpen}
          isSkillTreeDialogOpen={isSkillTreeDialogOpen}
          isDisconnectedDialogOpen={isDisconnectedDialogOpen}
          onNearbyOpenChange={setIsNearbyOpen}
          onOpenCharacterDialog={() => setIsCharacterDialogOpen(true)}
          onOpenInventoryDialog={() => setIsInventoryDialogOpen(true)}
          onOpenSkillTreeDialog={() => setIsSkillTreeDialogOpen(true)}
          onCloseCharacterDialog={() => setIsCharacterDialogOpen(false)}
          onCloseInventoryDialog={() => setIsInventoryDialogOpen(false)}
          onCloseSkillTreeDialog={() => setIsSkillTreeDialogOpen(false)}
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
  isCharacterDialogOpen: boolean;
  isInventoryDialogOpen: boolean;
  isSkillTreeDialogOpen: boolean;
  isDisconnectedDialogOpen: boolean;
  onNearbyOpenChange: (open: boolean) => void;
  onOpenCharacterDialog: () => void;
  onOpenInventoryDialog: () => void;
  onOpenSkillTreeDialog: () => void;
  onCloseCharacterDialog: () => void;
  onCloseInventoryDialog: () => void;
  onCloseSkillTreeDialog: () => void;
  onQuit: () => void;
  onConnectionLostClose: () => void;
}

function GameScreenContent({
  isInCombat,
  isNearbyOpen,
  isCharacterDialogOpen,
  isInventoryDialogOpen,
  isSkillTreeDialogOpen,
  isDisconnectedDialogOpen,
  onNearbyOpenChange,
  onOpenCharacterDialog,
  onOpenInventoryDialog,
  onOpenSkillTreeDialog,
  onCloseCharacterDialog,
  onCloseInventoryDialog,
  onCloseSkillTreeDialog,
  onQuit,
  onConnectionLostClose,
}: GameScreenContentProps) {
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
            onOpenCharacterDialog={onOpenCharacterDialog}
            onOpenInventoryDialog={onOpenInventoryDialog}
            onOpenSkillTreeDialog={onOpenSkillTreeDialog}
            onQuit={onQuit}
          />
        </div>

        <div className="relative flex min-h-0 flex-1 overflow-hidden will-change-transform">
          <SidebarInset>
            <GameChat />
          </SidebarInset>

          <NearbySidebar />
        </div>

        <GameDialogs
          isCharacterDialogOpen={isCharacterDialogOpen}
          isInventoryDialogOpen={isInventoryDialogOpen}
          isSkillTreeDialogOpen={isSkillTreeDialogOpen}
          onCloseCharacterDialog={onCloseCharacterDialog}
          onCloseInventoryDialog={onCloseInventoryDialog}
          onCloseSkillTreeDialog={onCloseSkillTreeDialog}
        />

        <ConnectionLostDialog open={isDisconnectedDialogOpen} onClose={onConnectionLostClose} />
      </SidebarProvider>
  );
}

interface GameDialogsProps {
  isCharacterDialogOpen: boolean;
  isInventoryDialogOpen: boolean;
  isSkillTreeDialogOpen: boolean;
  onCloseCharacterDialog: () => void;
  onCloseInventoryDialog: () => void;
  onCloseSkillTreeDialog: () => void;
}

function GameDialogs({
  isCharacterDialogOpen,
  isInventoryDialogOpen,
  isSkillTreeDialogOpen,
  onCloseCharacterDialog,
  onCloseInventoryDialog,
  onCloseSkillTreeDialog,
}: GameDialogsProps) {
  const playerId = usePlayerId();

  if (!playerId) {
    return null;
  }

  return (
    <>
      <CharacterDialog open={isCharacterDialogOpen} onClose={onCloseCharacterDialog} />
      <InventoryDialog
        playerId={playerId}
        open={isInventoryDialogOpen}
        onClose={onCloseInventoryDialog}
      />
      <SkillTreeDialog
        playerId={playerId}
        open={isSkillTreeDialogOpen}
        onClose={onCloseSkillTreeDialog}
      />
    </>
  );
}

export default GameScreen;
