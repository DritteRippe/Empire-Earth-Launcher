using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Logging;

namespace Empire_Earth_Launcher
{
    /// <summary>How an operation started through <see cref="UiOperation"/> ended.</summary>
    internal enum UiOperationOutcome
    {
        /// <summary>The work finished.</summary>
        Completed,
        /// <summary>The work was canceled (<see cref="OperationCanceledException"/>, e.g. its page was closed).</summary>
        Canceled,
        /// <summary>The work threw; the error was logged and shown.</summary>
        Failed,
        /// <summary>Not started: the same trigger's operation was still running.</summary>
        Ignored
    }

    /// <summary>
    /// Runs the asynchronous work of a UI event handler (ADR 0004): the triggering control is disabled while the
    /// work runs, every exception is logged and shown as a localized "unexpected error" (the UI boundary of
    /// ADR 0013), a cancellation is normal, and a second start from the same trigger while it runs is ignored.
    /// Afterwards the trigger is enabled again and the page applies its own state once more (<c>restore</c>), so a
    /// button that the page disabled meanwhile (nothing left to delete, a setup started) stays disabled.
    /// </summary>
    /// <remarks>
    /// <see cref="Run"/> is the only <c>async void</c> method of the launcher: event handlers call it instead of being
    /// <c>async void</c> themselves. The composition root (<see cref="Program"/>) creates one instance and passes it to
    /// the pages that start asynchronous work (the first one is the installation discovery). Use it on the UI thread
    /// only; the work resumes on the UI thread after its awaits (WinForms synchronization context), so it may update
    /// controls.
    /// </remarks>
    internal sealed class UiOperation
    {
        private readonly ILogger logger;
        private readonly HashSet<object> runningTriggers = new HashSet<object>();

        public UiOperation(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Starts <paramref name="work"/> for a click (or another event) of <paramref name="trigger"/>.</summary>
        /// <param name="trigger">The control that started the work; disabled while it runs.</param>
        /// <param name="work">The work.</param>
        /// <param name="restore">
        /// The state logic of the page (for example its ShowState), applied after the trigger was enabled again, so that the
        /// page decides whether the trigger stays enabled; <see langword="null"/> when the trigger is always usable.
        /// </param>
        /// <example><code>startButton.Click += (s, e) => uiOperation.Run(startButton, () => StartAsync(token), ShowState);</code></example>
        public async void Run(Control trigger, Func<Task> work, Action restore = null)
        {
            if (trigger == null)
                throw new ArgumentNullException(nameof(trigger));
            if (work == null)
                throw new ArgumentNullException(nameof(work));

            // RunAsync handles every exception of the work; what could still escape (a failing Enabled setter) ends in
            // Application.ThreadException like any other bug on the UI thread.
            await RunAsync(trigger, trigger.Name,
                enabled =>
                {
                    if (!trigger.IsDisposed)
                        trigger.Enabled = enabled;
                },
                work,
                exception => UnexpectedError.Show(trigger.IsDisposed ? null : trigger.FindForm(), exception, false),
                restore == null ? (Action)null : () =>
                {
                    if (!trigger.IsDisposed)
                        restore();
                });
        }

        /// <summary>The logic of <see cref="Run"/> without WinForms types, for the tests.</summary>
        /// <param name="trigger">Identity of the trigger: one operation per trigger at a time.</param>
        /// <param name="name">Name of the trigger, for the log.</param>
        /// <param name="setEnabled">Enables or disables the trigger.</param>
        /// <param name="work">The work; it may also throw before it returns a task.</param>
        /// <param name="report">Shows an error to the user.</param>
        /// <param name="restore">Applies the state of the page after the trigger was enabled again; may be <see langword="null"/>.</param>
        internal Task<UiOperationOutcome> RunAsync(object trigger, string name, Action<bool> setEnabled,
            Func<Task> work, Action<Exception> report, Action restore = null)
        {
            // Checked here, outside the async method, so that a programming error throws at once.
            if (trigger == null)
                throw new ArgumentNullException(nameof(trigger));
            if (setEnabled == null)
                throw new ArgumentNullException(nameof(setEnabled));
            if (work == null)
                throw new ArgumentNullException(nameof(work));
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            return RunCheckedAsync(trigger, name, setEnabled, work, report, restore);
        }

        private async Task<UiOperationOutcome> RunCheckedAsync(object trigger, string name, Action<bool> setEnabled,
            Func<Task> work, Action<Exception> report, Action restore)
        {
            if (!runningTriggers.Add(trigger))
                return UiOperationOutcome.Ignored;

            setEnabled(false);
            try
            {
                await work();
                return UiOperationOutcome.Completed;
            }
            catch (OperationCanceledException ex)
            {
                logger.Info("The operation of " + name + " was canceled (" + ex.Message + ").");
                return UiOperationOutcome.Canceled;
            }
            catch (Exception ex)
            {
                // The UI boundary of ADR 0013: environment problems are results of the core, so whatever arrives here
                // is unexpected. It is logged and shown, and the launcher continues.
                logger.Error("The operation of " + name + " failed unexpectedly.", ex);
                try
                {
                    report(ex);
                }
                catch (Exception reportException)
                {
                    logger.Error("Unable to show the error of the operation of " + name + ".", reportException);
                }
                return UiOperationOutcome.Failed;
            }
            finally
            {
                runningTriggers.Remove(trigger);
                setEnabled(true);
                // Last: the page decides about its controls (the work may have changed what is possible), not this helper.
                restore?.Invoke();
            }
        }
    }
}
