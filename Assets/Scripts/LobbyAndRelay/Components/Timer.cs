using System;
using Unity.Mathematics;

namespace LobbyAndRelay.Components
{
    [Serializable]
    public struct Timer
    {
        public Timer(float totalTime, bool isRepeating = false)
        {
            _isActive = true;
            _timeLeft = totalTime;
            _totalTime = totalTime;
            _justFinished = false;
            _isRepeating = isRepeating;
        }

        private bool _isActive;
        private float _totalTime;
        private float _timeLeft;
        private bool _justFinished;
        private bool _isRepeating;

        public bool JustFinished => _justFinished;
        public float TotalTime => _totalTime;
        public float TimeLeft => _timeLeft;
        public bool IsActive => _isActive;

        public void Reset()
        {
            _isActive = true;
            _timeLeft = _totalTime;
            _justFinished = false;
        }

        public void Update(float deltaTime)
        {
            _justFinished = false;
            if (!_isActive) return;
            
            _timeLeft -= deltaTime;
            _timeLeft = math.max(0f, _timeLeft);
            if (_timeLeft > 0) return;
            
            _justFinished = true;
            _isActive = _isRepeating;
            if (_isRepeating)
            {
                _timeLeft = _totalTime;
            }
        }
    }
}