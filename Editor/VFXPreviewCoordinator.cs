using UnityEngine;
using System;

namespace VFXTools.Editor
{
    public static class VFXPreviewCoordinator
    {
        public static event Action<object> OnPreviewStarted;

        public static void NotifyPreviewStarted(object sender)
        {
            OnPreviewStarted?.Invoke(sender);
        }
    }
}
