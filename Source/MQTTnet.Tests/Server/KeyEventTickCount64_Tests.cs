using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace MQTTnet.Tests.Server
{
    [TestClass]
    public sealed class KeyEvent_Tests : BaseTestClass
    {

        [TestMethod]
        public async Task Test_TickCount_Progress()
        {
            var k = new TimeTickSource();
            var startTick = k.Now;
            await Task.Delay(500);
            var stopTick = k.Now;
            Assert.IsTrue(stopTick > startTick, "stopTick should be greater than startTick");
            // Difference should be approximately 500
            Assert.IsTrue(Math.Abs(stopTick - startTick - 500) < 100, "startTick/stopTick difference outside expected range: " + (stopTick - startTick));
        }

        [TestMethod]
        public async Task Test_KeyEventSchedule_Add()
        {
            CancellationTokenSource waitCancelSource = new CancellationTokenSource();
            var eventProcessed = false;
            long processedTick = 0;
            var tickSource = new TimeTickSource();
            var k = new MQTTnet.KeyEventSchedule<string, object>(
                (string key, object args, CancellationToken cancellationToken) =>
                {
                    processedTick = tickSource.Now;
                    eventProcessed = true;
                    Assert.AreEqual(key, "event1");
                    waitCancelSource.Cancel();
                    return MQTTnet.Internal.CompletedTask.Instance;

                },
                CancellationToken.None
            );

            var startTick = tickSource.Now;
            k.AddOrUpdateEvent(1, "event1", null); // interval is in seconds
            // Event should be processed in one second
            try
            {
                await Task.Delay(1250, waitCancelSource.Token);
            }
            catch
            {
            }
            Assert.IsTrue(eventProcessed, "Event not processed");
            var delta = processedTick - startTick;
            Console.WriteLine("KeyEvent processed after [ms]: " + delta);
            Assert.IsTrue(Math.Abs(delta - 1000) < 100, "Processing occured outside expected time frame: " + delta);
        }

        [TestMethod]
        public async Task Test_KeyEventSchedule_AddThenRemove()
        {
            var eventProcessed = false;
            var tickSource = new MQTTnet.TimeTickSource();
            var k = new MQTTnet.KeyEventSchedule<string, object>(
                (string key, object args, CancellationToken cancellationToken) =>
                {
                    eventProcessed = true;
                    return MQTTnet.Internal.CompletedTask.Instance;
                },
                CancellationToken.None
            );

            k.AddOrUpdateEvent(1, "event1", null); // interval is in seconds
            k.RemoveEvent("event1"); // interval is in seconds
            // Event should NOT be processed in one second
            await Task.Delay(1250);
            Assert.IsFalse(eventProcessed, "Removed event is processed");
        }

        [TestMethod]
        public async Task Test_KeyEventSchedule_StressTest()
        {
            const int NumEventsToSchedule = 1000;

            CancellationTokenSource waitCancelSource = new CancellationTokenSource();
            var eventsProcessedCount = 0;
            long processedTick = 0;
            var tickSource = new MQTTnet.TimeTickSource();
            var k = new MQTTnet.KeyEventSchedule<string, string>(
                (string key, string args, CancellationToken cancellationToken) =>
                {
                    processedTick = tickSource.Now;
                    eventsProcessedCount++;
                    if (eventsProcessedCount == NumEventsToSchedule)
                    {
                        waitCancelSource.Cancel();
                    }
                    return MQTTnet.Internal.CompletedTask.Instance;
                },
                CancellationToken.None
            );

            var startTick = tickSource.Now;
            var tasks = new List<Task>();
            for(var i=0; i< NumEventsToSchedule; i++)
            {
                var eventId = i;
                tasks.Add(Task.Run(() =>
                {
                    k.AddOrUpdateEvent(1, "event" + eventId, null); // interval is in seconds
                }));
            }

            Task.WhenAll(tasks).Wait();

            // Events should be processed in one second
            try
            {
                await Task.Delay(1500, waitCancelSource.Token);
            }
            catch
            {
            }

            Assert.AreEqual(1000, eventsProcessedCount, "Event not processed");
        }
    }
}
