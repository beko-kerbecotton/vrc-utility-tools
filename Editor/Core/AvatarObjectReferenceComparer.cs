using nadena.dev.modular_avatar.core;

namespace net.bekobeko.utilitytools.core
{
    /// <summary>
    /// Centralizes equality while preserving Modular Avatar's comparison semantics,
    /// which cannot be reproduced through public fields alone.
    /// </summary>
    public static class AvatarObjectReferenceComparer
    {
        public static bool AreEqual(AvatarObjectReference first, AvatarObjectReference second)
        {
            return object.Equals(first, second);
        }
    }
}
