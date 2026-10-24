using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    // The game moves a held item to the Ignore Raycast layer and back when it is put
    // down, but only the item itself. A bundle's own collider is a child, so it keeps
    // whatever layer it was made on. Made while held, it can never be looked at again.
    // Made on the deck, it blocks the holder's own look. It follows the item's layer.
    internal sealed class FollowLayer : MonoBehaviour
    {
        private void LateUpdate()
        {
            Transform parent = transform.parent;
            if (parent != null && gameObject.layer != parent.gameObject.layer)
                gameObject.layer = parent.gameObject.layer;
        }
    }
}
