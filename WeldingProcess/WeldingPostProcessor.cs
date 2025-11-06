// <copyright file="WeldingPostProcessor.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc.WeldingProcess
{
    using System.Linq;
    using Robotmaster.Processor.Core.Common.Enums;
    using Robotmaster.Processor.Core.Common.Interfaces.Events;
    using Robotmaster.Processor.Core.Common.Interfaces.FileFramework;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Events.Interfaces;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.TouchSensingSettings;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <inheritdoc/>
    internal class WeldingPostProcessor : PostProcessor
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="WeldingPostProcessor"/> class.
        ///     This class inherits from <see cref="PostProcessor"/> therefore all <see cref="PostProcessor"/>'s
        ///     fields, properties and methods are accessible and overridable here.
        /// </summary>
        /// <param name="program">
        ///     The current program.
        /// </param>
        /// <param name="context">
        ///     The current context.
        /// </param>
        internal WeldingPostProcessor(IProgram program, IPostProcessorContext context)
            : base(program, context)
        {
            //// BASE - Default constructor - LEAVE EMPTY
        }

        /// <inheritdoc/>
        internal override bool IsRobotProgramInputValid()
        {
            //// BASE - Default output
            var isProgramInputValid = base.IsRobotProgramInputValid();

            //// WELDING

            var touchGroupCount = this.RobotProgram
                .Operations
                .Where(op => op.ApplicationData.TouchSensing != null)
                .SelectMany(op => op.ApplicationData.TouchSensing.TouchGroups)
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

        /// <inheritdoc/>
        internal override void OutputBeforePointEvent(IOperation operation, IPoint point, IEvent beforeEvent)
        {
            //// TOUCH SENSING

            // Outputs the touch macro events (include IHS)
            // Example:
            //      SEARCH_RM(P[3],P[4],10,2)
            if (beforeEvent is ISearchMacro searchMacro)
            {
                var pointNumberFormater = this.PointNumber;
                searchMacro.MacroPositionsArguments = $"P[{this.PointNumber.Value}],P[{this.PointNumber.Value + 1}]";
            }

            base.OutputBeforePointEvent(operation, point, beforeEvent);
        }

        /// <inheritdoc/>
        internal override void OutputAfterPointEvents(IOperation operation, IPoint point)
        {
            //// BASE - Default output
            base.OutputAfterPointEvents(operation, point); // DO NOT REMOVE
        }

        /// <inheritdoc/>
        internal override void OutputInlineEvents(IOperation operation, IPoint point)
        {
            //// BASE - Default output
            base.OutputInlineEvents(operation, point);
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
        internal override string FormatSpeed(IOperation operation, IPoint point)
        {
            if (operation.ApplicationData.Welding is null)
            {
                return base.FormatSpeed(operation, point);
            }

            //// If point is IN process, point without search direction, and previous point does not have search direction
            //// 20:L P[13] WELD_SPEED CNT100 ;
            return point.Flags.IsInProcess && point.ApplicationData.Welding != null
                ? " WELD_SPEED"
                : base.FormatSpeed(operation, point);
        }

        /// <inheritdoc/>
        internal override void OutputFileHeader(ITextFile file, IOperation operation, IPoint point)
        {
            base.OutputFileHeader(file, operation, point);

            /*
               /APPL
                   ARC Welding Equipment Number : 1;
            */
            file.RootSection.Header
                    .WriteLine("/APPL")
                    .WriteLine($"  {operation.Menus.WeldingSettings.ApplicationName};");
        }

        //// CUSTOMIZATION
        //// If needed, add other base class method overrides below.
        //// Type "Override..." to see the possible methods to override or extend.
        //// Example: - Uncomment the example below
        ////
        ////internal override void OutputBeforeRobotProgram()
        ////{
        ////    //// BASE - Default output
        ////    base.OutputBeforeRobotProgram(); // DO NOT REMOVE
        ////
        ////    //// CUSTOMIZATION - Uncomment the example below
        ////    this.MoveSection
        ////        .Write(this.LineNumber.Increment())
        ////        .WriteLine($"  ! Write something at the start of a robot program ;");
        ////}
    }
}