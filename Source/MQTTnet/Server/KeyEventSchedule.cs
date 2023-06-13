using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MQTTnet
{
    /// <summary>
    /// Implements a timer-like scheduler task that fires events when due.
    /// Events are identified by key and only one event per key can exist.
    /// Adding an event for an existing key will replace the event.
    /// TEventKey must have built-in comparability.
    /// </summary>
    public class KeyEventSchedule<TEventKey, TEventArgs> : IDisposable
    {
        public delegate Task ProcessEventAsyncDelegate(TEventKey key, TEventArgs args, CancellationToken cancellationToken);

        object _lock;
        SortedSet<KeyEvent<TEventKey, TEventArgs>> _eventQueue;
        Dictionary<TEventKey, KeyEvent<TEventKey, TEventArgs>> _eventDictionary;
        TimeTickSource _timeTicks;
        int _sequenceCounter;
        long _lastEventTimeTick;
        ProcessEventAsyncDelegate _onProcessEvent;
        CancellationTokenSource _cancelWaitTokenSource;
        volatile bool _disposed;

        public KeyEventSchedule(ProcessEventAsyncDelegate onProcessEvent, CancellationToken cancellationToken)
        {
            _timeTicks = new TimeTickSource();
            _onProcessEvent = onProcessEvent;
            _lock = new object();
            _cancelWaitTokenSource = new CancellationTokenSource();
            _eventQueue = new SortedSet<KeyEvent<TEventKey, TEventArgs>>();
            _eventDictionary = new Dictionary<TEventKey, KeyEvent<TEventKey, TEventArgs>>();
            Task.Run(() => Run(cancellationToken).ConfigureAwait(false));
        }

        public void Clear()
        {
            lock (_lock)
            {
                _eventQueue.Clear();
                _eventDictionary.Clear();
            }
        }

        /// <summary>
        /// Schedule event that fires intervalInSeconds from now.
        /// </summary>
        /// <param name="intervalInSeconds">Number of seconds</param>
        /// <param name="key">Unique event identifier</param>
        /// <param name="args">Arguments for callback the event fires</param>
        /// <returns></returns>
        public void AddOrUpdateEvent(uint intervalInSeconds, TEventKey key, TEventArgs args)
        {
            var eventTimeTick = _timeTicks.Now + intervalInSeconds * 1000;
            var nextSeqNo = 0;
            KeyEvent<TEventKey, TEventArgs> keyEvent;
            lock (_lock)
            {
                if (_lastEventTimeTick != eventTimeTick)
                {
                    // Can reset sequence counter while still retaining original sequence
                    // of events even when there have been multiple events with the same timestamp.
                    _lastEventTimeTick = eventTimeTick;
                    _sequenceCounter = 0;
                }
                else
                {
                    nextSeqNo = ++_sequenceCounter;
                }

                if (_eventDictionary.TryGetValue(key, out var existingEvent))
                {
                    // replace event; remove then add
                    _eventQueue.Remove(existingEvent);
                    _eventDictionary.Remove(key);
                }

                keyEvent = new KeyEvent<TEventKey, TEventArgs>(eventTimeTick, nextSeqNo, key, args);
                long prevFirstEventTimeTick = Int64.MaxValue;
                if (_eventQueue.Count > 0)
                {
                    prevFirstEventTimeTick = _eventQueue.Min.EventTimeTick;
                }
                _eventQueue.Add(keyEvent); 
                _eventDictionary.Add(key, keyEvent);

                // Wake up task if this was the first event queued or if the first event time has changed,
                // otherwise cancellation can be avoided since the task is already waiting for the correct time.
                if (prevFirstEventTimeTick != _eventQueue.Min.EventTimeTick)
                {
                    _cancelWaitTokenSource.Cancel();
                }
            }
        }

        public void RemoveEvent(TEventKey key)
        {
            lock (_lock)
            {
                if (_eventDictionary.TryGetValue(key, out var timedEvent))
                {
                    _eventQueue.Remove(timedEvent);
                    _eventDictionary.Remove(key);
                }
                // no need to wake up task
            }
        }

        /// <summary>
        /// Loop and process events when due until cancelled
        /// </summary>
        async Task Run(CancellationToken cancellationToken)
        {
            try
            {
                while ((!cancellationToken.IsCancellationRequested) && (!_disposed))
                {
                    // Get next event if any

                    KeyEvent<TEventKey, TEventArgs> nextEvent = null;
                    bool isDue = false;
                    lock (_lock)
                    {
                        if (_eventQueue.Count > 0)
                        {
                            nextEvent = _eventQueue.Min;
                            isDue = nextEvent.EventTimeTick <= _timeTicks.Now;
                            if (isDue)
                            {
                                _eventQueue.Remove(nextEvent);
                                _eventDictionary.Remove(nextEvent.Key);
                            }
                        }
                    }

                    // Process event or wait
                    if (isDue)
                    {
                        try
                        {
                            await _onProcessEvent(nextEvent.Key, nextEvent.EventArgs, cancellationToken);
                        }
                        catch // (Exception ex)
                        {
                            // event processing has caused an exception
                        }
                    }
                    else
                    {
                        try
                        {
                            if (nextEvent != null)
                            {
                                var delay = nextEvent.EventTimeTick - _timeTicks.Now;
                                if (delay >= int.MaxValue)
                                {
                                    // delay is longer than can be waited for with Task Delay; limit wait time.
                                    delay = int.MaxValue/2;
                                }
                                await Task.Delay((int)delay, _cancelWaitTokenSource.Token).ConfigureAwait(false);
                            }
                            else
                            {
                                // no events in the queue; wait indefinetly until wait is cancelled
                                await Task.Delay(Timeout.Infinite, _cancelWaitTokenSource.Token).ConfigureAwait(false);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            lock (_lock)
                            {
                                // create new wait token
                                _cancelWaitTokenSource.Dispose();
                                _cancelWaitTokenSource = new CancellationTokenSource();
                            }
                        }

                    }
                }
            }
            finally
            {
                _cancelWaitTokenSource.Dispose();
            }
        }

        public void Dispose()
        {
            _disposed = true;

            lock (_lock)
            {
                _cancelWaitTokenSource.Cancel();
            }
        }
    }
}
