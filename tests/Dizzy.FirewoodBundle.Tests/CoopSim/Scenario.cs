using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dizzy.FirewoodBundle.Tests.CoopSim
{
    public enum Op
    {
        Create,
        SetBundle,
        TakeOne,
        PickUp,
        Drop,
        Combine,
        Hang,
        Health,
        Destroy,
        Recolor,
        Width,
        Rejoin,
        EndFrame,   // this player's frame ends, and some messages arrive before the others' do
    }

    // One thing one player does. Items are chosen by Pick, a position in that player's items
    // sorted by ID, so a scenario still means something when other actions are cut out of it.
    public sealed class Act
    {
        public int Actor;   // 0 is the host, 1 and 2 the guests
        public Op Op;
        public int Pick;
        public int Pick2;
        public int Arg;

        public override string ToString()
        {
            string who = Actor == 0 ? "host" : "guest" + Actor;
            return who + " " + Op + " (item " + Pick + (Op == Op.Combine ? " with " + Pick2 : "") + ", " + Arg + ")";
        }
    }

    // Acts made at the same moment, before any message has arrived, and then everything arrives.
    public sealed class Batch
    {
        public List<Act> Acts = new List<Act>();
        public int DeliverySeed;
    }

    public sealed class Scenario
    {
        public List<Batch> Batches = new List<Batch>();

        public int ActCount { get { return Batches.Sum(b => b.Acts.Count); } }

        public Scenario Copy()
        {
            var copy = new Scenario();
            foreach (Batch b in Batches)
                copy.Batches.Add(new Batch { DeliverySeed = b.DeliverySeed, Acts = new List<Act>(b.Acts) });
            return copy;
        }

        public override string ToString()
        {
            var text = new StringBuilder();
            int n = 1;
            foreach (Batch batch in Batches)
            {
                text.AppendLine("  at once (delivery seed " + batch.DeliverySeed + "):");
                foreach (Act act in batch.Acts)
                    text.AppendLine("    " + n++ + ". " + act);
            }
            return text.ToString();
        }

        // ---- making one ----

        public static Scenario Generate(int seed, int batches, int maxTogether)
        {
            var random = new Random(seed);
            var scenario = new Scenario();
            for (int i = 0; i < batches; i++)
            {
                var batch = new Batch { DeliverySeed = random.Next(1000000) };
                int together = 1 + random.Next(maxTogether);
                for (int k = 0; k < together; k++)
                {
                    batch.Acts.Add(new Act
                    {
                        Actor = random.Next(3),
                        Op = (Op)random.Next(Enum.GetValues(typeof(Op)).Length),
                        Pick = random.Next(1000),
                        Pick2 = random.Next(1000),
                        Arg = random.Next(10),
                    });
                }
                scenario.Batches.Add(batch);
            }
            return scenario;
        }

        // ---- running one ----

        private static readonly int[] Prefabs = { 10, 20, 21 };

        public static SimNetwork Start(SimRules rules = null)
        {
            var net = new SimNetwork(rules);
            net.Join("guest1");
            net.Join("guest2");
            net.Settle();
            return net;
        }

        public SimNetwork Run(SimRules rules = null)
        {
            SimNetwork net = Start(rules);
            foreach (Batch batch in Batches)
            {
                foreach (Act act in batch.Acts)
                    Do(net, act);
                net.SettleInterleaved(batch.DeliverySeed);
            }
            return net;
        }

        public static void Do(SimNetwork net, Act act)
        {
            SimPeer actor = net.Peers[act.Actor % net.Peers.Count];
            List<SimItem> items = actor.Items.Values.OrderBy(i => i.Id).ToList();

            if (act.Op == Op.Rejoin)
            {
                // A guest drops out and comes back; the host does not.
                if (actor.IsHost)
                    return;
                string name = actor.Name;
                net.Disconnect(name);
                net.Join(name);
                net.Settle();   // a joiner does nothing until the snapshot is in
                return;
            }
            if (act.Op == Op.EndFrame)
            {
                actor.EndFrame();
                net.DeliverSome(act.Pick, 1 + act.Arg);
                return;
            }
            if (act.Op == Op.Create)
            {
                int prefab = Prefabs[act.Pick % Prefabs.Length];
                actor.Create(prefab, prefab == 10 ? SimKind.Candle : SimKind.Sausage, act.Arg % 5, 1 + act.Arg % 4);
                return;
            }
            if (items.Count == 0)
                return;

            SimItem item = items[act.Pick % items.Count];
            switch (act.Op)
            {
                case Op.SetBundle: actor.SetBundle(item, 1 + act.Arg % 6); break;
                case Op.TakeOne: actor.TakeOne(item); break;
                case Op.PickUp:
                    if (!actor.RemoteHeld.Contains(item.Id) && !actor.HeldByMe.Contains(item.Id))
                        actor.PickUp(item);
                    break;
                case Op.Drop:
                    if (actor.HeldByMe.Contains(item.Id))
                        actor.Drop(item, act.Arg % 5);
                    break;
                case Op.Combine:
                    SimItem other = items[act.Pick2 % items.Count];
                    if (other != item && other.Prefab == item.Prefab)
                        actor.Combine(item, other);
                    break;
                case Op.Hang: actor.Hang(item, act.Arg % 2 == 0); break;
                case Op.Health: actor.ChangeHealth(item, act.Arg / 10f); break;
                case Op.Destroy:
                    if (!actor.RemoteHeld.Contains(item.Id) && item.Count == 1)
                        actor.Destroy(item);
                    break;
                case Op.Recolor: actor.Recolor(item, act.Arg % 4); break;
                case Op.Width: actor.CycleWidth(item, act.Arg % 9); break;
            }
        }

        public bool Fails(SimRules rules = null)
        {
            try
            {
                return Run(rules).Differences().Count > 0;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        // ---- shrinking ----

        // The smallest scenario found that still fails: whole batches are tried out first, then
        // single acts, until nothing more can be taken away.
        public Scenario Shrink(SimRules rules = null)
        {
            Scenario best = Copy();
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int b = 0; b < best.Batches.Count; b++)
                {
                    Scenario trial = best.Copy();
                    trial.Batches.RemoveAt(b);
                    if (trial.Fails(rules))
                    {
                        best = trial;
                        changed = true;
                        b--;
                    }
                }
                for (int b = 0; b < best.Batches.Count; b++)
                {
                    for (int a = 0; a < best.Batches[b].Acts.Count; a++)
                    {
                        Scenario trial = best.Copy();
                        trial.Batches[b].Acts.RemoveAt(a);
                        if (trial.Batches[b].Acts.Count == 0)
                            trial.Batches.RemoveAt(b);
                        if (trial.Fails(rules))
                        {
                            best = trial;
                            changed = true;
                            a--;
                            if (b >= best.Batches.Count)
                                break;
                        }
                    }
                }
            }
            return best;
        }
    }
}
