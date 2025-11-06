// <copyright file="PlasmaCuttingMainProcessorTouch.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc.PlasmaCuttingProcess
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Robotmaster.Math.Algebra;
    using Robotmaster.Processor.Core.Common.Enums;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.PlasmaCuttingSettings;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.TouchSensingSettings;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces.Applications.Operations;

    /// <summary>
    ///     Multi touch section of the main processor.
    ///     This section is part of the main processor class therefore
    ///     all the fields, properties and methods are accessible here.
    /// </summary>
    internal partial class PlasmaCuttingMainProcessor
    {
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
                    .Where(op => op.ApplicationData.PlasmaCutting != null)
                    .SelectMany(op => op.ApplicationData.PlasmaCutting.TouchGroups)
                    .Select((tg, index) => (TouchGroup: tg, Index: index))
                    .FirstOrDefault(tuple => tuple.TouchGroup == touchGroup).Index;
        }

        /// <summary>
        /// Modifies a welding operation.
        /// </summary>
        /// <param name="operation">
        ///     The operation.
        /// </param>
        internal virtual void ResetTouchRegister(IOperation operation)
        {
            if (operation.Menus.TouchSensingSettings.TouchOffsetOutputType == TouchOffsetOutputType.Interpolated
                && operation.ApplicationData.PlasmaCutting.TouchGroups.Count > 1)
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
        ///     Edits a touch sensing operation.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void EditPlasmaOperationTouchSection(IOperation operation)
        {
            // TOUCH SENSING - Add touch sensing group and touch events
            // Add events to point.
            foreach (var touchGroup in operation.ApplicationData.PlasmaCutting.TouchGroups)
            {
                // There is currently only one touch per touch group for plasmas cutting.
                var singleTouchInGroup = touchGroup.Touches.Single();
                var searchTargetPoint = singleTouchInGroup.TouchPoint;
                var searchOriginPoint = searchTargetPoint.PreviousPoint;

                var touchMacroRegistry = operation.Menus.TouchSensingSettings.TouchSearchRegistry;

                if (operation.Menus.TouchSensingSettings.TouchSearchOutput == TouchSearchOutput.SearchCommand)
                {
                    //// LEGACY TOUCH SENSING (ex: Search[-Z])

                    // Before the search origin point
                    // Add an IHS search event
                    // Example:
                    //     Search Start[1,1] PR[50];
                    searchOriginPoint.Events
                        .CreateSearchStartEvent()
                        .SetSchedule(operation.Menus.TouchSensingSettings.Schedule)
                        .SetPositionRegisterNum(this.GetTouchGroupProgramIndex(touchGroup))
                        .AddBefore();

                    // Inline with the search origin point
                    // Add an IHS search direction event
                    // Example:
                    //     Search[-Z]
                    searchOriginPoint.Events
                        .CreateSearchDirectionEvent()
                        .SetDirection(SearchDirectionCalculation(searchTargetPoint))
                        .AddInline();

                    // The search target point (theoretical point of contact) will be commented out.
                    // Add a search off event after this point.
                    // Example:
                    //     Search End;
                    searchTargetPoint.Events
                    .CreateSearchEndEvent()
                    .AddAfter();
                }
                else if (operation.Menus.TouchSensingSettings.TouchSearchOutput == TouchSearchOutput.SearchMacro)
                {
                    //// TOUCH SENSING
                    // Adds one touch search macro event
                    // Example:
                    //   SEARCH_RM(P[3],P[4],10,2) ;
                    searchTargetPoint.Events
                        .CreateSearchMacroEvent()
                        .SetMacroName(operation.Menus.TouchSensingSettings.TouchMacroName)
                        .SetMacroArguments($"{searchTargetPoint.Feedrate:0.0},{this.GetTouchGroupProgramIndex(touchGroup)}")
                        .AddBefore();
                }
            }
        }

        /// <summary>
        ///     Gets the closed touch group to a plasma cutting point based on the selected method.
        /// </summary>
        /// <param name="operation">
        ///    The current plasma operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The closest touch group, its average position, and the distance from the current point.
        /// </returns>
        internal virtual
            IEnumerable<(ITouchGroup TouchGroup, Vector3 AverageTouchGroupPosition, double DistanceToPoint)>
            GetClosestPlasmaTouchGroups(IOperation operation, IPoint point)
        {
            if (point == null)
            {
                return Enumerable.Empty<(ITouchGroup, Vector3, double)>();
            }

            // Returns the closest touch group and the distance to the point
            List<ITouchGroup> touchGroups;
            switch (operation.Menus.PlasmaCuttingSettings.TouchAssociationMethod)
            {
                case TouchAssociationMethod.ByPlasmaModule:
                    {
                        touchGroups = new List<ITouchGroup>();
                        if (point.ApplicationData.PlasmaCutting.TouchGroupPair.Item1 != null)
                        {
                            touchGroups.Add(point.ApplicationData.PlasmaCutting.TouchGroupPair.Item1);
                        }

                        if (point.ApplicationData.PlasmaCutting.TouchGroupPair.Item2 != null)
                        {
                            touchGroups.Add(point.ApplicationData.PlasmaCutting.TouchGroupPair.Item2);
                        }

                        break;
                    }

                case TouchAssociationMethod.ByShortestDistance:
                default:
                    touchGroups = operation.ApplicationData
                        .PlasmaCutting
                        .TouchGroups
                        .ToList();
                    break;
            }

            return touchGroups
                .Select(tg => (TouchGroup: tg, Average: this.AverageTouchGroupPosition(tg)))
                .Select(tg => (tg.TouchGroup, tg.Average,
                    Distance: (point.Position - tg.Average).Length))
                .OrderBy(tuple => tuple.Distance)
                .Take(2);
        }

        /// <summary>
        ///     Calculates the average position of a given touch group.
        /// </summary>
        /// <param name="touchGroup">
        ///     Touch group.
        /// </param>
        /// <returns>
        ///     The average position.
        /// </returns>
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
            // Get the closest touch groups
            (ITouchGroup TouchGroup, Vector3 Average, double Distance)[] closestTouchGroups =
                this.GetClosestPlasmaTouchGroups(operation, point).ToArray();

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
                    this.GetClosestPlasmaTouchGroups(operation, point.PreviousPoint).ToArray();

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
            var closestTouchGroup = this.GetClosestPlasmaTouchGroups(operation, point).FirstOrDefault().TouchGroup;
            if (closestTouchGroup != null)
            {
                // Add point offset event.
                point.Events
                    .CreatePointOffsetEvent()
                    .SetPositionRegisterNumber(this.GetTouchGroupProgramIndex(closestTouchGroup))
                    .AddInline();
            }
        }

        private static double CalculateInterpolationRatio(
            IOperation operation,
            IPoint point,
            IReadOnlyList<(ITouchGroup TouchGroup, Vector3 Average, double Distance)> closestTouchGroups)
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

        /// <summary>
        ///     Detects the closest user frame XYZ signed search direction based on the path direction.
        /// </summary>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     XYZ signed search direction.
        /// </returns>
        private static string SearchDirectionCalculation(IPoint point)
        {
            Vector3 pathDirection = point.NextPoint.Position - point.Position;
            double[] pathDirectionArray =
                {
                    Math.Abs(pathDirection.X), Math.Abs(pathDirection.Y), Math.Abs(pathDirection.Z),
                };
            int index = Array.IndexOf(pathDirectionArray, pathDirectionArray.Max());

            switch (index)
            {
                case 0:
                    return pathDirection.X > 0 ? "X" : "-X";
                case 1:
                    return pathDirection.Y > 0 ? "Y" : "-Y";
                case 2:
                    return pathDirection.Z > 0 ? "Z" : "-Z";
                default:
                    return "-Z";
            }
        }
    }
}
