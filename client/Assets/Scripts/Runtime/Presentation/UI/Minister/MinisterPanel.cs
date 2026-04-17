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
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using UnityEngine;

namespace Panoptes.Presentation.UI.Minister
{
    public sealed class MinisterPanel : MonoBehaviour
    {
        private readonly List<MinisterDraftDto> _visibleDrafts = new();
        private PlanningDraftCache _draftCache;

        public IReadOnlyList<MinisterDraftDto> VisibleDrafts => _visibleDrafts;

        public event Action DraftsChanged;

        private void OnEnable()
        {
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

            GameIntents.AcceptMinisterAction(draftId);
        }

        public void RejectDraft(string draftId)
        {
            if (string.IsNullOrWhiteSpace(draftId))
            {
                return;
            }

            GameIntents.RejectMinisterAction(draftId);
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
