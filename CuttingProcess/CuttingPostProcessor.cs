// <copyright file="CuttingPostProcessor.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc.CuttingProcess
{
    using Robotmaster.Processor.Core.Common.Enums;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.PrecisionMotionOverrides;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <summary>
    ///     The Cutting post processor.
    /// </summary>
    internal class CuttingPostProcessor : PostProcessor
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CuttingPostProcessor"/> class.
        ///     This class inherits from <see cref="PostProcessor"/> therefore all <see cref="PostProcessor"/>'s
        ///     fields, properties, and methods are accessible and override-able here.
        /// </summary>
        /// <param name="program">
        ///     The current program.
        /// </param>
        /// <param name="context">
        ///     The current context.
        /// </param>
        internal CuttingPostProcessor(IProgram program, IPostProcessorContext context)
            : base(program, context)
        {
            //// BASE - Default constructor - LEAVE EMPTY
        }

        /// <summary>
        ///     Checks if the given point is at a sharp turn.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     True if the angle between path directions at the given point is above a minimum angle threshold.
        /// </returns>
        internal static bool IsSharpTurn(IOperation operation, IPoint point)
        {
            double sharpAngle = operation.Menus.PrecisionMotionOverrides.SharpTurnAngle;

            if (point.Flags.IsInProcess
                && point != operation.LastPoint
                && point.PathDirection.AngleBetween(point.NextPoint.PathDirection) >= sharpAngle)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Checks if the given point is a small move.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     True if the distance between the previous point and the current point is below a certain distance in mm.
        /// </returns>
        internal static bool IsSmallMove(IOperation operation, IPoint point)
        {
            double smallDistance = operation.Menus.PrecisionMotionOverrides.SmallMoveDistance;

            if (point.Flags.IsInProcess
                && point != operation.FirstPoint
                && point.Position.GetDistanceFrom(point.PreviousPoint.Position) <= smallDistance)
            {
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        internal override void OutputBeforePointEvents(IOperation operation, IPoint point)
        {
            //// BASE - Default output
            base.OutputBeforePointEvents(operation, point); // DO NOT REMOVE

            //// CUTTING
            // Force the output of the User Frame and the Tool Frame after a tool change event.
            if (point.Events.BeforeEvents.HasToolChangeEvent)
            {
                this.OutputUserFrame(operation);
                this.OutputToolFrame(operation);
            }
        }

        /// <inheritdoc/>
        internal override void OutputAfterPointEvents(IOperation operation, IPoint point)
        {
            //// BASE - Default output
            base.OutputAfterPointEvents(operation, point); // DO NOT REMOVE

            //// CUTTING
            // Force the output of the User Frame and the Tool Frame after a tool change.
            if (point.Events.AfterEvents.HasToolChangeEvent)
            {
                this.OutputUserFrame(operation);
                this.OutputToolFrame(operation);
            }
        }

        /// <summary>
        ///     Formats the Positioning Path at a given point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The formatted Positioning Path.
        /// </returns>
        internal override string FormatPositioningPath(IOperation operation, IPoint point)
        {
            //// Example: CNT100
            if (point.MotionType != PointMotionType.Linear)
            {
                return base.FormatPositioningPath(operation, point);
            }

            if (operation.Menus.PrecisionMotionOverrides.EnablePlungeMotionOverride
                && point.Flags.IsPlungeMove)
            {
                switch (operation.Menus.PrecisionMotionOverrides.PlungeTerminationOverride)
                {
                    case PlungeTerminationOverride.Fine:
                        return " FINE";
                    case PlungeTerminationOverride.Continuous:
                        return $" CNT{operation.Menus.PrecisionMotionOverrides.PlungeCntPositioningPathOverride}";
                    case PlungeTerminationOverride.NotSpecified:
                    default:
                        return string.Empty;
                }
            }

            if (operation.Menus.PrecisionMotionOverrides.EnableSharpTurnMotionOverride
                && IsSharpTurn(operation, point))
            {
                switch (operation.Menus.PrecisionMotionOverrides.SharpTurnTerminationOverride)
                {
                    case SharpTurnTerminationOverride.Fine:
                        return " FINE";
                    case SharpTurnTerminationOverride.Continuous:
                        return $" CNT{operation.Menus.PrecisionMotionOverrides.SharpTurnCntPositioningPathOverride}";
                    case SharpTurnTerminationOverride.NotSpecified:
                    default:
                        return string.Empty;
                }
            }

            if (operation.Menus.PrecisionMotionOverrides.EnableSmallMoveMotionOverride
                && IsSmallMove(operation, point))
            {
                switch (operation.Menus.PrecisionMotionOverrides.SmallMoveTerminationOverride)
                {
                    case SmallMoveTerminationOverride.Fine:
                        return " FINE";
                    case SmallMoveTerminationOverride.Continuous:
                        return $" CNT{operation.Menus.PrecisionMotionOverrides.SmallMoveCntPositioningPathOverride}";
                    case SmallMoveTerminationOverride.NotSpecified:
                    default:
                        return string.Empty;
                }
            }

            return base.FormatPositioningPath(operation, point);
        }

        /// <summary>
        ///     Formats the Acceleration at a given point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The formatted Acceleration.
        /// </returns>
        internal override string FormatAcceleration(IOperation operation, IPoint point)
        {
            //// Example: ACC100
            if (point.MotionType != PointMotionType.Linear)
            {
                return base.FormatAcceleration(operation, point);
            }

            if (operation.Menus.PrecisionMotionOverrides.EnablePlungeMotionOverride
                && point.Flags.IsPlungeMove
                && operation.Menus.PrecisionMotionOverrides.PlungeAccelerationAndDecelerationOverride > 0)
            {
                return $" ACC{operation.Menus.PrecisionMotionOverrides.PlungeAccelerationAndDecelerationOverride}";
            }

            if (operation.Menus.PrecisionMotionOverrides.EnableSharpTurnMotionOverride
                && IsSharpTurn(operation, point)
                && operation.Menus.PrecisionMotionOverrides.SharpTurnAccelerationAndDecelerationOverride > 0)
            {
                return $" ACC{operation.Menus.PrecisionMotionOverrides.SharpTurnAccelerationAndDecelerationOverride}";
            }

            if (operation.Menus.PrecisionMotionOverrides.EnableSmallMoveMotionOverride
                && IsSmallMove(operation, point)
                && operation.Menus.PrecisionMotionOverrides.SmallMoveAccelerationAndDecelerationOverride > 0)
            {
                return $" ACC{operation.Menus.PrecisionMotionOverrides.SmallMoveAccelerationAndDecelerationOverride}";
            }

            return base.FormatAcceleration(operation, point);
        }

        //// CUSTOMIZATION - Uncomment the example below
        //// Example: If needed, add other base class method overrides below.
        ////    Type "Override..." to see the possible methods to override or extend.
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