// <copyright file="CuttingMainProcessorCustom.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc.CuttingProcess
{
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.ProcessActivationSettings;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.ToolChangeSettings;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <summary>
    ///     Easy-to-customize section of the main processor.
    ///     This customizable section is part of the main processor class therefore
    ///     all the fields, properties, and methods are accessible here.
    /// </summary>
    internal partial class CuttingMainProcessor : MainProcessor
    {
        /// <inheritdoc/>
        internal override void EditOperation(IOperation operation)
        {
            //// BASE - Default processing
            base.EditOperation(operation); // DO NOT REMOVE

            //// CUTTING
            //// Default tool change implementation (operation level)
            this.EditOperationToolChange(operation);

            //// Default macro implementation (operation level)
            this.EditOperationMacroCall(operation);

            //// Default process activation and deactivation implementation (operation level)
            this.EditOperationProcessActivationDeactivation(operation);

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

            //// CUTTING - Default process activation and deactivation implementation (point level)
            this.EditPointProcessActivationDeactivation(operation, point);

            //// CUTTING - Default customization of corner points based on the In-Process motion customization menu
            this.EditPointPrecisionMotionOverrides(operation, point);

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
            //// CUTTING - Add the process activation event
            // Initialize the event
            // Example: CALL TOOLON
            // Define the event index (before/after)
            var processOnEvent = point.Events.CreateProcessOnEvent();
            processOnEvent.SetProcessActivationCommand(operation.Menus.ProcessActivationSettings.ProcessActivationMacro);

            if (operation.Menus.ProcessActivationSettings.ProcessActivationIndex == ProcessActivationIndex.After)
            {
                processOnEvent.AddAfter();
            }
            else
            {
                processOnEvent.AddBefore();
            }

            //// CUSTOMIZATION- Uncomment the example below
            //// Example: Get the spindle speed
            // double spindleSpeed = 0.0;
            // switch (operation.ApplicationType)
            // {
            //    case ApplicationType.Cutting:
            //        spindleSpeed = operation.ApplicationData.Cutting.SpindleSpeed;
            //        break;
            //    case ApplicationType.Cycle:
            //        spindleSpeed = operation.ApplicationData.Cycle.SpindleSpeed;
            //        break;
            //    default:
            //        break;
            // }

            //// CUSTOMIZATION - Uncomment the example below
            //// Example: Add other events to the process activation.
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
            //// CUTTING - Add the process deactivation event
            // Initialize the event
            // Example: CALL TOOLOFF
            // Define the event index (before/after)
            var processOffEvent = point.Events.CreateProcessOffEvent();
            processOffEvent.SetProcessDeactivationCommand(operation.Menus.ProcessActivationSettings.ProcessDeactivationMacro);

            if (operation.Menus.ProcessActivationSettings.ProcessDeactivationIndex == ProcessDeactivationIndex.After)
            {
                processOffEvent.AddAfter();
            }
            else
            {
                processOffEvent.AddBefore();
            }

            //// CUSTOMIZATION - Uncomment the example below
            //// Example: Add other events to the process deactivation.
            // point.Events.CreateCommandEvent().SetCommandText("Some other command").AddAfter();
            // point.Events.CreateCommentEvent().SetCommentText("Some comment").AddAfter();
        }

        /// <summary>
        ///     Adds a delay event using the WAIT instruction.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <param name="delayValue">
        ///     The value that will be used to set the event's DelayValue.
        /// </param>
        internal virtual void AddDelayEvent(IOperation operation, IPoint point, double delayValue)
        {
            //// CUTTING - Add a Delay event
            // Initialize the event
            // Example: WAIT = 2.0  ;
            // Define the event index (after)
            _ = operation;
            var delayEvent = point.Events.CreateDelayEvent();
            delayEvent.SetDelayValue(delayValue);
            delayEvent.AddAfter();

            //// CUSTOMIZATION - Uncomment the example below
            //// Example: Add other events to the delay event.
            // point.Events.CreateCommandEvent().SetCommandText("Some other command").AddAfter();
            // point.Events.CreateCommentEvent().SetCommentText("Some comment").AddAfter();
        }

        /// <summary>
        ///     Adds the tool change event.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void AddToolChangeEvent(IOperation operation, IPoint point)
        {
            //// CUTTING - Add the tool change event
            // Initialize the event
            // Example: CALL TOOL1
            var toolChangeEvent = point.Events.CreateToolChangeEvent();

            switch (operation.Menus.ToolChangeSettings.ToolChangeMacroNameType)
            {
                // Define Tool change command
                case ToolChangeMacroNameType.ToolName:
                    toolChangeEvent.SetToolChangeCommand(operation.Tool.Name);
                    break;
                case ToolChangeMacroNameType.CustomName:
                    toolChangeEvent.SetToolChangeCommand(
                        operation.Menus.ToolChangeSettings.ToolChangeMacroCustomName +
                        operation.Tool.ToolNumber);
                    break;
            }

            // Add tool change event to point
            if (operation == this.RobotProgram.FirstOperation
                && operation.Menus.ToolChangeSettings.FirstToolChangeOutputCondition == FirstToolChangeOutputCondition.AfterHome)
            {
                toolChangeEvent.AddAfter();
            }
            else
            {
                toolChangeEvent.AddBefore();
            }

            //// CUSTOMIZATION - Uncomment the example below
            //// Example: Add an additional tool comment to the tool change.
            // point.Events.CreateCommentEvent().SetCommentText($"NAME: {operation.Tool.Name} - LENGTH: {operation.Tool.Length} mm").AddAfter();
            // point.Events.CreateCommandEvent().SetCommandText("Some other command").AddAfter();
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
                //// CUTTING - Add the macro call event
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
