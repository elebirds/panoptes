using System;

namespace Panoptes.Runtime.Action
{
    public static class ActionLock
    {
        public static bool IsLocked { get; private set; }

        public static void Acquire()
        {
            if (IsLocked)
            {
                throw new InvalidOperationException("Action lock is already acquired.");
            }

            IsLocked = true;
        }

        public static void Release()
        {
            IsLocked = false;
        }
    }
}
