using NUnit.Framework;
using Panoptes.Presentation.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class SquadUnitRenderBudgetPresenterTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        [Test]
        public void Apply_ShouldKeepHighestPriorityRenderers()
        {
            _root = new GameObject("member");
            var low = CreateRenderer("cape_extra");
            var body = CreateRenderer("body_main");
            var weapon = CreateRenderer("weapon_sword");

            var options = new SquadUnitRenderBudgetPresenter.Options(
                maxVisibleRenderers: 2,
                disableCastShadows: true,
                disableReceiveShadows: true,
                rendererPriorityKeywords: new[] { "body", "weapon" });

            SquadUnitRenderBudgetPresenter.Apply(_root.transform, options);

            Assert.That(body.enabled, Is.True);
            Assert.That(weapon.enabled, Is.True);
            Assert.That(low.enabled, Is.False);
            Assert.That(body.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(weapon.receiveShadows, Is.False);
        }

        [Test]
        public void IsOverThreshold_ShouldCountActiveRenderers()
        {
            _root = new GameObject("member");
            CreateRenderer("body_main");
            CreateRenderer("weapon_sword");

            Assert.That(SquadUnitRenderBudgetPresenter.IsOverThreshold(_root.transform, 1), Is.True);
            Assert.That(SquadUnitRenderBudgetPresenter.IsOverThreshold(_root.transform, 2), Is.False);
        }

        private MeshRenderer CreateRenderer(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_root.transform, false);
            return child.AddComponent<MeshRenderer>();
        }
    }
}
