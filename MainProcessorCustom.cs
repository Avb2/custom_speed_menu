// <copyright file="MainProcessorCustom.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc
{
    using System.Linq;
    using Robotmaster.Processor.Core.Common.Interfaces.Scheduler;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <content>
    ///     Easy-to-customize section of the main processor.
    ///     This customizable section is part of the main processor class therefore
    ///     all the fields, properties, and methods are accessible here.
    /// </content>
    internal partial class MainProcessor
    {
        /// <summary>
        ///     Edits an operation.
        ///     Called before editing all it's points with <seealso cref="EditPoint"/>.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void EditOperation(IOperation operation)
        {
            // If operation contains pick place data and pick place output is enabled
            if (operation.ApplicationData.PickPlace != null && operation.Menus.PickPlaceSettings.IsPickPlaceOutputEnabled)
            {
                this.AddPickPlaceEvents(operation);
            }

            // If operation is part of a multi-robot setup
            this.AddSchedulerDataEvents(operation);

            //// CUSTOMIZATION - Uncomment the example below
            //// Example: Add events to the first point
            // operation.FirstPoint.Events.CreateCommandEvent().SetCommandText("Some command").AddBefore();
            // operation.FirstPoint.Events.CreateCommentEvent().SetCommentText("Some comment").AddBefore();
        }

        /// <summary>
        ///     Edits a point.
        ///     This method is called at every point of <paramref name="operation"/>.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void EditPoint(IOperation operation, IPoint point)
        {
            //// CUSTOMIZATION - Uncomment the example below
            //// Example: Add event to sharp corner
            // if (point.Flags.IsInProcess &&
            //     point != operation.LastPoint &&
            //     point.PathDirection.AngleBetween(point.NextPoint.PathDirection) > 45)
            // {
            //     point.Events.CreateCommentEvent().SetCommentText("Sharp corner detect").AddBefore();
            // }
        }

        /// <summary>
        ///     Adds the scheduler data events.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void AddSchedulerDataEvents(IOperation operation)
        {
            this.AddWaitTimeEvent(operation);

            if (this.Setup.Configuration.IsMultiRobot)
            {
                this.AddHandshakeEvent(operation);
            }
        }

        /// <summary>
        ///     Adds the wait time event attached to this operation.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void AddWaitTimeEvent(IOperation operation)
        {
            var waitTime = operation.SchedulerData.WaitTime;

            if (waitTime == null)
            {
                return;
            }

            // Example: WAIT   10.00 (sec);
            operation.FirstPoint.Events.CreateDelayEvent()
                .SetDelayValue(waitTime.Time)
                .AddBefore();
        }

        /// <summary>
        ///     Adds the handshake event attached to this operation.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void AddHandshakeEvent(IOperation operation)
        {
            var handshake = operation.SchedulerData.Handshake;

            if (handshake == null ||
                operation.PreviousOperation == null)
            {
                return;
            }

            string command = this.RobotProgram.Robot.Equals(handshake.DependentRobots[0]) ? "INPOS" : "PR_SYNC";

            // Handshake events trigger at the end of a move.
            // To sync the start of an operation, the handshake must be at the very end of the previous operation.
            // Example: SYNC_SCHED[3] ;
            operation.PreviousOperation.LastPoint.Events
                .CreateSyncScheduleEvent()
                .SetSyncScheduleNumber(GetSchedulerNumber(handshake))
                .AddBefore();

            // Example: PR_SYNC[1] ;
            operation.PreviousOperation.LastPoint.Events.CreateHandshakeEvent()
                .SetCommand(command)
                .SetHandshakeId(handshake.Id)
                .AddInline();
        }

        /// <summary>
        ///     Calculates the appropriate scheduler number based on the robots dependent on the handshake.
        /// </summary>
        /// <remarks>
        ///     Each robot's motion group number represents a bit. The scheduler number is calculated by summing the bits of the dependent robots.
        /// </remarks>
        /// <param name="handshake">
        ///     The handshake to calculate the scheduler number for.
        /// </param>
        /// <returns>
        ///     An integer representing the scheduler number.
        /// </returns>
        private static int GetSchedulerNumber(IHandshake handshake)
        {
            int schedulerNumber = 0;
            foreach (var robot in handshake.DependentRobots)
            {
                string motionGroup = robot.CustomValues[CustomValueKeys.MashGrpNumber];
                if (motionGroup.Equals("1", System.StringComparison.Ordinal))
                {
                    schedulerNumber += 1;
                }
                else if (motionGroup.Equals("2", System.StringComparison.Ordinal))
                {
                    schedulerNumber += 2;
                }
                else if (motionGroup.Equals("3", System.StringComparison.Ordinal))
                {
                    schedulerNumber += 4;
                }
                else if (motionGroup.Equals("4", System.StringComparison.Ordinal))
                {
                    schedulerNumber += 8;
                }
                else if (motionGroup.Equals("5", System.StringComparison.Ordinal))
                {
                    schedulerNumber += 16;
                }
            }

            return schedulerNumber;
        }
    }
}
