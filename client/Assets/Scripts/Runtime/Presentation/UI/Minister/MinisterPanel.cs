/*************************************************
 * Project: Panoptes
 * File: MinisterPanel.cs
 * Author: Panoptes Team
 * Date: 2026-04-17
 * Description: Domestic minister draft panel state controller.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Composition;
using UnityEngine;
using VContainer;

namespace Panoptes.Presentation.UI.Minister
{
    public sealed class MinisterPanel : MonoBehaviour
    {
        private readonly List<MinisterDraftDto> _visibleDrafts = new();
        private PlanningDraftCache _draftCache;
        private MinisterCommandService _ministerCommandService;

        public IReadOnlyList<MinisterDraftDto> VisibleDrafts => _visibleDrafts;

        public event Action DraftsChanged;

        [Inject]
        private void Construct(MinisterCommandService ministerCommandService)
        {
            _ministerCommandService = ministerCommandService;
        }

        private void OnEnable()
        {
            SceneCommandServiceInjector.InjectIfAvailable(this);
            AttachDraftCache();
            RefreshDrafts();
        }

        private void OnDisable()
        {
            if (_draftCache != null)
            {
                _draftCache.OrdersChanged -= RefreshDrafts;
            }
        }

        public void AcceptDraft(string draftId)
        {
            if (string.IsNullOrWhiteSpace(draftId))
            {
                return;
            }

            _ministerCommandService?.AcceptDraft(draftId);
        }

        public void RejectDraft(string draftId)
        {
            if (string.IsNullOrWhiteSpace(draftId))
            {
                return;
            }

            _ministerCommandService?.RejectDraft(draftId);
        }

        private void AttachDraftCache()
        {
            var nextDraftCache = PlanningDraftCache.Instance ?? PlanningDraftCache.EnsureInstance();
            if (_draftCache != null)
            {
                _draftCache.OrdersChanged -= RefreshDrafts;
            }

            _draftCache = nextDraftCache;
            if (_draftCache != null)
            {
                _draftCache.OrdersChanged += RefreshDrafts;
            }
        }

        private void RefreshDrafts()
        {
            _visibleDrafts.Clear();

            var drafts = _draftCache != null
                ? _draftCache.GetDomesticMinisterDrafts()
                : null;
            if (drafts != null)
            {
                for (var i = 0; i < drafts.Count; i++)
                {
                    if (drafts[i] != null)
                    {
                        _visibleDrafts.Add(drafts[i]);
                    }
                }
            }

            DraftsChanged?.Invoke();
        }
    }
}
