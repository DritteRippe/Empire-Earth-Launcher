using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// Runs asynchronous work of the launcher on the calling thread, the way the UI thread does, without a message loop: what
    /// the work posts to its synchronization context (the continuation after each <c>await</c>, so the <c>Changed</c> events the
    /// pages listen to) runs here, never on a thread pool thread that would fill a page from the wrong thread.
    /// </summary>
    internal static class UiThread
    {
        private sealed class QueueContext : SynchronizationContext
        {
            private readonly BlockingCollection<Tuple<SendOrPostCallback, object>> queue =
                new BlockingCollection<Tuple<SendOrPostCallback, object>>();

            public override void Post(SendOrPostCallback d, object state)
            {
                queue.Add(Tuple.Create(d, state));
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                d(state);
            }

            /// <summary>Runs the posted callbacks until <paramref name="task"/> has finished.</summary>
            public void RunUntilCompleted(Task task)
            {
                // The task completes on this thread or on a pool thread; wake up regularly so that neither is missed.
                while (!task.IsCompleted)
                {
                    Tuple<SendOrPostCallback, object> item;
                    if (queue.TryTake(out item, 20))
                        item.Item1(item.Item2);
                }
                Tuple<SendOrPostCallback, object> rest;
                while (queue.TryTake(out rest))
                    rest.Item1(rest.Item2);
            }
        }

        /// <summary>Runs <paramref name="work"/> to its end on this thread and rethrows its exception.</summary>
        public static void Run(Func<Task> work)
        {
            SynchronizationContext previous = SynchronizationContext.Current;
            var context = new QueueContext();
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                Task task = work();
                context.RunUntilCompleted(task);
                task.GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }
    }
}
