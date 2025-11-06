// <copyright file="WeldingMainProcessorTouch.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc.WeldingProcess
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Robotmaster.Math.Algebra;
    using Robotmaster.Processor.Core.Common.Enums;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.TouchSensingSettings;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces.Applications.Operations;

    /// <summary>
    ///     Multi touch section of the main processor.
    ///     This section is part of the main processor class therefore
    ///     all the fields, properties and methods are accessible here.
    /// </summary>
    internal partial class WeldingMainProcessor
    {
        /// <summary>
        ///     Detect the closest user frame XYZ signed search direction based on the path direction.
        /// </summary>
        /// <param name="point">Search point.</param>
        /// <returns>XYZ signed search direction.</returns>
        internal static string CalculateSearchDirection(IPoint point)
        {
            double[] pathDirections =
            {
                Math.Abs(point.PathDirection.X), Math.Abs(point.PathDirection.Y), Math.Abs(point.PathDirection.Z),
            };

            var directionIndex = Array.IndexOf(pathDirections, pathDirections.Max());

            switch (directionIndex)
            {
                case 0:
                    return point.PathDirection.X > 0 ? "X" : "-X";

                case 1:
                    return point.PathDirection.Y > 0 ? "Y" : "-Y";

                case 2:
                    return point.PathDirection.Z > 0 ? "Z" : "-Z";

                default:
                    return string.Empty;
            }
        }

        /// <summary>
        ///     Gets the touch group index.
        /// </summary>
        /// <param name="touchGroup">
        ///     The touch group.
        /// </param>
        /// <returns>
        ///     The touch group index.
        /// </returns>
        internal virtual int GetTouchGroupProgramIndex(ITouchGroup touchGroup)
        {
            return
                this.RobotProgram.FirstOperation.Menus.ControllerSettings.PositionRegistryStartIndex +
                this.RobotProgram
                    .Operations
                    .Where(op => op.ApplicationData.TouchSensing != null)
                    .SelectMany(op => op.ApplicationData.TouchSensing.TouchGroups)
                    .Select((tg, index) => (TouchGroup: tg, Index: index))
                    .FirstOrDefault(tuple => tuple.TouchGroup == touchGroup).Index;
        }

        /// <summary>
        ///     Calculates the average position of a given touch group.
        /// </summary>
        /// <param name="touchGroup">
        ///     Touch group.
        /// </param>
        /// <returns>The average position.</returns>
        internal virtual Vector3 AverageTouchGroupPosition(ITouchGroup touchGroup)
        {
            Vector3 averagePosition = default;

            foreach (var touch in touchGroup.Touches)
            {
                averagePosition += touch.TouchPoint.Position / touchGroup.Touches.Count;
            }

            return averagePosition;
        }

        /// <summary>
        /// Modifies a touch sensing operation.
        /// </summary>
        /// <param name="operation">The operation.</param>
        internal virtual void EditTouchSensingOperation(IOperation operation)
        {
            foreach (var touchGroup in operation.ApplicationData.TouchSensing.TouchGroups)
            {
                if (operation.Menus.TouchSensingSettings.TouchSearchOutput == TouchSearchOutput.SearchCommand)
                {
                    //// LEGACY TOUCH SENSING (ex: Search[-Z])

                    // Before the search origin point
                    // Add an IHS search event
                    // Example:
                    //     Search Start[1,1] PR[50];
                    touchGroup.Touches[0].TouchPoint.Events
                        .CreateSearchStartEvent()
                        .SetPositionRegisterNum(this.GetTouchGroupProgramIndex(touchGroup))
                        .SetSchedule(operation.Menus.TouchSensingSettings.Schedule)
                        .AddBefore();

                    foreach (var tp in touchGroup.Touches)
                    {
                        // Inline with the search origin point
                        // Add an IHS search direction event
                        // Example:
                        //     Search[-Z]
                        tp.TouchPoint.Events
                            .CreateSearchDirectionEvent()
                            .SetDirection(CalculateSearchDirection(tp.TouchPoint)).AddInline();
                    }

                    // The search target point (theoretical point of contact) will be commented out.
                    // Add a search off event after this point.
                    // Example:
                    //     Search End;
                    touchGroup.Touches[touchGroup.Touches.Count - 1].TouchPoint.Events.CreateSearchEndEvent()
                        .AddAfter();
                }
                else if (operation.Menus.TouchSensingSettings.TouchSearchOutput == TouchSearchOutput.SearchMacro)
                {
                    //// Reset macro registers
                    touchGroup.Touches[0].TouchPoint.Events
                        .CreateCommandEvent()
                        .SetCommandText(
                            $"PR[{operation.Menus.TouchSensingSettings.TouchSearchRegistry}]=LPOS-LPOS")
                        .AddBefore();

                    touchGroup.Touches[0].TouchPoint.Events
                        .CreateCommandEvent()
                        .SetCommandText(
                            $"PR[{operation.Menus.TouchSensingSettings.TouchSearchRegistry + 1}]=LPOS-LPOS")
                        .AddBefore();

                    var touchMacroRegistry = operation.Menus.TouchSensingSettings.TouchSearchRegistry;

                    foreach (var tp in touchGroup.Touches)
                    {
                        //// TOUCH SENSING
                        // Adds one touch search macro event
                        // Example:
                        //   SEARCH_RM(P[3],P[4],10,2) ;
                        tp.TouchPoint
                            .Events
                            .CreateSearchMacroEvent()
                            .SetMacroName(operation.Menus.TouchSensingSettings.TouchMacroName)
                            .SetMacroArguments($"{tp.TouchPoint.Feedrate:0.0},{touchMacroRegistry + 1}")
                            .AddBefore();

                        // Add up touch offset of the group
                        // Example:
                        //   PR[5] = PR[5] + PR[6];
                        tp.TouchPoint
                            .Events
                            .CreateCommandEvent()
                            .SetCommandText($"PR[{touchMacroRegistry}]=PR[{touchMacroRegistry}]+PR[{touchMacroRegistry + 1}]")
                            .AddBefore();
                    }

                    // Store touch offset in the touch offset variable
                    touchGroup.Touches[touchGroup.Touches.Count - 1]
                        .TouchPoint
                        .Events
                        .CreateCommandEvent()
                        .SetCommandText($"PR[{this.GetTouchGroupProgramIndex(touchGroup)}]=PR[{touchMacroRegistry}]")
                        .AddAfter();
                }
            }
        }

        /// <summary>
        /// Modifies a welding operation.
        /// </summary>
        /// <param name="operation">The operation.</param>
        internal virtual void ResetTouchRegister(IOperation operation)
        {
            if (operation.Menus.TouchSensingSettings.TouchOffsetOutputType == TouchOffsetOutputType.Interpolated
                && operation.ApplicationData.Welding?.ReferencedTouchOperation?.ApplicationData?.TouchSensing != null
                && operation.ApplicationData.Welding.ReferencedTouchOperation.ApplicationData.TouchSensing.TouchGroups.Count > 1)
            {
                var cacheRegister = operation.Menus.TouchSensingSettings.InterpolatedTouchRegister;

                //// Reset cache registers
                //// Initialize PR[10] used for interpolation;
                operation.FirstPoint.Events
                    .CreateCommandEvent()
                    .SetCommandText(
                        $"PR[{cacheRegister}]=LPOS-LPOS")
                    .AddBefore();

                //// Initialize PR[11] used for interpolation
                operation.FirstPoint.Events
                    .CreateCommandEvent()
                    .SetCommandText(
                        $"PR[{cacheRegister + 1}]=LPOS-LPOS")
                    .AddBefore();

                //// Initialize PR[12] used for interpolation with arcs moves
                operation.FirstPoint.Events
                    .CreateCommandEvent()
                    .SetCommandText(
                        $"PR[{cacheRegister + 2}]=LPOS-LPOS")
                    .AddBefore();

                //// Initialize PR[13] used for interpolation with arcs moves
                operation.FirstPoint.Events
                    .CreateCommandEvent()
                    .SetCommandText(
                        $"PR[{cacheRegister + 3}]=LPOS-LPOS")
                    .AddBefore();
            }
        }

        /// <summary>
        ///     Add the displacement interpolation events between closest touch groups.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void EditPointForInterpolatedTouchOffset(IOperation operation, IPoint point)
        {
            if (operation.ApplicationData.Welding.ReferencedTouchOperation is null)
            {
                return;
            }

            // Get the closest touch groups
            (ITouchGroup TouchGroup, Vector3 Average, double Distance)[] closestTouchGroups =
                this.ClosestTouchGroups(point, operation.ApplicationData.Welding?.ReferencedTouchOperation).ToArray();

            IPoint pointForOffsetAssignmentEvents;
            var positionVariableArcIncrement = 0;

            // Handle the case where the current point is an end point of an arc
            if (point.MotionType == PointMotionType.Circular && !point.IsArcMiddlePoint)
            {
                // Calculation and assignments need to be done 1 point earlier
                pointForOffsetAssignmentEvents = point.PreviousPoint;

                // Shift by 2 since 2 temporary variables are used for interpolation
                positionVariableArcIncrement = 2;
            }
            else
            {
                pointForOffsetAssignmentEvents = point;
            }

            if (closestTouchGroups.Length == 1)
            {
                (ITouchGroup TouchGroup, Vector3 Average, double Distance)[] previousClosestTouchGroups =
                    this.ClosestTouchGroups(point.PreviousPoint, operation.ApplicationData.Welding?.ReferencedTouchOperation).ToArray();

                // Output the registry assignment only if the closest touch group has changed
                if (previousClosestTouchGroups.Length != 1
                    || closestTouchGroups[0].TouchGroup != previousClosestTouchGroups[0].TouchGroup)
                {
                    // Add events to point.
                    // Example: PR[10] = PR[50];
                    pointForOffsetAssignmentEvents.Events
                        .CreateCommandEvent()
                        .SetCommandText(
                            $"PR[{operation.Menus.TouchSensingSettings.InterpolatedTouchRegister}] = PR[{this.GetTouchGroupProgramIndex(closestTouchGroups[0].TouchGroup)}]")
                        .AddBefore();
                }

                // Add point offset event.
                // Example: L P[7] 14mm/sec CNT100 Offset,PR[10]    ;
                point.Events
                    .CreatePointOffsetEvent()
                    .SetPositionRegisterNumber(operation.Menus.TouchSensingSettings.InterpolatedTouchRegister)
                    .AddInline();
            }
            else if (closestTouchGroups.Length == 2)
            {
                // Generate Events to modify Point Register (PR[]) based on interpolation ratio
                var interpolatedTouchRegister = operation.Menus.TouchSensingSettings.InterpolatedTouchRegister + positionVariableArcIncrement;
                var firstTouchGroupRegister = this.GetTouchGroupProgramIndex(closestTouchGroups[0].TouchGroup);
                var secondTouchGroupRegister = this.GetTouchGroupProgramIndex(closestTouchGroups[1].TouchGroup);
                var interpolationRatio = CalculateInterpolationRatio(operation, point, closestTouchGroups);

                // Only modify point register elements, 1 to 3 (xyz)
                for (int i = 1; i <= 3; i++)
                {
                    //// Example: PR[10, 1] = PR[5, 1] * 0.57;
                    pointForOffsetAssignmentEvents.Events
                        .CreateCommandEvent()
                        .SetCommandText(
                            $"PR[{interpolatedTouchRegister}, {i}] = PR[{firstTouchGroupRegister}, {i}] * {interpolationRatio:0.000}")
                        .AddBefore();

                    //// Example: PR[11, 1] = PR[4, 1] * 0.33;
                    pointForOffsetAssignmentEvents.Events
                        .CreateCommandEvent()
                        .SetCommandText(
                            $"PR[{interpolatedTouchRegister + 1}, {i}] = PR[{secondTouchGroupRegister}, {i}] * {1 - interpolationRatio:0.000}")
                        .AddBefore();

                    //// Example: PR[10, 1] = PR[10, 1] + PR[11, 1];
                    pointForOffsetAssignmentEvents.Events
                        .CreateCommandEvent()
                        .SetCommandText(
                            $"PR[{interpolatedTouchRegister}, {i}] = PR[{interpolatedTouchRegister}, {i}] + PR[{interpolatedTouchRegister + 1}, {i}]")
                        .AddBefore();
                }

                // Add point offset event.
                // Example:
                //      L P[7] 14mm/sec CNT100 Offset,PR[10]    ;
                // or
                //      C P[9] Offset,PR[10]
                //        P[10] 14mm / sec CNT100 Offset, PR[11]
                point.Events
                    .CreatePointOffsetEvent()
                    .SetPositionRegisterNumber(interpolatedTouchRegister)
                    .AddInline();
            }
        }

        /// <summary>
        ///     Adds the shift switch events.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void EditPointForSequentialTouchOffset(IOperation operation, IPoint point)
        {
            if (operation.ApplicationData.Welding.ReferencedTouchOperation is null)
            {
                return;
            }

            var closestTouchGroup = this.ClosestTouchGroups(point, operation.ApplicationData.Welding?.ReferencedTouchOperation).FirstOrDefault().TouchGroup;
            if (closestTouchGroup != null)
            {
                // Add point offset event.
                point.Events
                    .CreatePointOffsetEvent()
                    .SetPositionRegisterNumber(this.GetTouchGroupProgramIndex(closestTouchGroup))
                    .AddInline();
            }
        }

        /// <summary>
        ///     Gets the two closest touch groups of a touch sensing operation.
        /// </summary>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <param name="touchOperation">
        ///     The referenced touch sensing operation.
        /// </param>
        /// <returns>
        ///     The two closest touch groups and their distance to the point.
        /// </returns>
        internal virtual IEnumerable<(ITouchGroup TouchGroup, Vector3 Average, double Distance)> ClosestTouchGroups(
            IPoint point, IOperation touchOperation)
        {
            // Returns the closest touch group and the distance to the point
            return touchOperation?.ApplicationData.TouchSensing.TouchGroups
                .Select(tg => (TouchGroup: tg, Average: this.AverageTouchGroupPosition(tg)))
                .Select(tg => (tg.TouchGroup, tg.Average, Distance: (point.Position - tg.Average).Length))
                .OrderBy(tuple => tuple.Distance)
                .Take(2);
        }

        private static double CalculateInterpolationRatio(
            IOperation operation,
            IPoint point,
            IReadOnlyList<(ITouchGroup TouchGroup,
            Vector3 Average,
            double Distance)> closestTouchGroups)
        {
            // Calculate the vector between the two closest touch groups average positions
            var vectorTouch0To1 = new Vector3(closestTouchGroups[1].Average - closestTouchGroups[0].Average);

            // Calculate the vector between the closest touch group  average position and the current point
            var vectorTouch0ToPoint = new Vector3(point.Position - closestTouchGroups[0].Average);

            // Calculate the projected length between the two vectors
            var projectedLength = Vector3.DotProduct(vectorTouch0To1, vectorTouch0ToPoint) / vectorTouch0To1.Length;

            // Calculate the interpolation ratio
            // 1   = the current point is closer to the first touch group
            // 0.5 = the current point is about to be closer to the second touch group
            // 0   = the current point is closer to the second touch group.
            //       This case cannot not happen because the closest touch group list is ordered by distance.
            switch (operation.Menus.TouchSensingSettings.TouchInterpolation)
            {
                case TouchInterpolation.Linear:
                    return 1 - (projectedLength / vectorTouch0To1.Length);

                case TouchInterpolation.SmoothStep when projectedLength < 0:
                    return 1;

                case TouchInterpolation.SmoothStep when projectedLength > vectorTouch0To1.Length:
                    // Cannot happen
                    return 0;

                case TouchInterpolation.SmoothStep:
                    var x = projectedLength / vectorTouch0To1.Length;

                    return 1 - (3 * x * x) + (2 * x * x * x);
            }

            return 0.0;
        }
    }
}