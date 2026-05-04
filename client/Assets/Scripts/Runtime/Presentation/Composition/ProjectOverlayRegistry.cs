using Panoptes.Presentation.UI.Common;
using UnityEngine;

namespace Panoptes.Presentation.Composition
{
    public sealed class ProjectOverlayRegistry : MonoBehaviour
    {
        public ErrorToast ErrorToast { get; private set; }
        public ConfirmDialog ConfirmDialog { get; private set; }

        public void Configure(ErrorToast errorToast, ConfirmDialog confirmDialog)
        {
            ErrorToast = errorToast;
            ConfirmDialog = confirmDialog;
        }
    }

}
