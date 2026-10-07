using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    // How many sausages a stack holds and how wide it is. Food uses amount for how
    // cooked it is, so a stack cannot keep its count there like firewood does.
    [DisallowMultipleComponent]
    internal sealed class StackState : MonoBehaviour
    {
        internal int Count = 1;
        internal int Width;
        internal int Color;
    }

    // Only sausages that will not spoil stack, and only with the same kind of sausage.
    // A stack is never eaten, cooked, salted, sliced or put in soup as a whole.
    internal static class SausageStacks
    {
        internal const string ItemName = "sausage";
        internal const string BananaName = "banana";
        internal const string FreshBanana = "Only dried bananas can hang";
        internal const string EatenBanana = "Cannot hang eaten bananas";
        internal const string AppleName = "apple";
        internal const string FreshApple = "Only dried apples go in a bag";
        internal const string EatenApple = "Cannot bag eaten apples";
        internal const string Unpreserved = "Cannot stack unpreserved sausages";
        internal const string DifferentKind = "Cannot stack different kinds of sausage";
        internal const string Eaten = "Cannot stack eaten sausages";

        // Auto keeps the stack as close to square as it can. 1 is a single-file tower.
        // After the widths comes Tree, layers that cross and narrow toward the top, then
        // Pile, sausages dropped onto each other in a heap. Tree keeps the value Pile had
        // before Pile was rebuilt, so a saved stack keeps its look.
        internal const int MaxWidth = 5;
        internal const int TreeWidth = MaxWidth + 1;
        internal const int PileWidth = MaxWidth + 2;
        // Not a stack shape: marks a hanging bundle, which keeps its state even with one
        // sausage left, since it is still tied to its hook.
        internal const int HangWidth = 8;
        private const int WidthStride = 1000;
        private const int ColorStride = 10000;

        private static float _notifiedAt = -10f;

        internal static int CountOf(ShipItem item)
        {
            StackState state = item != null ? item.GetComponent<StackState>() : null;
            return state != null && state.Count > 1 ? state.Count : 1;
        }

        internal static int WidthOf(ShipItem item)
        {
            StackState state = item != null ? item.GetComponent<StackState>() : null;
            return state != null ? state.Width : 0;
        }

        internal static void WriteCount(ShipItem item, int count)
        {
            if (item == null)
                return;
            StackState state = item.GetComponent<StackState>();
            if (count <= 1 && (state == null || state.Width != HangWidth))
            {
                if (state != null)
                {
                    state.Count = 1;
                    state.Width = 0;
                }
                return;
            }

            if (state == null)
                state = item.gameObject.AddComponent<StackState>();
            state.Count = Mathf.Max(1, count);
        }

        internal static int ColorOf(ShipItem item)
        {
            StackState state = item != null ? item.GetComponent<StackState>() : null;
            return state != null ? state.Color : 0;
        }

        internal static void WriteColor(ShipItem item, int color)
        {
            StackState state = item != null ? item.GetComponent<StackState>() : null;
            if (state != null)
                state.Color = color;
        }

        internal static bool IsHanging(Component component)
        {
            StackState state = component != null ? component.GetComponent<StackState>() : null;
            return state != null && state.Width == HangWidth;
        }

        // A single sausage becomes a hanging bundle of one.
        internal static void MakeHanging(ShipItem item)
        {
            StackState state = item.GetComponent<StackState>();
            if (state == null)
                state = item.gameObject.AddComponent<StackState>();
            state.Count = 1;
            state.Width = HangWidth;
        }

        // The last sausage off a hanging bundle is a plain sausage again.
        internal static void Unhang(ShipItem item)
        {
            StackState state = item != null ? item.GetComponent<StackState>() : null;
            if (state == null)
                return;
            state.Count = 1;
            state.Width = 0;
            state.Color = 0;
        }

        // The stack is saved in the food's spare extra value: count + width * 1000.
        internal static float Encode(ShipItem item)
        {
            return CountOf(item) + WidthOf(item) * WidthStride + ColorOf(item) * ColorStride;
        }

        internal static void Decode(ShipItem item, float saved)
        {
            int value = Mathf.RoundToInt(saved);
            int color = value / ColorStride;
            value %= ColorStride;
            int count = value % WidthStride;
            int width = value / WidthStride;
            if (width == HangWidth && count >= 1)
            {
                MakeHanging(item);
                WriteCount(item, count);
                WriteColor(item, color);
                return;
            }
            if (count <= 1)
                return;
            WriteCount(item, count);
            item.GetComponent<StackState>().Width = Mathf.Clamp(width, 0, PileWidth);
        }

        internal static bool IsStack(Component component)
        {
            ShipItem item = FirewoodPieces.AsShip(component);
            BundleKind kind = FirewoodPieces.KindOf(item);
            return (kind == BundleKind.Sausage && CountOf(item) > 1) || (kind != null && kind.Hangs);
        }

        // Smoked or dried food barely spoils. Rotten food is past saving.
        internal static bool IsPreserved(ShipItem item)
        {
            FoodState food = item != null ? item.GetComponent<FoodState>() : null;
            if (food == null || food.spoiled > 0.9f)
                return false;
            if (IsBanana(item) || IsApple(item))
                return food.dried >= 0.99f;
            return food.smoked >= 0.99f || food.dried >= 0.99f;
        }

        internal static bool IsWhole(ShipItem item)
        {
            ShipItem prefab = FirewoodPieces.PrefabOf(item);
            return item != null && (prefab == null || item.health >= prefab.health - 0.01f);
        }

        // The word the game puts in front of the name, in the same order it checks them.
        internal static string KindWord(ShipItem item)
        {
            FoodState food = item != null ? item.GetComponent<FoodState>() : null;
            if (food == null)
                return "";
            if (food.spoiled > 0.9f)
                return "rotten";
            if (item.amount >= 1.5f)
                return "burnt";
            if (food.salted >= 0.99f && food.smoked >= 0.99f)
                return "salted smoked";
            if (food.salted >= 0.99f)
                return "salted";
            if (food.smoked >= 0.99f)
                return "smoked";
            if (food.dried >= 0.99f)
                return "dried";
            if (item.amount >= 1f)
                return "cooked";
            return "raw";
        }

        // A stack is labelled by count and kind alone, e.g. "76 Smoked Sausages".
        internal static string Label(ShipItem item, int count)
        {
            BundleKind kind = FirewoodPieces.KindOf(item);
            string words = KindWord(item) + " " + (kind != null ? kind.Plural : "sausages");
            var label = new System.Text.StringBuilder(count + " ");
            bool start = true;
            for (int i = 0; i < words.Length; i++)
            {
                char c = words[i];
                label.Append(start ? char.ToUpperInvariant(c) : c);
                start = c == ' ';
            }

            return label.ToString();
        }

        // Why these two cannot stack, or null when they can.
        internal static string Rejection(ShipItem held, ShipItem target)
        {
            if (IsApple(held) && IsApple(target))
            {
                if (!IsPreserved(held) || !IsPreserved(target))
                    return FreshApple;
                return !IsWhole(held) || !IsWhole(target) ? EatenApple : null;
            }
            if (IsBanana(held) && IsBanana(target))
            {
                if (!IsPreserved(held) || !IsPreserved(target))
                    return FreshBanana;
                return !IsWhole(held) || !IsWhole(target) ? EatenBanana : null;
            }
            if (!IsSausage(held) || !IsSausage(target))
                return null;
            if (!IsPreserved(held) || !IsPreserved(target))
                return Unpreserved;
            if (!IsWhole(held) || !IsWhole(target))
                return Eaten;
            if (KindWord(held) != KindWord(target))
                return DifferentKind;
            return null;
        }

        internal static bool IsSausage(ShipItem item)
        {
            BundleKind kind = FirewoodPieces.KindOf(item);
            return kind == BundleKind.Sausage || kind == BundleKind.HangingSausage;
        }

        internal static bool IsBanana(ShipItem item)
        {
            BundleKind kind = FirewoodPieces.KindOf(item);
            return kind == BundleKind.Banana || kind == BundleKind.HangingBanana;
        }

        internal static bool IsApple(ShipItem item)
        {
            BundleKind kind = FirewoodPieces.KindOf(item);
            return kind == BundleKind.Apple || kind == BundleKind.AppleBag;
        }

        // Why this loose piece cannot start a hanging bundle, or null.
        internal static string HangRefusal(ShipItem item)
        {
            bool banana = IsBanana(item);
            bool apple = IsApple(item);
            if (!IsPreserved(item))
                return apple ? FreshApple : banana ? FreshBanana : Unpreserved;
            if (!IsWhole(item))
                return apple ? EatenApple : banana ? EatenBanana : Eaten;
            return null;
        }

        // The stack takes the worst of both: the most spoiled, the least dried, smoked
        // and salted. Same-kind sausages keep the same word and stay preserved.
        internal static void MergeFood(ShipItem into, ShipItem from, int intoCount, int fromCount)
        {
            FoodState a = into != null ? into.GetComponent<FoodState>() : null;
            FoodState b = from != null ? from.GetComponent<FoodState>() : null;
            if (a == null || b == null)
                return;

            a.spoiled = Mathf.Max(a.spoiled, b.spoiled);
            a.dried = Mathf.Min(a.dried, b.dried);
            a.smoked = Mathf.Min(a.smoked, b.smoked);
            a.salted = Mathf.Min(a.salted, b.salted);
            int total = Mathf.Max(1, intoCount + fromCount);
            into.amount = (into.amount * intoCount + from.amount * fromCount) / total;
            UpdateMaterial(into);
        }

        // A sausage taken off the stack is in the same state as the stack.
        internal static void CopyFood(ShipItem from, ShipItem to)
        {
            FoodState a = from != null ? from.GetComponent<FoodState>() : null;
            FoodState b = to != null ? to.GetComponent<FoodState>() : null;
            if (a == null || b == null)
                return;

            to.amount = from.amount;
            to.health = from.health;
            b.dried = a.dried;
            b.smoked = a.smoked;
            b.salted = a.salted;
            b.spoiled = a.spoiled;
            UpdateMaterial(to);
        }

        private static void UpdateMaterial(ShipItem item)
        {
            CookableFood cookable = item.GetComponent<CookableFood>();
            if (cookable != null)
                cookable.UpdateMaterial();
        }

        internal static void Notify(string message)
        {
            if (NotificationUi.instance == null || Time.time - _notifiedAt < 0.75f)
                return;
            _notifiedAt = Time.time;
            NotificationUi.instance.ShowNotification(message, 2f);
        }

        // ] while holding or looking at a sausage stack makes it one column wider,
        // up to MaxWidth, then a tree, then a pile, then back to Auto.
        internal static void CycleLookedAtWidth()
        {
            if (!BundleKind.Sausage.IsEnabled)
                return;
            if (GameState.inCursorMenu || GameState.sleeping || BoatCamera.on)
                return;
            if (!Input.GetKeyDown(FirewoodBundleConfig.WidthKeyCode))
                return;

            GoPointer[] pointers = Object.FindObjectsOfType<GoPointer>();
            for (int i = 0; i < pointers.Length; i++)
            {
                GoPointer pointer = pointers[i];
                if (pointer == null)
                    continue;
                ShipItem stack = FirewoodPieces.AsShip(pointer.GetHeldItem());
                if (stack == null)
                    stack = pointer.GetPointedAtItem();
                if (!IsStack(stack) || IsHanging(stack))
                    continue;

                StackState state = stack.GetComponent<StackState>();
                state.Width = state.Width >= PileWidth ? 0 : state.Width + 1;
                FirewoodBundleBuilder.Apply(stack);
                Plugin.Log.LogInfo("Sausage stack is now " + WidthName(state.Width) + ".");
                return;
            }
        }

        internal static string WidthPrompt(ShipItem stack)
        {
            return FirewoodBundleConfig.KeyLabel(FirewoodBundleConfig.WidthKeyCode) + " Width: " + WidthName(WidthOf(stack));
        }

        private static string WidthName(int width)
        {
            if (width <= 0)
                return "Auto";
            if (width == TreeWidth)
                return "Tree";
            return width >= PileWidth ? "Pile" : width.ToString();
        }
    }
}
