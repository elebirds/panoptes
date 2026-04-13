using System;

namespace Panoptes.Core.Application.Intents
{
    public static class ActionLock
    {
        public static bool IsLocked { get; private set; }
        public static event Action<bool> OnChanged;

        public static void Acquire()
        {
            if (IsLocked)
            {
                throw new InvalidOperationException("Action lock is already acquired.");
            }

            IsLocked = true;
            OnChanged?.Invoke(true);
        }

        public static void Release()
        {
            IsLocked = false;
            OnChanged?.Invoke(false);
        }
    }
}
