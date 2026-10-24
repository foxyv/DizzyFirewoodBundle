using System.IO;
using System.Text;
using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    // A self-test of the co-op code on one real bundle, run in one game with no second player.
    // It feeds the bundle packets the simulator tests use, as if another player had sent them,
    // through the same code that applies a real packet (ApplyState and the merge), and checks
    // what the bundle ends as. Needs Sailwind Coop installed; it does not need a session.
    // Off unless Probe Key is set. The bundle is put back as it was and nothing is sent.
    internal static partial class CoopBridge
    {
        private static int _probePassed;
        private static int _probeFailed;

        internal static void ProbeIfAsked()
        {
            KeyCode key = FirewoodBundleConfig.ProbeKey != null ? FirewoodBundleConfig.ProbeKey.Value : KeyCode.None;
            if (key == KeyCode.None || !Input.GetKeyDown(key))
                return;
            if (GameState.inCursorMenu || GameState.sleeping || BoatCamera.on)
                return;

            try
            {
                Probe();
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError("[Probe] The probe threw: " + ex);
            }
        }

        private static ShipItem ProbeTarget()
        {
            GoPointer[] pointers = Object.FindObjectsOfType<GoPointer>();
            for (int i = 0; i < pointers.Length; i++)
            {
                if (pointers[i] == null)
                    continue;
                ShipItem item = FirewoodPieces.AsShip(pointers[i].GetHeldItem());
                if (item == null)
                    item = pointers[i].GetPointedAtItem();
                if (item != null && (FirewoodPieces.IsActive(item) || HookLinePieces.IsHook(item)))
                    return item;
            }
            return null;
        }

        private static void Check(string name, bool ok, string detail = "")
        {
            if (ok)
                _probePassed++;
            else
                _probeFailed++;
            Plugin.Log.LogInfo("[Probe] " + (ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? " (" + detail + ")" : ""));
        }

        private static string Describe(ShipItem item)
        {
            BundleState s;
            if (!TryRead(item, out s))
                return "unreadable";
            return "count " + CountNow(item) + ", amount " + s.Amount + ", stack " + s.Stack + ", health " + s.Health;
        }

        private static void LocalWrite(ShipItem item, int count)
        {
            if (HookLinePieces.IsHook(item))
                HookLinePieces.WriteCount(item, count, HookLinePieces.IsLine(item));
            else
                FirewoodPieces.WriteCount(item, count);
        }

        private static void Forget(ShipItem item)
        {
            Changed.Remove(item);
            Bases.Remove(item);
            SetByCoop.Remove(item);
        }

        // A packet another player might have sent: the bundle as it is with this count.
        private static BundleState StateWithCount(ShipItem item, BundleState basis, int count)
        {
            LocalWrite(item, count);
            BundleState made;
            TryRead(item, out made);
            Forget(item);
            made.BaseCount = basis.Count;
            return made;
        }

        private static BundleState Stands(BundleState state)
        {
            state.BaseCount = CountMerge.Settled;
            return state;
        }

        internal static void Probe()
        {
            if (!_present)
            {
                Plugin.Log.LogWarning("[Probe] Sailwind Coop is not loaded, so there is nothing to test.");
                return;
            }
            ShipItem item = ProbeTarget();
            if (item == null)
            {
                Plugin.Log.LogWarning("[Probe] Hold or look at a bundle first.");
                return;
            }

            _probePassed = 0;
            _probeFailed = 0;
            bool wasChanged = Changed.Contains(item);
            string start = Describe(item);
            int n = CountNow(item);
            Plugin.Log.LogInfo("[Probe] Testing " + item.name + ": " + start + ".");

            BundleState original;
            if (!TryRead(item, out original))
            {
                Check("bundle has an ID", false, "no SaveablePrefab instance ID");
                return;
            }

            // 1. What goes out is what comes in.
            var stream = new MemoryStream();
            BundleWire.Write(new BinaryWriter(stream), original);
            BundleState back;
            bool read = BundleWire.TryRead(new BinaryReader(new MemoryStream(stream.ToArray())), out back);
            Check("packet round trip", read && back.Id == original.Id && back.Amount == original.Amount
                && back.Stack == original.Stack && back.Count == original.Count && back.Health == original.Health);

            // 2. Applying the bundle's own state changes nothing and sends nothing.
            ApplyState(item, Stands(back));
            Check("applying its own state changes nothing", Describe(item) == start, Describe(item));
            Check("applying a packet does not send one", Changed.Contains(item) == wasChanged);

            // 3. Another player's count lands, drawn and all.
            int step = n >= 3 ? -1 : 1;
            BundleKind kind = FirewoodPieces.KindOf(item);
            int limit = kind != null ? kind.Limit : FirewoodBundleConfig.MaxHooks.Value;
            bool room = n + 2 * step >= 1 && n + 2 * step <= limit;
            BundleState other = StateWithCount(item, original, n + step);
            ApplyState(item, Stands(original));
            ApplyState(item, Stands(other));
            Check("another player's count is taken", CountNow(item) == n + step, "wanted " + (n + step) + ", got " + CountNow(item));
            BundleState nowState;
            TryRead(item, out nowState);
            Check("and the item holds what they sent", nowState.Amount == other.Amount && nowState.Stack == other.Stack);
            Check("and nothing is sent back", Changed.Contains(item) == wasChanged);

            // 4. A change made here at the same moment is kept too (J-025).
            if (room)
            {
                ApplyState(item, Stands(original));
                CountWillChange(item);
                LocalWrite(item, n + step);
                ApplyState(item, other);
                Check("a change made here too is added", CountNow(item) == n + 2 * step, "wanted " + (n + 2 * step) + ", got " + CountNow(item));
                Forget(item);
            }
            else
            {
                Plugin.Log.LogInfo("[Probe] SKIP merge test: no room for two changes from a count of " + n + ".");
            }

            // 5. A settled packet from the host stands over a change made here (J-029, J-033).
            ApplyState(item, Stands(original));
            CountWillChange(item);
            LocalWrite(item, n + step);
            ApplyState(item, Stands(original));
            Check("a settled packet stands over a local change", CountNow(item) == n, "wanted " + n + ", got " + CountNow(item));
            Forget(item);

            // 6. A count Coop's own amount packet set is not a change made here (J-032).
            ApplyState(item, Stands(original));
            ApplyState(item, Stands(other));
            SetByCoop[item] = CountNow(item);
            ApplyState(item, other);
            Check("a count set by Coop is not counted twice", CountNow(item) == n + step, "wanted " + (n + step) + ", got " + CountNow(item));
            Forget(item);

            // 7. Nobody else holds this bundle.
            Check("held by another player: no", !HeldByOther(item));

            // Put it back.
            ApplyState(item, Stands(original));
            Forget(item);
            if (wasChanged)
                Changed.Add(item);
            Check("bundle put back as it was", Describe(item) == start, Describe(item));

            var summary = new StringBuilder("[Probe] ");
            summary.Append(_probeFailed == 0 ? "ALL PASSED" : _probeFailed + " FAILED").Append(": ").Append(_probePassed).Append(" passed, ").Append(_probeFailed).Append(" failed.");
            if (_probeFailed == 0)
                Plugin.Log.LogInfo(summary.ToString());
            else
                Plugin.Log.LogWarning(summary.ToString());
        }
    }
}
