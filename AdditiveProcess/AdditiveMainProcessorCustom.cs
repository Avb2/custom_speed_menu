// <copyright file="AdditiveMainProcessorCustom.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc.AdditiveProcess
{
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <content>
    ///     Easy-to-customize section of the main processor.
    ///     This customizable section is part of the main processor class therefore
    ///     all the fields, properties, and methods are accessible here.
    /// </content>
    internal partial class AdditiveMainProcessor : MainProcessor
    {
        /// <inheritdoc/>
        internal override void EditOperation(IOperation operation)
        {
            //// BASE - Default processing
            base.EditOperation(operation); // DO NOT REMOVE

            //// CUSTOMIZATION - Uncomment the example below
            //// Example: Add events to the first point
            // operation.FirstPoint.Events.CreateCommandEvent().SetCommandText("Some command").AddBefore();
            // operation.FirstPoint.Events.CreateCommentEvent().SetCommentText("Some comment").AddBefore();
        }

        /// <inheritdoc/>
        internal override void EditPoint(IOperation operation, IPoint point)
        {
            //// BASE - Default processing
            base.EditPoint(operation, point); // DO NOT REMOVE

            //// ADDITIVE MANUFACTURING -  Process activation and deactivation implementation (point level)
            this.EditPointProcessActivationDeactivation(operation, point);

            //// CUSTOMIZATION - Uncomment the example below
            //// Example: Add event to sharp corner
            // if (point.Flags.IsInProcess &&
            //     point != operation.LastPoint &&
            //     point.PathDirection.AngleBetween(point.NextPoint.PathDirection) > 45)
            // {
            //    point.Events.CreateCommentEvent().SetCommentText("Sharp corner detected").AddBefore();
            // }
        }

        /// <summary>
        ///     Adds the process activation event.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void AddProcessActivationEvent(IOperation operation, IPoint point)
        {
            //// ADDITIVE MANUFACTURING - Add the Weld On event
            // Initialize the event
            // Example: Weld Start[1,1]
            var weldOnEvent = point.Events.CreateAdditiveOnEvent();
            weldOnEvent.SetAdditiveOnCommand(operation.Menus.AdditiveSettings.WeldOnCommand);
            weldOnEvent.SetAdditiveScheduleProcess(operation.Menus.AdditiveSettings.WeldStartScheduleProcess);
            weldOnEvent.AddAfter();

            //// CUSTOMIZATION - Uncomment the example below
            // Example: Add other events to the process activation.
            // point.Events.CreateCommandEvent().SetCommandText("Some other command").AddAfter();
            // point.Events.CreateCommentEvent().SetCommentText("Some comment").AddAfter();
        }

        /// <summary>
        ///     Adds the process deactivation event.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void AddProcessDeactivationEvent(IOperation operation, IPoint point)
        {
            //// ADDITIVE MANUFACTURING - Add the Weld Off event
            // Initialize the event
            // Example: Weld End[1,1]
            var weldOffEvent = point.Events.CreateAdditiveOffEvent();
            weldOffEvent.SetAdditiveOffCommand(operation.Menus.AdditiveSettings.WeldOffCommand);
            weldOffEvent.SetAdditiveScheduleProcess(operation.Menus.AdditiveSettings.WeldEndScheduleProcess);
            weldOffEvent.AddAfter();

            //// CUSTOMIZATION - Uncomment the example below
            // Example: Add other events to the process deactivation.
            // point.Events.CreateCommandEvent().SetCommandText("Some other command").AddAfter();
            // point.Events.CreateCommentEvent().SetCommentText("Some comment").AddAfter();
        }
    }
}
