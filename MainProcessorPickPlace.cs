// <copyright file="MainProcessorPickPlace.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc
{
    using Robotmaster.Processor.Core.Common.Enums;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <summary>
    /// This partial class contains the logic for the Pick and Place feature.
    /// </summary>
    internal partial class MainProcessor
    {
        /// <summary>
        /// Adds the pick place events.
        /// </summary>
        /// <param name="operation">The operation.</param>
        internal virtual void AddPickPlaceEvents(IOperation operation)
        {
            var gripperOpenCommand = operation.Menus.PickPlaceSettings.GripperOpenCommand;
            var gripperCloseCommand = operation.Menus.PickPlaceSettings.GripperCloseCommand;

            switch (operation.ApplicationData.PickPlace.PickPlaceStep)
            {
                case PickPlaceStep.ToPick:

                    // add the open and close gripper Commands before first and after last point respectively.
                    operation.FirstPoint.Events.CreateGripperOpenEvent().SetCommand(gripperOpenCommand).AddBefore();
                    operation.LastPoint.Events.CreateGripperCloseEvent().SetCommand(gripperCloseCommand).AddAfter();
                    break;

                case PickPlaceStep.ToPlace:

                    // add open gripper Command after last point
                    operation.LastPoint.Events.CreateGripperOpenEvent().SetCommand(gripperOpenCommand).AddAfter();
                    break;

                case PickPlaceStep.AfterPick:
                case PickPlaceStep.AfterPlace:
                case PickPlaceStep.None:

                default:
                    break;
            }
        }
    }
}
