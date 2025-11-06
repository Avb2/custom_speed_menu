// <copyright file="WeldingMainProcessorCustom.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc.WeldingProcess
{
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.TouchSensingSettings;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.WeldingSettings;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <summary>
    ///     Easy-to-customize section of the main processor.
    ///     This customizable section is part of the main processor class therefore
    ///     all the fields, properties and methods are accessible here.
    /// </summary>
    internal partial class WeldingMainProcessor : MainProcessor
    {
        /// <inheritdoc/>
        internal override void EditOperation(IOperation operation)
        {
            //// BASE - Default processing
            base.EditOperation(operation); // DO NOT REMOVE

            this.EditOperationMacroCall(operation);

            if (operation.ApplicationData.TouchSensing != null)
            {
                this.EditTouchSensingOperation(operation);
            }

            if (operation.ApplicationData.Welding != null)
            {
                this.EditWeldingOperation(operation);
            }

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

            if (point.ApplicationData.Welding != null)
            {
                //// Add welding "start" events to first point of contact with part
                if (point.Flags.IsFirstPointOfContact)
                {
                    this.AddWeldStartEvent(operation, point);
                    this.AddWeaveStartEvent(operation, point);
                    this.AddSeamTrackingStartEvent(operation, point);
                }

                //// Add welding "end" events to last point of contact with part
                if (point.Flags.IsLastPointOfContact)
                {
                    this.AddWeldEndEvent(operation, point);
                    this.AddWeaveEndEvent(operation, point);
                    this.AddSeamTrackingEndEvent(operation, point);
                }

                switch (operation.Menus.TouchSensingSettings.TouchOffsetOutputType)
                {
                    case TouchOffsetOutputType.Sequential:
                        this.EditPointForSequentialTouchOffset(operation, point);
                        break;

                    case TouchOffsetOutputType.Interpolated:
                        this.EditPointForInterpolatedTouchOffset(operation, point);
                        break;
                }
            }

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
        ///     Adds the Weld Start event.
        /// </summary>
        /// <param name="operation">
        ///     The operation.
        /// </param>
        /// <param name="point">
        ///     The point.
        /// </param>
        internal virtual void AddWeldStartEvent(IOperation operation, IPoint point)
        {
            // Initialize the event
            // Example: Weld Start[1,1]
            // Set event parameters to respective menu values
            // Define the event index (before/after)
            var weldStartEvent = point.Events.CreateArcStartEvent();

            weldStartEvent
                .SetArcStartCommand(operation.Menus.WeldingSettings.ArcStartCommand)
                .SetWeldStartSchedule(operation.Menus.WeldingSettings.StartSchedule)
                .AddAfter();

            //// CUSTOMIZATION - Uncomment the example below
            // Example: Add other events to the process activation.
            // Uncomment the lines below if needed.
            // point.Events.CreateCommandEvent().SetCommandText("Some other command").AddAfter();
            // point.Events.CreateCommentEvent().SetCommentText("Some comment").AddAfter();
        }

        /// <summary>
        ///     Adds the Weld End event.
        /// </summary>
        /// <param name="operation">
        ///     The operation.
        /// </param>
        /// <param name="point">
        ///     The point.
        /// </param>
        internal virtual void AddWeldEndEvent(IOperation operation, IPoint point)
        {
            // Initialize the event
            // Example: Weld End[1,1]
            // Set event parameters to respective menu values
            // Define the event index (before/after)
            var weldStartEvent = point.Events.CreateArcEndEvent();

            weldStartEvent
                .SetArcEndCommand(operation.Menus.WeldingSettings.ArcEndCommand)
                .SetWeldEndSchedule(operation.Menus.WeldingSettings.EndSchedule)
                .AddAfter();

            //// CUSTOMIZATION - Uncomment the example below
            // Example: Add other events to the process deactivation.
            // Uncomment the lines below if needed.
            // point.Events.CreateCommandEvent().SetCommandText("Some other command").AddAfter();
            // point.Events.CreateCommentEvent().SetCommentText("Some comment").AddAfter();
        }

        /// <summary>
        ///     Adds the Weave start event.
        /// </summary>
        /// <param name="operation">
        ///     The operation.
        /// </param>
        /// <param name="point">
        ///     The point.
        /// </param>
        internal virtual void AddWeaveStartEvent(IOperation operation, IPoint point)
        {
            if (operation.Menus.WeldingSettings.IsWeavingEnabled == IsWeavingEnabled.Disabled)
            {
                return;
            }

            // Initialize the event
            // Example: Weave Sine[1]
            // Set event parameters to respective menu values
            // Define the event index (before/after)
            var weaveStartEvent = point.Events.CreateWeaveStartEvent();

            weaveStartEvent
                .SetWeaveStartCommand(operation.Menus.WeldingSettings.WeavingStartCommand)
                .SetWeavePattern(operation.Menus.WeldingSettings.WeavePattern.ToString())
                .SetWeaveSchedule(operation.Menus.WeldingSettings.WeaveSchedule)
                .AddAfter(); // Add event before the point.
        }

        /// <summary>
        ///     Adds the Weave End event.
        /// </summary>
        /// <param name="operation">
        ///     The operation.
        /// </param>
        /// <param name="point">
        ///     The point.
        /// </param>
        internal virtual void AddWeaveEndEvent(IOperation operation, IPoint point)
        {
            if (operation.Menus.WeldingSettings.IsWeavingEnabled == IsWeavingEnabled.Disabled)
            {
                return;
            }

            // Initialize the event
            // Example: Weave End
            // Set event parameters to respective menu values
            // Define the event index (before/after)
            var weaveEndEvent = point.Events.CreateWeaveEndEvent();

            weaveEndEvent
                .SetWeaveEndCommand(operation.Menus.WeldingSettings.WeavingEndCommand)
                .AddAfter();
        }

        /// <summary>
        ///     Adds the Seam tracking start event.
        /// </summary>
        /// <param name="operation">
        ///     The operation.
        /// </param>
        /// <param name="point">
        ///     The point.
        /// </param>
        internal virtual void AddSeamTrackingStartEvent(IOperation operation, IPoint point)
        {
            if (operation.Menus.WeldingSettings.IsSeamTrackingEnabled == IsSeamTrackingEnabled.Disabled)
            {
                return;
            }

            // Initialize the event
            // Example: Track TAST[1] RPM[2]
            // Set event parameters to respective menu values
            // Define the event index (before/after)
            var seamTrackingStartEvent = point.Events.CreateSeamTrackingStartEvent();

            seamTrackingStartEvent
                .SetSeamTrackingStartCommand(operation.Menus.WeldingSettings.SeamTrackingStartCommand)
                .SetSeamTrackingSchedule(operation.Menus.WeldingSettings.SeamTrackingSchedule)
                .SetRpmCommand(operation.Menus.WeldingSettings.RpmCommand)
                .SetRpmRegister(operation.Menus.WeldingSettings.RpmRegister)
                .AddAfter();
        }

        /// <summary>
        /// Adds the Seam Tracking End event.
        /// </summary>
        /// <param name="operation">
        ///     The operation.
        /// </param>
        /// <param name="point">
        ///     The point.
        /// </param>
        internal virtual void AddSeamTrackingEndEvent(IOperation operation, IPoint point)
        {
            if (operation.Menus.WeldingSettings.IsSeamTrackingEnabled == IsSeamTrackingEnabled.Disabled)
            {
                return;
            }

            // Initialize the event
            // Example: Weave End
            // Set event parameters to respective menu values
            // Define the event index (before/after)
            var seamTrackingEndEvent = point.Events.CreateSeamTrackingEndEvent();

            seamTrackingEndEvent
                .SetSeamTrackingEndCommand(operation.Menus.WeldingSettings.SeamTrackingEndCommand)
                .AddAfter();
        }

        /// <summary>
        ///     Adds the macro event.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void AddMacroEvent(IOperation operation, IPoint point)
        {
            if (operation is IMacroOperation macroOperation)
            {
                //// Add the macro call event
                // Initialize the event
                // Example: CALL MACRO1
                var macroEvent = point.Events
                    .CreateCommandEvent()
                    .SetCommandText($"CALL {macroOperation.MacroName}");

                // Add macro call event to point
                macroEvent.AddBefore();
            }

            //// CUSTOMIZATION - Uncomment the example below
            //// Example: Add an additional comment to the macro call.
            // point.Events.CreateCommentEvent().SetCommentText($"Default macro comment").AddAfter();
        }
    }
}
