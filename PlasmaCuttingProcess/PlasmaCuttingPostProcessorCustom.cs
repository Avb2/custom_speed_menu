// <copyright file="PlasmaCuttingPostProcessorCustom.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc.PlasmaCuttingProcess
{
    using System.Linq;
    using Robotmaster.Processor.Core.Common.Interfaces.Events;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Events.Interfaces;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <content>
    ///     Easy-to-customize section of the post processor.
    ///     This customizable section is part of the main processor class therefore
    ///     all the fields, properties and methods are accessible here.
    /// </content>
    internal partial class PlasmaCuttingPostProcessor
    {
        /// <inheritdoc />
        internal override bool IsRobotProgramInputValid()
        {
            var isProgramInputValid = base.IsRobotProgramInputValid();

            var touchGroupCount = this.RobotProgram
                .Operations
                .Where(op => op.ApplicationData.PlasmaCutting != null)
                .SelectMany(op => op.ApplicationData.PlasmaCutting.TouchGroups)
                .Count();

            if (this.RobotProgram.FirstOperation.Menus.ControllerSettings.PositionRegistryStartIndex + touchGroupCount >
                this.RobotProgram.FirstOperation.Menus.ControllerSettings.PositionRegistryMaxIndex)
            {
                this.Context.NotifyUser(
                    "Not enough Position Registers available to store all the touch offsets in one program." +
                    "Reduce the number of touch offsets or split the program into multiple programs.");
                isProgramInputValid = false;
            }

            return isProgramInputValid;
        }

        /// <inheritdoc />
        internal override void OutputBeforeOperation(IOperation operation)
        {
            //// Default output (base)
            base.OutputBeforeOperation(operation); // DO NOT REMOVE

            //// Plasma cutting output (custom)
            //// CUSTOMIZATION - Insert customization below

            // Reset Slowdown (0 = Disable).
            this.PlasmaSpeedOverride = 0;
        }

        /// <inheritdoc/>
        internal override void OutputBeforePointEvent(IOperation operation, IPoint point, IEvent beforeEvent)
        {
            //// TOUCH SENSING

            // Outputs the touch macro events (include IHS)
            // Example:
            //      SEARCH_RM(P[3],P[4],10,2)
            if (beforeEvent is ISearchMacro searchMacro)
            {
                searchMacro.MacroPositionsArguments = $"P[{this.PointNumber.Value}],P[{this.PointNumber.Value + 1}]";
            }

            base.OutputBeforePointEvent(operation, point, beforeEvent);
        }

        /// <inheritdoc />
        internal override void OutputLinePrefix(IOperation operation, IPoint point)
        {
            //// TOUCH SENSING
            // Comment out search target points because they are either:
            // - managed by the touch macro. They will be argument of the search macro.
            // - or not output with the search command. They will be defined during mastering.
            if (point.PreviousPoint?.Events.InlineEvents.HasSearchDirectionEvent == true
                || point.Events.BeforeEvents.HasSearchMacroEvent)
            {
                this.MoveSection
                    .Write("  ! ");
            }
            else
            {
                //// Default output (base)
                base.OutputLinePrefix(operation, point);
            }
        }

        /// <inheritdoc/>
        internal override void OutputAfterPointEvent(IOperation operation, IPoint point, IEvent afterEvent)
        {
            //// PLASMA CUTTING
            // Overwrites the plasma speed when a speed override event is added.
            // This speed is later used when outputting the speed of all the following points.
            if (afterEvent is IPlasmaSpeedOverride plasmaSpeedOverrideEvent)
            {
                // Overwrites the plasma speed when a speed override event is added.
                // This speed is later used when outputting the speed of all the following points.
                this.PlasmaSpeedOverride = plasmaSpeedOverrideEvent.Speed;
            }

            // Reset speed on speed reset event
            if (afterEvent is IPlasmaSpeedReset)
            {
                this.PlasmaSpeedOverride = 0;
            }

            //// BASE - Default output
            base.OutputAfterPointEvent(operation, point, afterEvent);
        }
        //// CUSTOMIZATION
        //// If needed, add other base class method overrides below.
        //// Type "Override..." to see the possible methods to override or extend.
    }
}
