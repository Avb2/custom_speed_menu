// <copyright file="PostProcessorCustom.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

using Robotmaster.Processor.Core.Common.Enums;
using Robotmaster.Processor.Core.Common.Interfaces.Events;
using Robotmaster.Processor.Fanuc.Generated.Interfaces;
using Robotmaster.Processor.Fanuc.Generated.Artifacts.Events.Interfaces;

namespace Robotmaster.Processor.Fanuc
{
    /// <content>
    ///     Easy-to-customize section of the post processor.
    ///     This customizable section is part of the post processor class therefore
    ///     all the fields, properties, and methods are accessible here.
    /// </content>
    internal partial class PostProcessor
    {
        /// <summary>
        ///     Outputs robot code before the robot program output.
        /// </summary>

       
        // Current speed setting : Should pro0b default to speed defined elsewhere
        double _currentSpeed;


        internal virtual void OutputBeforeRobotProgram()
        {
            // Write the setup and robot program names
            this.MoveSection
                .Write(this.LineNumber.Increment())
                .WriteLine($"  ! {this.Setup.Name} - {this.RobotProgram.Name} ;");

            //// CUSTOMIZATION - Uncomment the example below
            // this.MoveSection
            //   .Write(this.LineNumber.Increment())
            //   .WriteLine($"  ! Write something at the start of a robot program ;");
        }

        /// <summary>
        ///     Outputs robot code before the <paramref name="operation"/> output.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void OutputBeforeOperation(IOperation operation)
        {
            // Write the operation name
            this.MoveSection
                .WriteLine(this.LineNumber.Increment() + $"  ;")
                .WriteLine(this.LineNumber.Increment() + $"  ! --- {operation.Name} --- ;");

            // User frame output
            if (operation.UserFrame.Number != operation.PreviousOperation?.UserFrame.Number)
            {
                this.OutputUserFrame(operation);
            }

            // Tool frame output
            if (operation.TCPFrame.Number != operation.PreviousOperation?.TCPFrame.Number)
            {
                this.OutputToolFrame(operation);
            }

            //// CUSTOMIZATION - Uncomment the example below
            // this.MoveSection
            //   .Write(this.LineNumber.Increment())
            //   .WriteLine($"  ! Write something before every operation ;");
        }

        /// <summary>
        ///     Outputs robot code before the <paramref name="point"/> output.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void OutputBeforePoint(IOperation operation, IPoint point)
        {
            if (point.PreviousPoint?.IsArcMiddlePoint == true)
            {
                // Arc end point cannot have before point events.
                // FallBack: Events were output before the arc middle point;
                return;
            }

            this.OutputBeforePointEvents(operation, point);

            if (point.IsArcMiddlePoint)
            {
                // Arc middle point cannot have after point events.
                // FallBack: Events are output before the point instead;
                this.OutputAfterPointEvents(operation, point);

                // Arc end point cannot have before point events.
                // FallBack: Events are output before the arc middle point;
                this.OutputBeforePointEvents(operation, point.NextPoint);
            }

            //// CUSTOMIZATION - Uncomment the example below
            // this.MoveSection
            //    .Write(this.LineNumber.Increment())
            //    .WriteLine($"  ! Write something before every point ;");
        }

        /// <summary>
        ///     Outputs robot code at the start of a <paramref name="point"/> move line.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void OutputLinePrefix(IOperation operation, IPoint point)
        {
            //// CUSTOMIZATION - Uncomment the example below
            // this.MoveSection
            //    .Write($"Something at the beginning of a line ");
        }

        /// <summary>
        ///     Outputs robot code at the end of a <paramref name="point"/> move line.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void OutputLineSuffix(IOperation operation, IPoint point)
        {
            this.OutputInlineEvents(operation, point);

            //// CUSTOMIZATION - Uncomment the example below
            // this.MoveSection
            //    .Write($" Something at the end of a line");
        }

        /// <summary>
        ///     Outputs robot code after the <paramref name="point"/> output.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void OutputAfterPoint(IOperation operation, IPoint point)
        {
            if (point.IsArcMiddlePoint)
            {
                // Arc middle point cannot have after point events.
                // FallBack: Events were output before the point;
                return;
            }

            this.OutputAfterPointEvents(operation, point);

            //// CUSTOMIZATION - Uncomment the example below
            // this.MoveSection
            //    .Write(this.LineNumber.Increment())
            //    .WriteLine($"  ! Write something after every point ;");
        }

