using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace SigmaTau.Unity.NeovimCodeEditor.Editor
{
    public static class TaskHelper
    {
        public static T ExecuteCancelable<T>(Func<Action<string>, CancellationToken, T> execute)
        {
            Task<T> task = null;
            CancellationTokenSource cts = null;
            string lastInfo = null;
            string currentInfo = null;
            bool showingModal = false;

            try
            {
                cts = new CancellationTokenSource();
                task = Task.Run(
                    () => execute((info) => Volatile.Write(ref currentInfo, info), cts.Token),
                    cts.Token);
                task.Wait(TimeSpan.FromSeconds(1.0));
                while (!task.IsCompleted)
                {
                    string newInfo = Volatile.Read(ref currentInfo);
                    if (showingModal && lastInfo != newInfo)
                    {
                        lastInfo = newInfo;
                        EditorUtility.ClearProgressBar();
                    }
                    showingModal = true;
                    bool cancelTask = EditorUtility.DisplayCancelableProgressBar("Neovide Code Editor", newInfo, -1);
                    if (cancelTask)
                    {
                        EditorUtility.ClearProgressBar();
                        EditorUtility.DisplayCancelableProgressBar("NeovimCodeEditor", "Cancelling...", -1f);
                        cts.Cancel();
                        task.Wait();
                        break;
                    }
                    task.Wait(TimeSpan.FromMilliseconds(50.0));
                }

                return task.Result;
            }
            catch when (task?.IsCanceled == true)
            {
                throw new OperationCanceledException();
            }
            catch (AggregateException exc)
            {
                exc = exc.Flatten();
                if (exc.InnerExceptions.Count == 1)
                {
                    throw exc.InnerException;
                }
                throw;
            }
            finally
            {
                cts?.Dispose();
                task?.Dispose();
                if (showingModal)
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }
    }
}
