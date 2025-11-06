// <copyright file="CuttingMainProcessor.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc.CuttingProcess
{
    using System.Linq;
    using Robotmaster.Processor.Core.Common.Enums;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.ProcessActivationSettings;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.ToolChangeSettings;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <summary>
    ///     The Cutting main processor.
    /// </summary>
    internal partial class CuttingMainProcessor : MainProcessor
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CuttingMainProcessor"/> class.
        ///     This class inherits from <see cref="MainProcessor"/> therefore all <see cref="MainProcessor"/>'s
        ///     fields, properties and methods are accessible and override-able here.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="context">
        ///     The current context.
        /// </param>
        internal CuttingMainProcessor(IOperation operation, IMainProcessorContext context)
            : base(operation, context)
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

        /// <summary>
        ///     Edits the process activation and deactivation at the operation level based on the process activation menu.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void EditOperationProcessActivationDeactivation(IOperation operation)
        {
            IPoint point;
            switch (operation.Menus.ProcessActivationSettings.ProcessCondition)
            {
                case ProcessCondition.EveryProgram:
                    if (operation.Flags.IsFirstTaskOperation)
                    {
                        point = this.FindFirstProcessActivationPoint(operation);
                        this.AddProcessActivationEvent(operation, point);
                    }

                    if (operation.Flags.IsLastTaskOperation)
                    {
                        point = this.FindLastProcessDeactivationPoint(operation);
                        this.AddProcessDeactivationEvent(operation, point);
                    }

                    break;
                case ProcessCondition.EveryOperation when operation.OperationType == OperationType.TaskOperation:
                    point = this.FindFirstProcessActivationPoint(operation);
                    this.AddProcessActivationEvent(operation, point);
                    point = this.FindLastProcessDeactivationPoint(operation);
                    this.AddProcessDeactivationEvent(operation, point);
                    break;
                case ProcessCondition.EveryToolChange:
                    if (operation.OperationType == OperationType.TaskOperation
                                                           && operation.PreviousTaskOperation?.Tool.ToolNumber != operation.Tool.ToolNumber)
                    {
                        point = this.FindFirstProcessActivationPoint(operation);
                        this.AddProcessActivationEvent(operation, point);
                    }

                    if (operation.OperationType == OperationType.TaskOperation
                                                           && operation.Tool.ToolNumber != operation.NextTaskOperation?.Tool.ToolNumber)
                    {
                        point = this.FindLastProcessDeactivationPoint(operation);
                        this.AddProcessDeactivationEvent(operation, point);
                    }

                    break;
                case ProcessCondition.EveryPath:
                    // Do nothing here. See EditPointProcessActivationDeactivation ran at every point.
                    break;
            }
        }

        /// <summary>
        ///     Edits the tool change based on the tool change menu.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void EditOperationToolChange(IOperation operation)
        {
            switch (operation.Menus.ToolChangeSettings.ToolChangeCondition)
            {
                case ToolChangeCondition.Enable:
                case ToolChangeCondition.IfNeeded when this.RobotProgram.Operations.Select(op => op.Tool.ToolNumber).Distinct().Count() > 1:
                    if (this.IsToolChangeNeeded(operation))
                    {
                        this.AddToolChangeEvent(operation, operation.FirstPoint);
                    }

                    break;
                case ToolChangeCondition.Disable:
                default:
                    break;
            }
        }

        /// <summary>
        ///     Edits the macro call based on the tool change menu.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void EditOperationMacroCall(IOperation operation)
        {
            if (operation.Menus.ToolChangeSettings.IsMacroNameCallEnabled &&
                this.IsMacroCallNeeded(operation))
            {
                this.AddMacroEvent(operation, operation.FirstPoint);
            }
        }

        /// <summary>
        ///      Gets a value indicating whether an operation would need a tool change event.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <returns>
        ///     <c>true</c> if the operation needs a tool change event; otherwise, <c>false</c>.
        /// </returns>
        internal virtual bool IsToolChangeNeeded(IOperation operation)
        {
            var anyMacroAfterHome = this.RobotProgram.Operations.TakeWhile(op => !op.Flags.IsFirstTaskOperation)
               .Any(op => op is IMacroOperation);

            var macroExitOperationAfterHome = this.RobotProgram.Operations.TakeWhile(op => !op.Flags.IsFirstTaskOperation)
                .LastOrDefault(op => op is IMacroOperation);

            // Home when there is no macro after the first home
            if (!anyMacroAfterHome
                && operation.Flags.IsFirstHomeOperation)
            {
                return true;
            }

            // Macro entry operation when there is macro after the first home
            if (operation == macroExitOperationAfterHome)
            {
                return true;
            }

            // Any other operation with a real Robotmaster tool change
            if (!operation.Flags.IsFirstHomeOperation
                && operation.Tool.ToolNumber != operation.PreviousOperation.Tool.ToolNumber)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///      Gets a value indicating whether an operation would need a macro event.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <returns>
        ///     <c>true</c> if the operation needs a macro event; otherwise, <c>false</c>.
        /// </returns>
        internal virtual bool IsMacroCallNeeded(IOperation operation)
        {
            // Macro exit operation when the menu parameter is enabled and when a tool change is not needed
            if (operation is IMacroOperation macroOperation
                && macroOperation.IsExitOperation
                && !this.IsToolChangeNeeded(operation))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Edits the process activation and deactivation at the point level based on the process activation menu.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void EditPointProcessActivationDeactivation(IOperation operation, IPoint point)
        {
            if (operation.OperationType == OperationType.TaskOperation &&
                operation.Menus.ProcessActivationSettings.ProcessCondition == ProcessCondition.EveryPath)
            {
                if (this.IsProcessActivationPoint(operation, point))
                {
                    this.AddProcessActivationEvent(operation, point);
                }

                if (this.IsProcessDeactivationPoint(operation, point))
                {
                    this.AddProcessDeactivationEvent(operation, point);
                }
            }
        }

        /// <summary>
        ///     Finds the first process activation point of an <paramref name="operation"/> based on the process activation menu.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <returns>
        ///     The first process activation point.
        /// </returns>
        internal virtual IPoint FindFirstProcessActivationPoint(IOperation operation)
        {
            switch (operation.Menus.ProcessActivationSettings.ProcessActivationCondition)
            {
                case ProcessActivationCondition.FirstPoint:
                    return operation.FirstPoint;
                case ProcessActivationCondition.FirstNonJointMove:
                    return operation.PointsOfInterest.VeryFirstNonJointMove;
                case ProcessActivationCondition.FirstPlunge:
                    return operation.PointsOfInterest.VeryFirstPlungeMove;
                case ProcessActivationCondition.FirstPointOfContact:
                    return operation.PointsOfInterest.VeryFirstPointOfContact;
                case ProcessActivationCondition.FirstMoveInProcess:
                    return operation.PointsOfInterest.VeryFirstMoveInProcess;
                default:
                    return null;
            }
        }

        /// <summary>
        ///     Edits last process deactivation point of an <paramref name="operation"/> based on the process activation menu.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <returns>
        ///     The last process deactivation point.
        /// </returns>
        internal virtual IPoint FindLastProcessDeactivationPoint(IOperation operation)
        {
            switch (operation.Menus.ProcessActivationSettings.ProcessDeactivationCondition)
            {
                case ProcessDeactivationCondition.LastPoint:
                    return operation.LastPoint;
                case ProcessDeactivationCondition.LastNonJointMove:
                    return operation.PointsOfInterest.VeryLastNonJointMove;
                case ProcessDeactivationCondition.LastRetract:
                    return operation.PointsOfInterest.VeryLastRetractMove;
                case ProcessDeactivationCondition.LastPointOfContact:
                    return operation.PointsOfInterest.VeryLastPointOfContact;
                case ProcessDeactivationCondition.LastMoveInProcess:
                    return operation.PointsOfInterest.VeryLastMoveInProcess;
                default:
                    return null;
            }
        }

        /// <summary>
        ///     Gets a value indicating whether the <paramref name="point"/> is a process activation point based on the process activation menu.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     <c>true</c> if the <paramref name="point" /> is a process activation point; otherwise, <c>false</c>.
        /// </returns>
        internal virtual bool IsProcessActivationPoint(IOperation operation, IPoint point)
        {
            switch (operation.Menus.ProcessActivationSettings.ProcessActivationCondition)
            {
                case ProcessActivationCondition.FirstPoint:
                    return point == operation.FirstPoint;
                case ProcessActivationCondition.FirstNonJointMove:
                    return point.Flags.IsFirstNonJointMove;
                case ProcessActivationCondition.FirstPlunge:
                    return point.Flags.IsPlungeMove;
                case ProcessActivationCondition.FirstPointOfContact:
                    return point.Flags.IsFirstPointOfContact;
                case ProcessActivationCondition.FirstMoveInProcess:
                    return point.Flags.IsFirstMoveInProcess;
                default:
                    return false;
            }
        }

        /// <summary>
        ///     Gets a value indicating whether the <paramref name="point"/> is process deactivation point based on the process activation menu.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     <c>true</c> if <paramref name="point" /> is a process deactivation point; otherwise, <c>false</c>.
        /// </returns>
        internal virtual bool IsProcessDeactivationPoint(IOperation operation, IPoint point)
        {
            switch (operation.Menus.ProcessActivationSettings.ProcessDeactivationCondition)
            {
                case ProcessDeactivationCondition.LastPoint:
                    return point == operation.LastPoint;
                case ProcessDeactivationCondition.LastNonJointMove:
                    return point.Flags.IsLastNonJointMove;
                case ProcessDeactivationCondition.LastRetract:
                    return point.Flags.IsRetractMove;
                case ProcessDeactivationCondition.LastPointOfContact:
                    return point.Flags.IsLastPointOfContact;
                case ProcessDeactivationCondition.LastMoveInProcess:
                    return point.Flags.IsLastMoveInProcess;
                default:
                    return false;
            }
        }

        /// <summary>
        ///     Edits plunge points if they are in-process.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void EditPointPrecisionMotionOverrides(IOperation operation, IPoint point)
        {
            if (point.MotionType != PointMotionType.Linear)
            {
                return;
            }

            if (point.Flags.IsPlungeMove
                && operation.Menus.PrecisionMotionOverrides.PlungeDelayTime > 0)
            {
                this.AddDelayEvent(operation, point, operation.Menus.PrecisionMotionOverrides.PlungeDelayTime);
                return;
            }

            if (IsSharpTurn(operation, point)
                && operation.Menus.PrecisionMotionOverrides.SharpTurnDelayTime > 0)
            {
                this.AddDelayEvent(operation, point, operation.Menus.PrecisionMotionOverrides.SharpTurnDelayTime);
                return;
            }

            if (IsSmallMove(operation, point)
                && operation.Menus.PrecisionMotionOverrides.SmallMoveDelayTime > 0)
            {
                this.AddDelayEvent(operation, point, operation.Menus.PrecisionMotionOverrides.SmallMoveDelayTime);
                return;
            }
        }
    }
}
