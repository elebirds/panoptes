/*************************************************
 * Project: Panoptes
 * File: IPlanningInputMode.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Lifecycle contract for player planning input modes; modes only collect input and forward intents.
 *************************************************/

namespace Panoptes.Presentation.Planning.Input
{
    /// <summary>
    /// Defines the small lifecycle surface shared by planning input modes.
    /// </summary>
    public interface IPlanningInputMode
    {
        void Enter();
        void Exit();
        void Tick();
        bool HandlePrimary();
        bool HandleCancel();
    }
}
