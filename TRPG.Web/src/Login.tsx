import { useMutation } from '@tanstack/react-query';

import { postSessionsMutation } from './api/client';
import { Button } from './components/ui/button';
import { useGameHubConnection } from './hooks/useGameHubConnection';

function Login() {
  const mutation = useMutation(postSessionsMutation());

  const login = (worldId: string) => {
    mutation.mutate(
      {
        query: { worldId },
      },
      {
        onSuccess(data, variables, onMutateResult, context) {
          const sessionId = data.sessionId;
        },
      },
    );
  };

  return (
    <div className="flex h-screen flex-col">
      <Button onClick={() => login('7d441547-5a62-4b39-85b2-4ef69835229a')}>Login</Button>
    </div>
  );
}

export default Login;
