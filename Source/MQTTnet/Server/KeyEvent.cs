using System;
using System.Collections.Generic;
using System.Text;

namespace MQTTnet
{
    public class KeyEvent<TKey, TEventArgs> : IComparable
    {
        /// <summary>
        /// Create KeyEvent
        /// </summary>
        /// <param name="eventTime">Time when the event should fire</param>
        /// <param name="sequenceNo">Sequence number to distinguish events with the same event time</param>
        /// <param name="key">Unique event identifier</param>
        /// <param name="args">Event parameters</param>
        public KeyEvent(DateTime eventTime, int sequenceNo, TKey key, TEventArgs args)
        {
            EventTime = eventTime;
            SequenceNo = sequenceNo;

            Key = key;
            EventArgs = args;
        }

        public DateTime EventTime { get; }
        public int SequenceNo { get; }

        public TKey Key { get; }
        public TEventArgs EventArgs { get; }

        public int CompareTo(object obj)
        {
            var other = (KeyEvent<TKey,TEventArgs>)obj;
            if (this.EventTime > other.EventTime)
                return 1;
            if (this.EventTime < other.EventTime)
                return -1;
            if (SequenceNo > other.SequenceNo)
                return 1;
            if (SequenceNo < other.SequenceNo)
                return -1;
            return 0;
        }
    }
}
