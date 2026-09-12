import { useEffect } from 'react';
import { AppState } from 'react-native';
import { useVocabularyStore } from '../store/useVocabularyStore';

const INTERVAL_MS = 60000;

// Keeps the local copy caught up: once the database is loaded, then every
// minute, and whenever the app comes back to the foreground. The foreground
// trigger carries most of the weight — the interval gets throttled or
// suspended outright while the app sits in the background.
export function useAutoSync() {
  const isHydrated = useVocabularyStore((state) => state.isHydrated);
  const resync = useVocabularyStore((state) => state.resync);

  useEffect(() => {
    if (!isHydrated) {
      return undefined;
    }

    resync();
    const interval = setInterval(resync, INTERVAL_MS);
    const subscription = AppState.addEventListener('change', (nextState) => {
      if (nextState === 'active') {
        resync();
      }
    });

    return () => {
      clearInterval(interval);
      subscription.remove();
    };
  }, [isHydrated, resync]);
}