        /// <summary>
        ///     Outputs robot code after the <paramref name="operation"/> output.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void OutputAfterOperation(IOperation operation)
        {
            //// CUSTOMIZATION - Uncomment the example below
            // this.MoveSection
            //    .Write(this.LineNumber.Increment())
            //    .WriteLine($"  ! Write something after every operation ;");
        }

        /// <summary>
        ///     Outputs robot code after the robot program output.
        /// </summary>
        internal virtual void OutputAfterRobotProgram()
        {
            // Scheduler file output is enabled
            if (this.ShouldOutputScheduler && this.RobotProgram.Robot.CustomValues.ContainsKey(CustomValueKeys.RobotNumber))
            {
                // Set the robot's task done flag to true
                // Example: F[1:TASK DONE]=(ON) ;
                this.MoveSection
                    .Write(this.LineNumber.Increment())
                    .WriteLine($"  F[{this.RobotProgram.Robot.CustomValues[CustomValueKeys.RobotNumber]}:TASK DONE]=(ON) ;");
            }

            //// CUSTOMIZATION - Uncomment the example below
            // this.MoveSection
            //    .Write(this.LineNumber.Increment())
            //    .WriteLine($"  ! Write something at the end of a robot program ;");
        }

        /// <summary>
        ///     Outputs the robot code of an event before the <paramref name="point"/> output.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <param name="beforeEvent">
        ///     The event to output.
        /// </param>
        internal virtual void OutputBeforePointEvent(IOperation operation, IPoint point, IEvent beforeEvent)
        {
            //// BASE - Default before point event output
            this.MoveSection
                .Write(this.LineNumber.Increment())
                .WriteLine($"  {beforeEvent.ToCode()};");

            // Check if this is a SpeedChange event and extract the speed value
            if (beforeEvent is ISpeedChange speedChangeEvent && speedChangeEvent.Speed > 0)
            {
                // Set current speed from the SpeedChange event
                _currentSpeed = speedChangeEvent.Speed;
                this.MoveSection
                    .Write(this.LineNumber.Increment())
                    .WriteLine($"! ---- Current Speed: {_currentSpeed}");
            }

        }

        /// <summary>
        ///     Outputs the robot code of an event inline with the <paramref name="point"/> output.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <param name="inlineEvent">
        ///     The event to output.
        /// </param>
      

private string GetCustomSpeed()
{
    // If _currentSpeed is 0 (not set), use the default feedrate from the point
    if (_currentSpeed == 0)
    {
        return $"{this.Feedrate}";
    }
    
    return $" {_currentSpeed}mm/sec";
}

    /// Moved from POSTPROCESSOR.cs
    internal virtual void OutputPoint(IOperation operation, IPoint point)
        {
            if (point.MotionSpace == PointMotionSpace.JointSpace)
            {
                this.OutputJointSpaceMove(operation, point);
                return;
            }

            switch (point.MotionType)
            {
                case PointMotionType.Joint:
                    this.OutputJointMove(operation, point);
                    break;
                case PointMotionType.Linear:
                    this.OutputLinearMove(operation, point);
                    break;
                case PointMotionType.Circular when point.IsArcMiddlePoint:
                    // wait for endpoint
                    break;
                case PointMotionType.Circular when !point.IsArcMiddlePoint:
                    this.OutputCircularMove(operation, point.PreviousPoint, point);
                    break;
                default:
                    throw new System.NotImplementedException();
            }
        }




        internal virtual void OutputLinearMove(IOperation operation, IPoint point)
        {
            /*
             * Move output
             * Example:
             *  25: L P[29] 50 mm/sec CNT85 ACC65 ;
             */

            this.MoveSection
                .Write(this.LineNumber.Increment());
            this.OutputLinePrefix(operation, point);
            this.MoveSection
                .Write(this.FormatMotionType(operation, point))
                .Write(this.PointNumber.Increment())
                .Write(GetCustomSpeed())
                .Write(this.FormatPositioningPath(operation, point))
                .Write(this.FormatAcceleration(operation, point))
                .Write(this.FormatAdditionalMotionInstructions(operation, point));
            this.OutputLineSuffix(operation, point);
            this.MoveSection
                .WriteLine(" ;");

            /*
             * Position output
             * Example:
             *     P[2]{
             *        GP1:
             *        UF : 1, UT : 1, CONFIG : 'N U T, 0, 0, 0',
             *        X =    -5.00  mm,  Y =     -15.00  mm,  Z =    100.00  mm,
             *        W =    180.00 deg,  P =     0.00 deg,  R =    0.00 deg
             *     };
             */

            this.PositionSection
                .Write($"{this.PointNumber}{{")
                .Write(this.FormatRobotGroups(operation, point))
                .Write(this.FormatPositionUfAndUt(operation, point))
                .Write(this.FormatConfig(operation, point))
                .Write(this.FormatPosition(operation, point))
                .Write(this.FormatOrientation(operation, point))
                .Write(this.FormatExternalAxisValues(operation, point))
                .WriteLine()
                .WriteLine("};");
        }




