using System;

namespace LSOL.Systems
{
    internal sealed class CareerAutosaveScheduler
    {
        private readonly int _debounceDelayMs;
        private readonly int _retryDelayMs;
        private readonly int _periodicCheckpointIntervalMs;

        private bool _saveRequested;
        private int _nextSaveDueAtMs;
        private int _lastSavedAtMs;

        public CareerAutosaveScheduler(
            int debounceDelayMs = 5000,
            int retryDelayMs = 2000,
            int periodicCheckpointIntervalMs = 60000)
        {
            _debounceDelayMs = Math.Max(0, debounceDelayMs);
            _retryDelayMs = Math.Max(250, retryDelayMs);
            _periodicCheckpointIntervalMs = Math.Max(0, periodicCheckpointIntervalMs);
        }

        public bool HasPendingSave
        {
            get { return _saveRequested; }
        }

        public void Reset(int currentGameTimeMs)
        {
            var now = NormalizeTime(currentGameTimeMs);
            _saveRequested = false;
            _nextSaveDueAtMs = 0;
            _lastSavedAtMs = now;
        }

        public void RequestSave(int currentGameTimeMs)
        {
            var dueAt = NormalizeTime(currentGameTimeMs) + _debounceDelayMs;
            _saveRequested = true;
            _nextSaveDueAtMs = _nextSaveDueAtMs <= 0
                ? dueAt
                : Math.Min(_nextSaveDueAtMs, dueAt);
        }

        public bool TryRequestPeriodicCheckpoint(int currentGameTimeMs)
        {
            var now = NormalizeTime(currentGameTimeMs);
            if (_periodicCheckpointIntervalMs <= 0
                || _saveRequested
                || now - _lastSavedAtMs < _periodicCheckpointIntervalMs)
            {
                return false;
            }

            _saveRequested = true;
            _nextSaveDueAtMs = now;
            return true;
        }

        public bool IsSaveDue(int currentGameTimeMs)
        {
            return _saveRequested && NormalizeTime(currentGameTimeMs) >= _nextSaveDueAtMs;
        }

        public void MarkSaved(int currentGameTimeMs)
        {
            var now = NormalizeTime(currentGameTimeMs);
            _saveRequested = false;
            _nextSaveDueAtMs = 0;
            _lastSavedAtMs = now;
        }

        public void MarkSaveFailed(int currentGameTimeMs)
        {
            var now = NormalizeTime(currentGameTimeMs);
            _saveRequested = true;
            _nextSaveDueAtMs = now + _retryDelayMs;
        }

        private static int NormalizeTime(int currentGameTimeMs)
        {
            return Math.Max(0, currentGameTimeMs);
        }
    }
}