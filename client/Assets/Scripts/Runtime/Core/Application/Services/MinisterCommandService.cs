using System;
using Panoptes.Core.Application.Intents;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Application.Services
{
    public sealed class MinisterCommandService
    {
        [Serializable]
        private sealed class MinisterDirectivePayload
        {
            public string directive_type;
            public string draft_id;
            public string skill_card_id;
        }

        private readonly IClientMessageSender _sender;

        public MinisterCommandService(IClientMessageSender sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        public bool AcceptDraft(string draftId, string ministerRole = "domestic")
        {
            return SendDirective("accept", draftId, ministerRole);
        }

        public bool RejectDraft(string draftId, string ministerRole = "domestic")
        {
            return SendDirective("reject", draftId, ministerRole);
        }

        public bool AcceptRole(string ministerRole)
        {
            return SendDirective("accept_role", string.Empty, ministerRole);
        }

        public bool RejectRole(string ministerRole)
        {
            return SendDirective("reject_role", string.Empty, ministerRole);
        }

        public bool ActivateSkill(string ministerRole, string skillCardId)
        {
            return SendDirective("activate_skill", string.Empty, ministerRole, skillCardId);
        }

        private bool SendDirective(string directiveType, string draftId, string ministerRole)
        {
            return SendDirective(directiveType, draftId, ministerRole, string.Empty);
        }

        private bool SendDirective(string directiveType, string draftId, string ministerRole, string skillCardId)
        {
            if (ActionLock.IsLocked)
            {
                return false;
            }

            return _sender.Send(new MsgSetMinisterDirective
            {
                MinisterRole = ministerRole ?? string.Empty,
                Content = BuildMinisterDirectiveContent(directiveType, draftId, skillCardId)
            });
        }

        private static string BuildMinisterDirectiveContent(string directiveType, string draftId, string skillCardId)
        {
            var payload = new MinisterDirectivePayload
            {
                directive_type = directiveType,
                draft_id = draftId ?? string.Empty,
                skill_card_id = skillCardId ?? string.Empty
            };
            return JsonUtility.ToJson(payload);
        }
    }
}
