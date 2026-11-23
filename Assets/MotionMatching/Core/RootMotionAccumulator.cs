namespace MotionMatching.Core
{
    /// <summary>Collects horizontal metres for one rendered frame, then consumes them once.</summary>
    public sealed class RootMotionAccumulator
    {
        private int _frame = -1;
        private float _x, _z;

        public void Submit(int frame, float xMeters, float zMeters)
        {
            if (_frame != frame)
            {
                Clear();
                _frame = frame;
            }
            _x += xMeters;
            _z += zMeters;
        }

        public void Consume(int frame, out float xMeters, out float zMeters)
        {
            xMeters = _frame == frame ? _x : 0f;
            zMeters = _frame == frame ? _z : 0f;
            Clear();
        }

        public void Clear()
        {
            _frame = -1;
            _x = _z = 0f;
        }
    }
}
