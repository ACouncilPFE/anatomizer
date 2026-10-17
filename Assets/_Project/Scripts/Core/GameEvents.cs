using System;

namespace AIGame.Core
{
    public static class GameEvents
    {
        public static Action<GameState> OnGameStateChanged;
        public static Action<float> OnMissionTimerUpdated;
        public static Action<float, float> OnShipHealthChanged;
        public static Action<int, int> OnObjectiveProgressChanged;
        public static Action<bool> OnMissionEnded;

        public static void RaiseGameStateChanged(GameState state) => OnGameStateChanged?.Invoke(state);
        public static void RaiseMissionTimerUpdated(float remainingSeconds) => OnMissionTimerUpdated?.Invoke(remainingSeconds);
        public static void RaiseShipHealthChanged(float current, float max) => OnShipHealthChanged?.Invoke(current, max);
        public static void RaiseObjectiveProgressChanged(int complete, int total) => OnObjectiveProgressChanged?.Invoke(complete, total);
        public static void RaiseMissionEnded(bool success) => OnMissionEnded?.Invoke(success);
    }
}
