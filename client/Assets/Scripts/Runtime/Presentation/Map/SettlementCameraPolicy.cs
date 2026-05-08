using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class SettlementCameraPolicy
    {
        private readonly SettlementPlaybackMode _mode;
        private readonly float _playbackStartRealtime;

        public SettlementCameraPolicy(SettlementPlaybackMode mode, float playbackStartRealtime)
        {
            _mode = mode;
            _playbackStartRealtime = playbackStartRealtime;
        }

        public bool ShouldFocus(SettlementPlaybackWindow window)
        {
            if (window == null || !window.AllowsCameraFocus)
            {
                return false;
            }

            if (CinemachineMapCameraController.LastManualInputRealtime > _playbackStartRealtime)
            {
                return false;
            }

            switch (_mode)
            {
                case SettlementPlaybackMode.CriticalOnly:
                    return window.Tier == SettlementPlaybackTier.Critical;
                default:
                    return window.Tier == SettlementPlaybackTier.Important ||
                           window.Tier == SettlementPlaybackTier.Critical;
            }
        }

        public static SettlementCameraPolicy Start(SettlementPlaybackMode mode)
        {
            return new SettlementCameraPolicy(mode, Time.unscaledTime);
        }
    }
}
