using System;
using System.Collections.Generic;

namespace Panoptes.Core.Application.Stores
{
    public sealed class GameplayFeedbackState
    {
        public GameplayFeedbackState(
            int sequence = 0,
            string source = "",
            string code = "",
            string message = "",
            bool success = false,
            IReadOnlyDictionary<string, string> details = null)
        {
            Sequence = sequence;
            Source = source ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Success = success;
            Details = CloneDetails(details);
        }

        public int Sequence { get; }
        public string Source { get; }
        public string Code { get; }
        public string Message { get; }
        public bool Success { get; }
        public IReadOnlyDictionary<string, string> Details { get; }
        public bool HasFeedback => Sequence > 0 && (!string.IsNullOrWhiteSpace(Code) || !string.IsNullOrWhiteSpace(Message));

        internal GameplayFeedbackState Clone()
        {
            return new GameplayFeedbackState(Sequence, Source, Code, Message, Success, Details);
        }

        private static IReadOnlyDictionary<string, string> CloneDetails(IReadOnlyDictionary<string, string> source)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (var pair in source)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    result[pair.Key] = pair.Value ?? string.Empty;
                }
            }

            return result;
        }
    }
}
