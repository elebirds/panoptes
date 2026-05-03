using System.Collections;
using NUnit.Framework;
using Panoptes.Core.Application.App;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Presentation.Binders.UiToolkit;
using Panoptes.Presentation.Composition;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.ViewModels;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Panoptes.Tests.PlayMode.Composition
{
    public sealed class C0ePlayModeCompositionBootstrapTests
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SuppressAppManagerInitialSceneTransition()
        {
            AppManager.SuppressInitialTransitionForTests = true;
        }

        [UnityTest]
        public IEnumerator RuntimeBootstrapAndGameScene_ShouldBuildResolvableFinalScopes()
        {
            yield return null;

            var projectScope = FindSingle<ProjectLifetimeScope>("runtime project composition bootstrap");
            Assert.That(projectScope.Container, Is.Not.Null, "ProjectLifetimeScope must build a VContainer container.");
            AssertResolves<AppManager>(projectScope.Container);
            AssertResolves<IClientMessageSender>(projectScope.Container);
            AssertResolves<StaticCatalogStore>(projectScope.Container);

            var loadOperation = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            Assert.That(loadOperation, Is.Not.Null, "Game scene must be present in build settings for PlayMode composition smoke tests.");
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            yield return null;
            yield return null;

            projectScope = FindSingle<ProjectLifetimeScope>("project scope after loading Game scene");
            var gameScope = FindSingle<GameLifetimeScope>("game scene composition scope");
            Assert.That(gameScope.Container, Is.Not.Null, "GameLifetimeScope must build a VContainer child container.");
            Assert.That(gameScope.Parent, Is.SameAs(projectScope), "GameLifetimeScope must parent to the runtime ProjectLifetimeScope.");

            AssertResolves<GameStateStore>(gameScope.Container);
            AssertResolves<PlanningDraftStore>(gameScope.Container);
            AssertResolves<SelectionStore>(gameScope.Container);
            AssertResolves<TurnStore>(gameScope.Container);
            AssertResolves<GameIntentService>(gameScope.Container);
            AssertResolves<PlanningIntentService>(gameScope.Container);
            AssertResolves<MinisterCommandService>(gameScope.Container);
            AssertResolves<ManagementPanelVisibilityStore>(gameScope.Container);
            AssertResolves<NationalOverviewViewModel>(gameScope.Container);
            AssertResolves<BuildCatalogViewModel>(gameScope.Container);
            AssertResolves<TechTreeViewModel>(gameScope.Container);
            AssertResolves<RecipeSynthesisViewModel>(gameScope.Container);
            AssertResolves<PolicyFocusViewModel>(gameScope.Container);
            AssertResolves<NationalLedgerViewModel>(gameScope.Container);
            AssertResolves<TokenHUD>(gameScope.Container);
            AssertResolves<ManagementHostUiToolkitBinder>(gameScope.Container);
            AssertResolves<BuildCatalogUiToolkitBinder>(gameScope.Container);
            AssertResolves<TechTreeUiToolkitBinder>(gameScope.Container);
            AssertResolves<RecipeSynthesisUiToolkitBinder>(gameScope.Container);
            AssertResolves<PolicyFocusUiToolkitBinder>(gameScope.Container);
            AssertResolves<NationalLedgerUiToolkitBinder>(gameScope.Container);
        }

        private static void AssertResolves<T>(IObjectResolver resolver)
        {
            Assert.That(resolver, Is.Not.Null);
            Assert.DoesNotThrow(
                () => resolver.Resolve<T>(),
                typeof(T).Name + " must resolve from the active composition scope.");
        }

        private static T FindSingle<T>(string label) where T : Object
        {
            var instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            Assert.That(instances, Has.Length.EqualTo(1), "Expected exactly one " + typeof(T).Name + " for " + label + ".");
            return instances[0];
        }
    }
}
