using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dizzy.FirewoodBundle.Tests.CoopSim
{
    // Where a bundle keeps its count. Candles keep it in amount, which Coop sends now and
    // then. Food keeps it in extraValue4 as a StackCode, which Coop never sends.
    public enum SimKind
    {
        Candle,
        Sausage,
    }

    // One item as a peer sees it. Only what the sync rules read or write is here.
    public sealed class SimItem
    {
        public int Id;
        public int Prefab;
        public SimKind Kind;
        public double X;
        public float Amount = 1f;
        public float Health = 1f;
        public float Extra4;
        public float Dried;

        public int Count
        {
            get
            {
                if (Kind == SimKind.Candle)
                    return Math.Max(1, (int)Amount);
                return Math.Max(1, StackCode.Decode(Extra4).Count);
            }
        }

        public SimItem Clone()
        {
            return (SimItem)MemberwiseClone();
        }

        public void SetCount(int count)
        {
            if (Kind == SimKind.Candle)
                Amount = count;
            else
                Extra4 = count <= 1 ? 0f : StackCode.Encode(count, 0, 0);
        }

        public override string ToString()
        {
            return "#" + Id + " prefab " + Prefab + " x" + Count + " health " + Health + " dried " + Dried + " at " + X;
        }
    }

    // What Coop and Bundle Up do, as switches. The defaults are how the game works now.
    // Turning one off shows what depends on it.
    public sealed class SimRules
    {
        // Coop. These are copied from how Coop behaves, not choices of ours.
        public bool CoopSetsHeldPointer = true;       // C5
        public bool CoopTakesNearestOnUnknownId = true; // C6
        public bool CoopHostRelays = true;            // C4

        // Bundle Up.
        public bool SendBundlePacket = true;          // packet 249
        public bool AnnounceSpawn = true;             // Spawned() after RegisterToSave
        public bool RefuseWhenHeldByOther = true;     // combine, take from, gather
        public bool SuppressEchoes = true;            // no send for a rebuild another player's change caused
    }

    public sealed class SimMessage
    {
        public string From;
        public string To;
        public string Type;
        public byte[] Body;          // the bundle packet, as BundleWire writes it
        public List<SimItem> Items;  // Coop's own messages carry items
        public int Id;
        public double X;
    }
}