          internal virtual void OutputCircularMove(IOperation operation, IPoint midPoint, IPoint endPoint)
        {
            /*
             * Circular move(s)
             * Example:
             *    25:C P[9]
             *         P[10] 20 mm/sec CNT85 ACC65 ;
             */

            /*
             * Circular position
             * Example:
             *     P[9]{
             *        GP1:
             *         UF : 1, UT : 1, CONFIG: 'N U T, 0, 0, 0',
             *         X =   -5.00  mm,  Y =  -15.00  mm,  Z =  100.00 mm,
             *         W =  180.00 deg,  P =    0.00 deg,  R =  0.00 deg
             *     };
             */

            // Midpoint move
            // Example: C P[9]
            this.MoveSection
                .Write(this.LineNumber.Increment());
            this.OutputLinePrefix(operation, midPoint);
            this.MoveSection
                .Write(this.FormatMotionType(operation, midPoint))
                .Write(this.PointNumber.Increment());
            this.OutputLineSuffix(operation, midPoint);
            this.MoveSection
                .WriteLine();

            // Midpoint position
            this.PositionSection
                .Write($"{this.PointNumber}{{")
                .Write(this.FormatRobotGroups(operation, midPoint))
                .Write(this.FormatPositionUfAndUt(operation, midPoint))
                .Write(this.FormatConfig(operation, midPoint))
                .Write(this.FormatPosition(operation, midPoint))
                .Write(this.FormatOrientation(operation, midPoint))
                .Write(this.FormatExternalAxisValues(operation, midPoint))
                .WriteLine()
                .WriteLine("};");

            // Endpoint move
            this.MoveSection
                .Indent(7)
                .Write(this.PointNumber.Increment())
                .Write(GetCustomSpeed())
                .Write(this.FormatPositioningPath(operation, endPoint))
                .Write(this.FormatAcceleration(operation, endPoint))
                .Write(this.FormatAdditionalMotionInstructions(operation, endPoint));
            this.OutputLineSuffix(operation, endPoint);
            this.MoveSection
                .WriteLine(" ;");

            // Endpoint position
            this.PositionSection
                .Write($"{this.PointNumber}{{")
                .Write(this.FormatRobotGroups(operation, endPoint))
                .Write(this.FormatPositionUfAndUt(operation, endPoint))
                .Write(this.FormatConfig(operation, endPoint))
                .Write(this.FormatPosition(operation, endPoint))
                .Write(this.FormatOrientation(operation, endPoint))
                .Write(this.FormatExternalAxisValues(operation, endPoint))
                .WriteLine()
                .WriteLine("};");
        }














        internal virtual void OutputInlineEvent(IOperation operation, IPoint point, IEvent inlineEvent)
        {
            //// BASE - Default inline event output
            this.MoveSection
                .Write(" " + inlineEvent.ToCode());

            //// CUSTOMIZATION - Uncomment the example below
            ////if (inlineEvent is Command)
            ////{
            ////    this.MoveSection
            ////        .Write($" Write something else");
            ////}
        }

        /// <summary>
        ///     Outputs the robot code of an event after the <paramref name="point"/> output.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <param name="afterEvent">
        ///     The event to output.
        /// </param>
        internal virtual void OutputAfterPointEvent(IOperation operation, IPoint point, IEvent afterEvent)
        {
            //// BASE - Default after point event output
            this.MoveSection
                .Write(this.LineNumber.Increment())
                .WriteLine($"  {afterEvent.ToCode()};");

            //// CUSTOMIZATION - Uncomment the example below
            ////if (afterEvent is Command)
            ////{
            ////    this.MoveSection
            ////        .Write(this.LineNumber.Increment())
            ////        .WriteLine($"  ! Write something after a command event");
            ////}
        }
    }
}