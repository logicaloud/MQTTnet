using System;
using System.Collections.Generic;
using System.Text;

namespace MQTTnet
{
    /// <summary>
    /// Implements 64-bit tick count. For NET version earlier then NET 5, the 64-bit count
    /// is calculated by accumulating a count in regular intervals. For NET 5 and later
    /// the built-in Environment.TickCount64 is used.
    /// </summary>
    public class TimeTickSource : IDisposable
    {

#if !NET5_0_OR_GREATER
        // Derive a 64-bit environment tick from 32-bit
        long _accumulated64BitTick;
        int _lastAccumulationTickCount;
        // System.Threading.Timer is available in netstandard 1.3
        System.Threading.Timer _accumulationTimer;
        object _accumulationLock = new object();

        public TimeTickSource()
        {
            // Setup timer to accumulate a 64-bit tick
            const int AccumulationIntervalMs = int.MaxValue / 2;
            _lastAccumulationTickCount = Environment.TickCount;
            _accumulated64BitTick = _lastAccumulationTickCount;
            _accumulationTimer = new System.Threading.Timer(_accumulationTimer_Elapsed, null, AccumulationIntervalMs, AccumulationIntervalMs);
        }

        private void _accumulationTimer_Elapsed(object state)
        {
            lock (_accumulationLock)
            {
                var tickNow = Environment.TickCount;
                _accumulated64BitTick += tickNow - _lastAccumulationTickCount;
                _lastAccumulationTickCount = tickNow;
            }
        }
#endif

        public void Dispose()
        {
#if !NET5_0_OR_GREATER
            // derive a 64-bit environment tick from 32-bit
            _accumulationTimer.Dispose();
#endif
        }

        public long Now
        {
            get
            {
#if NET5_0_OR_GREATER
                return Environment.TickCount64;
#else
                lock (_accumulationLock)
                {
                    return _accumulated64BitTick + (Environment.TickCount - _lastAccumulationTickCount);
                }
#endif
            }
        }
    }
}
