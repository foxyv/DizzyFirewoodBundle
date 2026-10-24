using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    // Finds freezes. Logs any rebuild that takes long, and any frame that does.
    //
    // A watchdog thread also notices when the game stops altogether, and writes down
    // the last thing the game was doing to BepInEx/BundleUp-stall.log. The game marks
    // where it is each frame: before physics, after physics, Update, LateUpdate, and
    // inside this mod's own work. A freeze between "before physics" and "after
    // physics" is in Unity's physics step or a trigger callback.
    internal static class Stall
    {
        private const double SlowMs = 30.0;
        private const float SlowFrame = 0.5f;
        private const double StuckSeconds = 3.0;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly object Lock = new object();
        private static volatile string _phase = "starting";
        private static string _lastBundle = "";
        private static long _beat;
        private static Thread _watchdog;
        private static string _path;

        internal static void Report(string what, Stopwatch watch)
        {
            double ms = watch.Elapsed.TotalMilliseconds;
            if (ms >= SlowMs)
                Plugin.Log.LogWarning("[Stall] " + what + " took " + ms.ToString("0") + " ms.");
        }

        internal static void Frame()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt >= SlowFrame)
                Plugin.Log.LogWarning("[Stall] A frame took " + dt.ToString("0.00") + " s (at " + Time.realtimeSinceStartup.ToString("0") + " s).");
        }

        // Where the game is now. Cheap enough to call often.
        internal static void Mark(string phase)
        {
            _phase = phase;
            Interlocked.Exchange(ref _beat, Clock.ElapsedTicks);
        }

        // The last bundle this mod touched, for the stall report.
        internal static void Bundle(string what)
        {
            lock (Lock)
                _lastBundle = what + " at " + Clock.Elapsed.TotalSeconds.ToString("0.0") + " s";
        }

        internal static void Start(MonoBehaviour host)
        {
            if (_watchdog != null)
                return;
            try
            {
                _path = Path.Combine(BepInEx.Paths.BepInExRootPath, "BundleUp-stall.log");
                File.WriteAllText(_path, "Dizzy Bundle Up stall watchdog, started " + DateTime.Now + "\r\n");
            }
            catch
            {
                _path = null;
            }

            Mark("starting");
            host.StartCoroutine(AfterPhysics());
            _watchdog = new Thread(Watch) { IsBackground = true, Name = "Bundle Up stall watchdog" };
            _watchdog.Start();
        }

        private static IEnumerator AfterPhysics()
        {
            var wait = new WaitForFixedUpdate();
            while (true)
            {
                yield return wait;
                Mark("after physics");
            }
        }

        private static void Watch()
        {
            double reportedAt = 0;
            while (true)
            {
                Thread.Sleep(500);
                long beat = Interlocked.Read(ref _beat);
                double stuck = (Clock.ElapsedTicks - beat) / (double)Stopwatch.Frequency;
                if (stuck < StuckSeconds)
                {
                    if (reportedAt > 0)
                    {
                        Write("Running again after about " + reportedAt.ToString("0") + " s.");
                        reportedAt = 0;
                    }
                    continue;
                }

                // Say so at 3 s, then each time the stop doubles.
                if (reportedAt > 0 && stuck < reportedAt * 2)
                    continue;
                reportedAt = stuck;
                string bundle;
                lock (Lock)
                    bundle = _lastBundle;
                Write("Game stopped for " + stuck.ToString("0") + " s. Last place: " + _phase + ". Last bundle work: " + (bundle.Length > 0 ? bundle : "none") + ".");
            }
        }

        private static void Write(string line)
        {
            string stamped = DateTime.Now.ToString("HH:mm:ss") + " " + line;
            try
            {
                if (_path != null)
                    File.AppendAllText(_path, stamped + "\r\n");
            }
            catch
            {
            }
        }
    }
}
