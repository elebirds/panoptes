using NUnit.Framework;
using Panoptes.Presentation.UI.Domestic;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class BuildItemViewTests
    {
        [Test]
        public void SetLocked_ShouldKeepButtonInteractable_ForSoftLockedBuildEntries()
        {
            var gameObject = new GameObject("BuildItemView", typeof(Image), typeof(Button));
            var view = gameObject.AddComponent<BuildItemView>();
            var button = gameObject.GetComponent<Button>();

            view.SetLocked(true);

            Assert.That(button, Is.Not.Null);
            Assert.That(button!.interactable, Is.True,
                "建造项软锁定只应保留提示，不应直接禁用点击。");

            Object.DestroyImmediate(gameObject);
        }
    }
}
