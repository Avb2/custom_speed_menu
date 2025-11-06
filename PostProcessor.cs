// <copyright file="PostProcessor.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>



namespace Robotmaster.Processor.Fanuc
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Robotmaster.Processor.Core.Common.Enums;
    using Robotmaster.Processor.Core.Common.Interfaces.FileFramework;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.ControllerSettings;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.MotionSettings;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <summary>
    ///     The top-level post processor.
    /// </summary>
    internal partial class PostProcessor
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PostProcessor"/> class.
        /// </summary>
        /// <param name="program">
        ///     The current program.
        /// </param>
        /// <param name="context">
        ///     The current context.
        /// </param>
        internal PostProcessor(IProgram program, IPostProcessorContext context)
        {
            this.Context = context;
            this.Scheduler = context.Scheduler;
            this.Cell = context.Cell;
            this.Setup = context.Setup;
            this.Program = program;
            this.RootDirectory = context.RootPostDirectory;
        }

        /// <summary>
        ///     Gets the current context.
        /// </summary>
        internal IPostProcessorContext Context { get; }

        /// <summary>
        ///     Gets the current scheduler.
        /// </summary>
        internal IScheduler Scheduler { get; }

        /// <summary>
        ///     Gets the current cell.
        /// </summary>
        internal ICell Cell { get; }

        /// <summary>
        ///     Gets the current setup.
        /// </summary>
        internal ISetup Setup { get; }

        /// <summary>
        ///     Gets the current program.
        /// </summary>
        internal IProgram Program { get; }

        /// <summary>
        ///     Gets or sets the current robot program.
        /// </summary>
        internal IRobotProgram RobotProgram { get; set; }

        /// <summary>
        ///     Gets the root directory.
        /// </summary>
        internal IPostDirectory RootDirectory { get; }

        /// <summary>
        ///     Gets or sets the current file.
        /// </summary>
        internal ITextFile CurrentFile { get; set; }

        /// <summary>
        ///     Gets the current <see cref="ITextSection"/> for moves.
        /// </summary>
        internal ITextSection MoveSection => this.CurrentFile.RootSection.ChildSections["MOVES"];

        /// <summary>
        ///     Gets the current <see cref="ITextSection"/> for positions.
        /// </summary>
        internal ITextSection PositionSection => this.CurrentFile.RootSection.ChildSections["POSITIONS"];

        /// <summary>
        ///     Gets or sets the current joint speed.
        /// </summary>
        internal IDoublePostVariable JointSpeed { get; set; }

        /// <summary>
        ///     Gets or sets the current feedrate.
        /// </summary>
        internal IDoublePostVariable Feedrate { get; set; }

        /// <summary>
        ///     Gets or sets a generic numerical values.
        /// </summary>
        internal IDoublePostVariable GenericVariable { get; set; }

        /// <summary>
        ///     Gets or sets a generic length values.
        /// </summary>
        internal IDoublePostVariable GenericLengthVariable { get; set; }

        /// <summary>
        ///     Gets or sets a generic angle values.
        /// </summary>
        internal IDoublePostVariable GenericAngleVariable { get; set; }

        /// <summary>
        ///     Gets or sets the current point number.
        /// </summary>
        internal IIntegerPostVariable PointNumber { get; set; }

        /// <summary>
        ///     Gets or sets the current line number.
        /// </summary>
        internal IIntegerPostVariable LineNumber { get; set; }

        /// <summary>
        ///     Gets or sets the current line number in the main file.
        /// </summary>
        internal IIntegerPostVariable MainFileLineNumber { get; set; }

        /// <summary>
        ///     Gets or sets the robot Fanuc group.
        /// </summary>
        internal FanucGroup RobotGroup { get; set; }

        /// <summary>
        ///      Gets or sets the list of other Fanuc group .
        /// </summary>
        internal List<FanucGroup> OtherGroups { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the current posted program is part of a multi-robot cell.
        /// </summary>
        internal bool ShouldOutputScheduler { get; set; }

        /// <summary>
        ///     The post processor entry point.
        /// </summary>
        public void Run()
        {
            this.ProgramRun();
            foreach (var robotProgram in this.Program.RobotPrograms)
            {
                this.RobotProgramRun(robotProgram);
            }
        }

        /// <summary>
        ///     Auto formats the file name to be compatible with the chosen robot controller.
        ///     Formatting follows the controller's file naming convention.
        /// </summary>
        /// <param name="fileName">
        ///     The file name to modify.
        /// </param>
        /// <returns>
        ///     A formatted string that complys with the file name formatting requirements of the robot controller.
        /// </returns>
        internal static string FormatFileName(string fileName)
        {
            int maxProgrameNameLength = 36;

            return fileName.Replace(' ', '_').Replace('-', '_').ToUpper(CultureInfo.InvariantCulture).CapLength(maxProgrameNameLength);
        }

        /// <summary>
        ///     The program-level entry point.
        /// </summary>
        internal void ProgramRun()
        {
            if (this.Program.RobotPrograms.All(rp => rp.Operations.Count is 0))
            {
                return;
            }

            // MULTI-ROBOT: Generate code specific to multi-robot simultaneous applications.
            this.ActiveRobots = this.GetActiveRobotsInCell();
            var firstOperation = this.Program.RobotPrograms.SelectMany(rp => rp.Operations).First();

            // Enable scheduler output if it is set to "Always" output, or it is set to "When Needed" and there are multiple active robots
            this.ShouldOutputScheduler =
                firstOperation.Menus.ControllerSettings.SchedulerProgramOutputType == SchedulerProgramOutputType.Always ||
                    (this.ActiveRobots.Count > 1 &&
                        firstOperation.Menus.ControllerSettings.SchedulerProgramOutputType == SchedulerProgramOutputType.WhenNeeded);
            if (this.ShouldOutputScheduler)
            {
                this.OutputMultiRobotSchedulerProgram();
            }
        }

        /// <summary>
        ///     The robot-program-level entry point.
        /// </summary>
        /// <param name="robotProgram">
        ///     The robot program to generate robot code for.
        /// </param>
        internal void RobotProgramRun(IRobotProgram robotProgram)
        {
            this.RobotProgram = robotProgram;

            if (this.RobotProgram.Operations.Count is 0 || !this.IsRobotProgramInputValid())
            {
                return;
            }

            this.InitializeDataBeforeRobotProgram();
            this.OutputBeforeRobotProgram();

            foreach (var operation in this.RobotProgram.Operations)
            {
                this.InitializeDataBeforeOperation(operation);
                this.OutputBeforeOperation(operation);

                for (var point = operation.FirstPoint; point != null; point = point.NextPoint)
                {
                    this.InitializeDataBeforePoint(operation, point);
                    this.OutputBeforePoint(operation, point);
                    this.OutputPoint(operation, point);
                    this.OutputAfterPoint(operation, point);
                }

                this.OutputAfterOperation(operation);
            }

            this.OutputAfterRobotProgram();
            this.FinalizeDataAfterRobotProgram();
            this.GenerateFiles();
        }

        /// <summary>
        ///     Gets a value indicating whether the processor inputs are valid.
        ///     If <c>false</c> post processing and file generation will be canceled.
        /// </summary>
        /// <returns>
        ///     <c>true</c> if the inputs are valid; otherwise, <c>false</c>.
        /// </returns>
        internal virtual bool IsRobotProgramInputValid()
        {
            bool isProgramInputValid = true;

            // Checks if the program contains events to be output mid-arc move.
            if (this.RobotProgram.Operations.Any(op => op.Points.Any(pt =>
                    pt.IsArcMiddlePoint && (pt.Events.AfterEvents.Any() || pt.NextPoint.Events.BeforeEvents.Any()))))
            {
                this.Context.NotifyUser("Processor Warning: Fanuc does not support event output mid-arc move. Events between the mid and end point of an arc move will be output BEFORE the mid point.");
            }

            // Checks if home operation output is disabled and an operation immediately following home has a handshake.
            if (this.RobotProgram.Operations.Any(op => (op is IHomeOperation homeOperation) && !homeOperation.ShouldOutput && op.NextOperation.SchedulerData.Handshake != null))
            {
                this.Context.NotifyUser(
                    "Processor Warning: Approaching home operation output cannot be disabled when there is a handshake on the next operation." + Environment.NewLine +
                    "Please re-enable approaching home position output for robot program: " + this.RobotProgram.Name + ".",
                    false);
                isProgramInputValid = false;
            }

            if (!isProgramInputValid)
            {
                this.Context.NotifyUser("Processor Warning: Program's input invalid. Code generation has been canceled.");
            }

            return isProgramInputValid;
        }

        /// <summary>
        ///     Initializes data before the robot program output.
        /// </summary>
        internal virtual void InitializeDataBeforeRobotProgram()
        {
            this.InitializeVariables();
            this.InitializeFanucGroups();
            this.InitializeFileStructure();
        }

        /// <summary>
        ///     Initializes the variables before the robot program output.
        /// </summary>
        internal virtual void InitializeVariables()
        {
            this.JointSpeed = this.Context.CreateDoublePostVariable(0, " {0:0}%");
            this.Feedrate = this.Context.CreateDoublePostVariable(0, " {0:0}mm/sec");
            this.PointNumber = this.Context.CreateIntegerPostVariable(0, "P[{0}]");

            // Check if line number output is enabled
            if (this.RobotProgram.FirstOperation.Menus.ControllerSettings.IsLineNumberEnabled)
            {
                this.LineNumber = this.Context.CreateIntegerPostVariable(0, "{0,4:0}:");
                this.MainFileLineNumber = this.Context.CreateIntegerPostVariable(0, "{0,4:0}: ");
            }
            else
            {
                this.LineNumber = this.Context.CreateIntegerPostVariable(0, "    :");
                this.MainFileLineNumber = this.Context.CreateIntegerPostVariable(0, "    :");
            }

            this.GenericVariable = this.Context.CreateDoublePostVariable(0, "{0:0.###}", false);
            this.GenericLengthVariable = this.Context.CreateDoublePostVariable(0, "{0,8:0.000}  mm", false);
            this.GenericAngleVariable = this.Context.CreateDoublePostVariable(0, "{0,8:0.000} deg", false);
        }

        /// <summary>
        ///     Initializes the file structure before the robot program output.
        /// </summary>
        internal virtual void InitializeFileStructure()
        {
            // If program is batched, create parent sub directory for program output. Else, use target directory.
            var rootDirectory = this.GetTargetRootDirectory();

            if (this.RobotProgram.FirstOperation.Menus.ControllerSettings.MaximumLine <= 0)
            {
                // Single file output
                // Create and format a program
                this.CurrentFile = rootDirectory.CreateChildTextFile(FormatFileName(this.RobotProgram.Name), ".LS");
                this.StartFile(this.CurrentFile, this.RobotProgram.FirstOperation, this.RobotProgram.FirstOperation.FirstPoint);
            }
            else
            {
                // Multi-file output
                // Create and format a main program
                var mainFile = rootDirectory.CreateChildTextFile(FormatFileName(this.RobotProgram.Name), ".LS");
                this.StartMainFile(mainFile);

                // Create and format the first sub program (child of the main program)
                var firstPostFileName = this.FindNextAvailableChildName(mainFile, this.RobotProgram.FirstOperation, this.RobotProgram.FirstOperation.FirstPoint);
                this.CurrentFile = rootDirectory.CreateChildTextFile(FormatFileName(firstPostFileName), ".LS", mainFile);
                this.CallFileIntoParent(this.CurrentFile, this.RobotProgram.FirstOperation, this.RobotProgram.FirstOperation.FirstPoint);
                this.StartFile(this.CurrentFile, this.RobotProgram.FirstOperation, this.RobotProgram.FirstOperation.FirstPoint);
            }
        }

        /// <summary>
        ///     Gets the target post directory of the robot program.
        /// </summary>
        /// <returns>
        ///     The target post directory.
        /// </returns>
        internal virtual IPostDirectory GetTargetRootDirectory()
        {
            return this.RobotProgram.FirstOperation.Menus.ControllerSettings.IsProgramBatched
                ? this.RootDirectory.CreateChildDirectory(FormatFileName(this.RobotProgram.Name))
                : this.RootDirectory;
        }

        /// <summary>
        ///     Updates the multi-file output structure by creating a new program file when the max number of lines is reached.
        /// </summary>
        /// <remarks>
        ///     Updates before the point output.
        /// </remarks>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void UpdateFileStructure(IOperation operation, IPoint point)
        {
            var rootDirectory = this.GetTargetRootDirectory();

            // Multi-file output
            // Split into a new file if the maximum line number for the current file is reached
            if (operation.Menus.ControllerSettings.MaximumLine > 0
                && this.LineNumber.Value >= operation.Menus.ControllerSettings.MaximumLine
                && (point.MotionType != PointMotionType.Circular || point.IsArcMiddlePoint))
            {
                // Finalize the format of the current sub-program file
                this.EndFile(this.CurrentFile, operation, point);

                // Create and format the next sub-program (child of the main program)
                var firstPostFileName = FormatFileName(this.FindNextAvailableChildName(this.CurrentFile.Parent, operation, point));
                this.CurrentFile = rootDirectory.CreateChildTextFile(firstPostFileName, ".LS", this.CurrentFile.Parent);

                this.StartFile(this.CurrentFile, operation, point);

                // Reference this new sub-program in the main program
                this.CallFileIntoParent(this.CurrentFile, operation, point);
            }
        }

        /// <summary>
        ///     Finalizes the file structure after the robot program output.
        /// </summary>
        internal virtual void FinalizeFileStructure()
        {
            if (this.RobotProgram.LastOperation.Menus.ControllerSettings.MaximumLine <= 0)
            {
                // Single file output
                // Finalize the format of the current program file
                this.EndFile(this.CurrentFile, this.RobotProgram.LastOperation, this.RobotProgram.LastOperation.LastPoint);
            }
            else
            {
                // Multi-file output
                // Finalize the format of the current, and last, sub-program file
                this.EndFile(this.CurrentFile, this.RobotProgram.LastOperation, this.RobotProgram.LastOperation.LastPoint);

                // Finalize the format of the main program file
                this.EndMainFile(this.CurrentFile.Parent);
            }
        }

        /// <summary>
        ///     Starts the file formatting following the brand convention.
        /// </summary>
        /// <param name="file">
        ///     The current file.
        /// </param>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void StartFile(ITextFile file, IOperation operation, IPoint point)
        {
            var movesSection = file.RootSection.CreateChildSection("MOVES"); // Section for moves
            var positionsSection = file.RootSection.CreateChildSection("POSITIONS"); // Section for position

            this.OutputFileHeader(file, operation, point);

            movesSection.Header.WriteLine("/MN");

            positionsSection.Header.WriteLine("/POS");
            positionsSection.Footer.WriteLine("/END");
        }

        /// <summary>
        ///     Starts the main file formatting following the brand convention.
        /// </summary>
        /// <param name="mainFile">
        ///     The main file.
        /// </param>
        internal virtual void StartMainFile(ITextFile mainFile)
        {
            var movesSection = mainFile.RootSection.CreateChildSection("MOVES"); // Section for moves
            var positionsSection = mainFile.RootSection.CreateChildSection("POSITIONS"); // Section for position

            this.WriteMainFileHeader(mainFile);

            movesSection.Header.WriteLine("/MN");

            positionsSection.Header.WriteLine("/POS");
            positionsSection.Footer.WriteLine("/END");
        }

        /// <summary>
        ///     Ends the file formatting following the brand convention.
        /// </summary>
        /// <param name="file">
        ///     The current file.
        /// </param>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void EndFile(ITextFile file, IOperation operation, IPoint point)
        {
            file.RootSection.GetTextPlaceholder("LINE_COUNT").ReplacementValue = $"{this.LineNumber.Value}";
            this.LineNumber.Value = 0;
        }

        /// <summary>
        ///     Ends the main file formatting following the brand convention.
        /// </summary>
        /// <param name="mainFile">
        ///     The main file.
        /// </param>
        internal virtual void EndMainFile(ITextFile mainFile)
        {
            mainFile.RootSection.GetTextPlaceholder("LINE_COUNT").ReplacementValue = $"{this.MainFileLineNumber.Value}";
            this.MainFileLineNumber.Value = 0;
        }

        /// <summary>
        ///      Adds a call for a sub-file in the main/parent file following the brand convention.
        /// </summary>
        /// <param name="file">
        ///     The current file.
        /// </param>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void CallFileIntoParent(ITextFile file, IOperation operation, IPoint point)
        {
            file.Parent.RootSection.ChildSections["MOVES"]
                .Write(this.MainFileLineNumber.Increment())
                .WriteLine("CALL " + file.FileNameWithoutExtension + " ;");
        }

        /// <summary>
        ///     Finds the next available file name among the siblings files following the brand convention.
        /// </summary>
        /// <param name="file">
        ///     The current file.
        /// </param>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The next available file name.
        /// </returns>
        internal virtual string FindNextAvailableChildName(ITextFile file, IOperation operation, IPoint point)
        {
            string subPostFileNamePattern = file.FileNameWithoutExtension + "_";
            var maxIndex = 1;

            while (file.ChildFiles.Any(pf => pf.FileNameWithoutExtension == subPostFileNamePattern + maxIndex))
            {
                maxIndex++;
            }

            return subPostFileNamePattern + maxIndex;
        }

        /// <summary>
        ///     Initializes the Fanuc groups structure from the current robot program.
        /// </summary>
        internal virtual void InitializeFanucGroups()
        {
            var robotJoints = this.RobotProgram.RobotJoints;
            var robotGroupName = robotJoints[0].Group;
            var externalAxisJoints = this.Setup.Configuration.ExternalJoints;
            var jointGroup = new List<FanucGroup>();

            foreach (var groupNumber in robotJoints.Union(externalAxisJoints).Select(ax => ax.Group).Distinct())
            {
                jointGroup.Add(new FanucGroup()
                {
                    Number = int.Parse(groupNumber, CultureInfo.InvariantCulture),
                });
            }

            foreach (var robotJoint in robotJoints)
            {
                var fanucId = Regex.Match(robotJoint.Id, "(?>GP[0-9]*_)?(J[0-9]*)_?").Groups[1].Value;
                jointGroup.Single(gp => gp.Number.ToString(CultureInfo.InvariantCulture) == robotGroupName)
                    .RobotJoints
                    .Add(new FanucJoint()
                    {
                        RobotmasterId = robotJoint.Id,
                        FanucId = fanucId,
                        IsRevolute = robotJoint.IsRevolute,
                    });
            }

            foreach (var externalAxisJoint in externalAxisJoints)
            {
                var fanucId = Regex.Match(externalAxisJoint.Id, "(?>GP[0-9]*_)?(.*)").Groups[1].Value;
                jointGroup.Single(gp => gp.Number.ToString(CultureInfo.InvariantCulture) == externalAxisJoint.Group)
                    .ExternalJoints
                    .Add(new FanucJoint()
                    {
                        RobotmasterId = externalAxisJoint.Id,
                        FanucId = fanucId,
                        IsRevolute = externalAxisJoint.IsRevolute,
                    });
            }

            // Assumes there is only one robot per configuration
            this.RobotGroup =
                jointGroup.Single(gp =>
                    gp.Number.ToString(CultureInfo.InvariantCulture) == robotGroupName);
            this.OtherGroups = jointGroup.Where(gp => !gp.RobotJoints.Any()).ToList();
        }

        /// <summary>
        ///     Initializes data before the operation output.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void InitializeDataBeforeOperation(IOperation operation)
        {
            this.JointSpeed.Value = operation.Menus.MotionSettings.JointSpeed;
        }

        /// <summary>
        ///     Initializes data before the point output.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void InitializeDataBeforePoint(IOperation operation, IPoint point)
        {
            this.Feedrate.Value = point.Feedrate;

            this.UpdateFileStructure(operation, point);
        }

        /// <summary>
        ///     Outputs the <paramref name="point"/> robot code.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// 
        /// 
        /// 
        /// 
        /// 
        // internal virtual void OutputPoint(IOperation operation, IPoint point)
        // {
        //     if (point.MotionSpace == PointMotionSpace.JointSpace)
        //     {
        //         this.OutputJointSpaceMove(operation, point);
        //         return;
        //     }

        //     switch (point.MotionType)
        //     {
        //         case PointMotionType.Joint:
        //             this.OutputJointMove(operation, point);
        //             break;
        //         case PointMotionType.Linear:
        //             this.OutputLinearMove(operation, point);
        //             break;
        //         case PointMotionType.Circular when point.IsArcMiddlePoint:
        //             // wait for endpoint
        //             break;
        //         case PointMotionType.Circular when !point.IsArcMiddlePoint:
        //             this.OutputCircularMove(operation, point.PreviousPoint, point);
        //             break;
        //         default:
        //             throw new NotImplementedException();
        //     }
        // }

        /// <summary>
        ///     Outputs a joint move defined in joint space (JJ).
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void OutputJointSpaceMove(IOperation operation, IPoint point)
        {
            if (!(operation is IHomeOperation homeOperation) || homeOperation.ShouldOutput)
            {
                /*
                 * Move output
                 * Example:
                 *  24: J P[1] 100% FINE ;
                 */

                this.MoveSection
                    .Write(this.LineNumber.Increment());
                this.OutputLinePrefix(operation, point);
                this.MoveSection
                    .Write(this.FormatMotionType(operation, point))
                    .Write(this.PointNumber.Increment())
                    .Write(this.FormatSpeed(operation, point))
                    .Write(this.FormatPositioningPath(operation, point))
                    .Write(this.FormatAcceleration(operation, point))
                    .Write(this.FormatAdditionalMotionInstructions(operation, point));
                this.OutputLineSuffix(operation, point);
                this.MoveSection
                    .WriteLine(" ;");

                /*
                 * Position output
                 * Example:
                 *     P[1]{
                 *        GP1:
                 *        UF : 1, UT : 1,
                 *        J1 = 0.00 deg,    J2 = 0.00 deg,  J3 = 0.00 deg,
                 *        J4 = 0.00 deg,    J5 = 0.00 deg,  J6 = 0.00 deg
                 *     };
                 */

                this.PositionSection
                    .Write($"{this.PointNumber}{{")
                    .Write(this.FormatRobotGroups(operation, point))
                    .Write(this.FormatPositionUfAndUt(operation, point))
                    .Write(this.FormatRobotJointValues(operation, point))
                    .Write(this.FormatExternalAxisValues(operation, point))
                    .WriteLine()
                    .WriteLine("};");
            }
        }

        /// <summary>
        ///     Outputs a joint move defined in Cartesian space (JC).
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void OutputJointMove(IOperation operation, IPoint point)
        {
            /*
             * Move output
             * Example:
             *  25: J P[2] 100% FINE ;
             */

            this.MoveSection
                .Write(this.LineNumber.Increment());
            this.OutputLinePrefix(operation, point);
            this.MoveSection
                .Write(this.FormatMotionType(operation, point))
                .Write(this.PointNumber.Increment())
                .Write(this.FormatSpeed(operation, point))
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
             *        X =   -5.00  mm,  Y =  -15.00  mm,  Z =  100.00 mm,
             *        W =  180.00 deg,  P =    0.00 deg,  R =  0.00 deg
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

        /// <summary>
        ///     Outputs a linear move defined in Cartesian space (LC).
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        // internal virtual void OutputLinearMove(IOperation operation, IPoint point)
        // {
        //     /*
        //      * Move output
        //      * Example:
        //      *  25: L P[29] 50 mm/sec CNT85 ACC65 ;
        //      */

        //     this.MoveSection
        //         .Write(this.LineNumber.Increment());
        //     this.OutputLinePrefix(operation, point);
        //     this.MoveSection
        //         .Write(this.FormatMotionType(operation, point))
        //         .Write(this.PointNumber.Increment())
        //         .Write(this.FormatSpeed(operation, point))
        //         .Write(this.FormatPositioningPath(operation, point))
        //         .Write(this.FormatAcceleration(operation, point))
        //         .Write(this.FormatAdditionalMotionInstructions(operation, point));
        //     this.OutputLineSuffix(operation, point);
        //     this.MoveSection
        //         .WriteLine(" ;");

        //     /*
        //      * Position output
        //      * Example:
        //      *     P[2]{
        //      *        GP1:
        //      *        UF : 1, UT : 1, CONFIG : 'N U T, 0, 0, 0',
        //      *        X =    -5.00  mm,  Y =     -15.00  mm,  Z =    100.00  mm,
        //      *        W =    180.00 deg,  P =     0.00 deg,  R =    0.00 deg
        //      *     };
        //      */

        //     this.PositionSection
        //         .Write($"{this.PointNumber}{{")
        //         .Write(this.FormatRobotGroups(operation, point))
        //         .Write(this.FormatPositionUfAndUt(operation, point))
        //         .Write(this.FormatConfig(operation, point))
        //         .Write(this.FormatPosition(operation, point))
        //         .Write(this.FormatOrientation(operation, point))
        //         .Write(this.FormatExternalAxisValues(operation, point))
        //         .WriteLine()
        //         .WriteLine("};");
        // }

        /// <summary>
        ///     Outputs an arc move defined in Cartesian space (CC).
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="midPoint">
        ///     The arc mid point.
        /// </param>
        /// <param name="endPoint">
        ///     The arc end point.
        /// </param>
        // internal virtual void OutputCircularMove(IOperation operation, IPoint midPoint, IPoint endPoint)
        // {
        //     /*
        //      * Circular move(s)
        //      * Example:
        //      *    25:C P[9]
        //      *         P[10] 20 mm/sec CNT85 ACC65 ;
        //      */

        //     /*
        //      * Circular position
        //      * Example:
        //      *     P[9]{
        //      *        GP1:
        //      *         UF : 1, UT : 1, CONFIG: 'N U T, 0, 0, 0',
        //      *         X =   -5.00  mm,  Y =  -15.00  mm,  Z =  100.00 mm,
        //      *         W =  180.00 deg,  P =    0.00 deg,  R =  0.00 deg
        //      *     };
        //      */

        //     // Midpoint move
        //     // Example: C P[9]
        //     this.MoveSection
        //         .Write(this.LineNumber.Increment());
        //     this.OutputLinePrefix(operation, midPoint);
        //     this.MoveSection
        //         .Write(this.FormatMotionType(operation, midPoint))
        //         .Write(this.PointNumber.Increment());
        //     this.OutputLineSuffix(operation, midPoint);
        //     this.MoveSection
        //         .WriteLine();

        //     // Midpoint position
        //     this.PositionSection
        //         .Write($"{this.PointNumber}{{")
        //         .Write(this.FormatRobotGroups(operation, midPoint))
        //         .Write(this.FormatPositionUfAndUt(operation, midPoint))
        //         .Write(this.FormatConfig(operation, midPoint))
        //         .Write(this.FormatPosition(operation, midPoint))
        //         .Write(this.FormatOrientation(operation, midPoint))
        //         .Write(this.FormatExternalAxisValues(operation, midPoint))
        //         .WriteLine()
        //         .WriteLine("};");

        //     // Endpoint move
        //     this.MoveSection
        //         .Indent(7)
        //         .Write(this.PointNumber.Increment())
        //         .Write(this.FormatSpeed(operation, endPoint))
        //         .Write(this.FormatPositioningPath(operation, endPoint))
        //         .Write(this.FormatAcceleration(operation, endPoint))
        //         .Write(this.FormatAdditionalMotionInstructions(operation, endPoint));
        //     this.OutputLineSuffix(operation, endPoint);
        //     this.MoveSection
        //         .WriteLine(" ;");

        //     // Endpoint position
        //     this.PositionSection
        //         .Write($"{this.PointNumber}{{")
        //         .Write(this.FormatRobotGroups(operation, endPoint))
        //         .Write(this.FormatPositionUfAndUt(operation, endPoint))
        //         .Write(this.FormatConfig(operation, endPoint))
        //         .Write(this.FormatPosition(operation, endPoint))
        //         .Write(this.FormatOrientation(operation, endPoint))
        //         .Write(this.FormatExternalAxisValues(operation, endPoint))
        //         .WriteLine()
        //         .WriteLine("};");
        // }

        /// <summary>
        ///     Formats the motion type at a given point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     Formatted motion type.
        /// </returns>
        internal virtual string FormatMotionType(IOperation operation, IPoint point)
        {
            if (point.MotionSpace == PointMotionSpace.JointSpace)
            {
                return "J ";
            }

            switch (point.MotionType)
            {
                case PointMotionType.Joint:
                    return "J ";
                case PointMotionType.Linear:
                    return "L ";
                case PointMotionType.Circular:
                    return "C ";
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        ///     Formats the feed or speed depending on the motion type at a given point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     Formatted feed or speed.
        /// </returns>
        /// 
        internal virtual string FormatSpeed(IOperation operation, IPoint point)
        {
            if (point.MotionSpace == PointMotionSpace.JointSpace)
            {
                return $"{this.JointSpeed}";
            }

            switch (point.MotionType)
            {
                case PointMotionType.Joint:
                    return $"{this.JointSpeed}";
                case PointMotionType.Linear:
                    return $"{this.Feedrate}";
                case PointMotionType.Circular when !point.IsArcMiddlePoint:
                    return $"{this.Feedrate}";
                default:
                    throw new NotImplementedException();
            }
        }


        /// <summary>
        ///     Outputs the file header.
        /// </summary>
        /// <param name="file">
        ///     The current file.
        /// </param>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void OutputFileHeader(ITextFile file, IOperation operation, IPoint point)
        {
            ITextPlaceholder lineCountPlaceHolder = file.RootSection.CreateTextPlaceholder("LINE_COUNT");

            file.RootSection.Header
                .WriteLine($"/PROG  {file.FileNameWithoutExtension}")
                .WriteLine($"/ATTR")
                .WriteLine($"OWNER           = MNEDITOR;")
                .WriteLine($"COMMENT         = \"BY ROBOTMASTER\";")
                .WriteLine($"PROG_SIZE       = 0;")
                .WriteLine($"CREATE          = DATE {this.Context.DateTime("yy-MM-dd")}  TIME {this.Context.DateTime("HH:mm:ss")};")
                .WriteLine($"MODIFIED        = DATE {this.Context.DateTime("yy-MM-dd")}  TIME {this.Context.DateTime("HH:mm:ss")};")
                .WriteLine($"FILE_NAME       = ;")
                .WriteLine($"VERSION         = 0;")
                .Write($"LINE_COUNT      = ").Write(lineCountPlaceHolder).WriteLine(";")
                .WriteLine($"MEMORY_SIZE     = 0;")
                .WriteLine($"PROTECT         = READ_WRITE;")
                .WriteLine($"TCD:  STACK_SIZE        = 0,")
                .WriteLine($"      TASK_PRIORITY     = 50,")
                .WriteLine($"      TIME_SLICE        = 0,")
                .WriteLine($"      BUSY_LAMP_OFF     = 0,")
                .WriteLine($"      ABORT_REQUEST     = 0,")
                .WriteLine($"      PAUSE_REQUEST     = 0;")
                .WriteLine($"DEFAULT_GROUP   = {this.FormatDefaultGroups(operation, point)};")
                .WriteLine($"CONTROL_CODE = 00000000 00000000;");
        }

        /// <summary>
        ///     Formats the main file header.
        /// </summary>
        /// <param name="mainFile">
        ///     The current file.
        /// </param>
        internal virtual void WriteMainFileHeader(ITextFile mainFile)
        {
            this.OutputFileHeader(mainFile, this.RobotProgram.FirstOperation, this.RobotProgram.FirstOperation.FirstPoint);
        }

        /// <summary>
        ///     Formats the default groups.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The formatted default groups.
        /// </returns>
        internal virtual string FormatDefaultGroups(IOperation operation, IPoint point)
        {
            //// Example: 1,1,*,*,*

            var defaultGroup = new[] { "*", "*", "*", "*", "*" };
            defaultGroup[this.RobotGroup.Number - 1] = "1";
            foreach (var fanucGroup in this.OtherGroups)
            {
                defaultGroup[fanucGroup.Number - 1] = "1";
            }

            return string.Join(",", defaultGroup);
        }

        /// <summary>
        ///     Output the user frame for a given operation.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void OutputUserFrame(IOperation operation)
        {
            var userFrameOutputType = operation.Menus.ControllerSettings.UserFrameOutputType;

            if (operation.UserFrame.Number == 0)
            {
                // Force user frame by registry for User Frame 0;
                userFrameOutputType = UserFrameOutputType.ByRegister;
            }

            if (operation.Menus.ControllerSettings.IsUserFrameOutputAsComment)
            {
                //// Example:
                ////   14: ;
                ////   15: ! USER FRAME 1 ;
                ////   16: !  X=-369.965 ;
                ////   17: !  Y=0.000 ;
                ////   18: !  Z=479.276 ;
                ////   19: !  W=0.000 ;
                ////   20: !  P=-60.000 ;
                ////   21: !  R=0.000 ;
                ////   22: ;

                this.MoveSection
                    .WriteLine(this.LineNumber.Increment() +
                               $"  ; ")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  ! {(this.Setup.Configuration.IsRTCP ? "TOOL FRAME " : "USER FRAME ")}{operation.UserFrame.Number} ;")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  !  X= {this.GenericVariable.Write(operation.UserFrame.Position.X)} ;")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  !  Y= {this.GenericVariable.Write(operation.UserFrame.Position.Y)} ;")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  !  Z= {this.GenericVariable.Write(operation.UserFrame.Position.Z)} ;")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  !  W= {this.GenericVariable.Write(operation.UserFrame.OrientationEuler.X)} ;")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  !  P= {this.GenericVariable.Write(operation.UserFrame.OrientationEuler.Y)} ;")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  !  R= {this.GenericVariable.Write(operation.UserFrame.OrientationEuler.Z)} ;")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  ; ");
            }

            switch (userFrameOutputType)
            {
                case UserFrameOutputType.Disable:
                    break;
                case UserFrameOutputType.ByRegister:
                    {
                        // TODO Output the user frame value as a comment.

                        //// Example:
                        ////    8: UFRAME_NUM = 1 ;

                        var frameLabel = this.Setup.Configuration.IsRTCP ? "UTOOL" : "UFRAME";
                        this.MoveSection
                            .WriteLine(this.LineNumber.Increment() + $"  {frameLabel}_NUM = {operation.UserFrame.Number} ;");

                        break;
                    }

                case UserFrameOutputType.ByValue:
                    {
                        //// Example:
                        ////    1: PR[3, 1]= 1160.539 ;
                        ////    2: PR[3, 2] = 0;
                        ////    3: PR[3, 3] = 4.601;
                        ////    4: PR[3, 4] = 0;
                        ////    5: PR[3, 5] = 0;
                        ////    6: PR[3, 6] = 0;
                        ////    7: UFRAME[1] = PR[3];
                        ////    8: UFRAME_NUM = 1 ;

                        var registerNumber = this.Setup.Configuration.IsRTCP
                            ? operation.Menus.ControllerSettings.ToolFrameRegisterNumber
                            : operation.Menus.ControllerSettings.UserFrameRegisterNumber;
                        var frameLabel = this.Setup.Configuration.IsRTCP ? "UTOOL" : "UFRAME";

                        this.MoveSection
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  PR[{registerNumber},1] = {this.GenericVariable.Write(operation.UserFrame.Position.X)} ;")
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  PR[{registerNumber},2] = {this.GenericVariable.Write(operation.UserFrame.Position.Y)} ;")
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  PR[{registerNumber},3] = {this.GenericVariable.Write(operation.UserFrame.Position.Z)} ;")
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  PR[{registerNumber},4] = {this.GenericVariable.Write(operation.UserFrame.OrientationEuler.X)} ;")
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  PR[{registerNumber},5] = {this.GenericVariable.Write(operation.UserFrame.OrientationEuler.Y)} ;")
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  PR[{registerNumber},6] = {this.GenericVariable.Write(operation.UserFrame.OrientationEuler.Z)} ;")
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  {frameLabel}[{operation.UserFrame.Number}] = PR[{registerNumber}] ; ")
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  {frameLabel}_NUM = {operation.UserFrame.Number} ;");
                        break;
                    }
            }
        }

        /// <summary>
        ///     Outputs the user frame for a given operation.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void OutputToolFrame(IOperation operation)
        {
            var userFrameOutputType = operation.Menus.ControllerSettings.ToolFrameOutputType;

            if (operation.UserFrame.Number == 0)
            {
                // Force tool frame by registry for Tool Frame 0;
                userFrameOutputType = ToolFrameOutputType.ByRegister;
            }

            if (operation.Menus.ControllerSettings.IsToolFrameOutputAsComment)
            {
                //// Example:
                ////   14: ;
                ////   15: ! TOOL FRAME 1 ;
                ////   16: !  X=-369.965 ;
                ////   17: !  Y=0.000 ;
                ////   18: !  Z=479.276 ;
                ////   19: !  W=0.000 ;
                ////   20: !  P=-60.000 ;
                ////   21: !  R=0.000 ;
                ////   22: ;

                this.MoveSection
                    .WriteLine(this.LineNumber.Increment() +
                               $"  ; ")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  ! {(this.Setup.Configuration.IsRTCP ? "USER FRAME " : "TOOL FRAME ")}{operation.TCPFrame.Number} ;")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  !  X= {this.GenericVariable.Write(operation.TCPFrame.Position.X)} ;")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  !  Y= {this.GenericVariable.Write(operation.TCPFrame.Position.Y)} ;")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  !  Z= {this.GenericVariable.Write(operation.TCPFrame.Position.Z)} ;")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  !  W= {this.GenericVariable.Write(operation.TCPFrame.OrientationEuler.X)} ;")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  !  P= {this.GenericVariable.Write(operation.TCPFrame.OrientationEuler.Y)} ;")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  !  R= {this.GenericVariable.Write(operation.TCPFrame.OrientationEuler.Z)} ;")
                    .WriteLine(this.LineNumber.Increment() +
                               $"  ; ");
            }

            switch (userFrameOutputType)
            {
                case ToolFrameOutputType.Disable:
                    break;
                case ToolFrameOutputType.ByRegister:
                    {
                        // TODO Output the tool frame value as a comment.

                        //// Example:
                        ////    16: UTOOL_NUM = 8;

                        var frameLabel = this.Setup.Configuration.IsRTCP ? "UFRAME" : "UTOOL";
                        this.MoveSection
                            .WriteLine(this.LineNumber.Increment() + $"  {frameLabel}_NUM = {operation.TCPFrame.Number} ;");

                        break;
                    }

                case ToolFrameOutputType.ByValue:
                    {
                        //// Example:
                        ////    9: PR[4, 1] = -352.64472;
                        ////    10: PR[4, 2] = 0;
                        ////    11: PR[4, 3] = 469.275923;
                        ////    12: PR[4, 4] = 0;
                        ////    13: PR[4, 5] = -60.000527;
                        ////    14: PR[4, 6] = 0;
                        ////    15: UTOOL[8] = PR[4];
                        ////    16: UTOOL_NUM = 8;

                        var registerNumber = this.Setup.Configuration.IsRTCP
                            ? operation.Menus.ControllerSettings.UserFrameRegisterNumber
                            : operation.Menus.ControllerSettings.ToolFrameRegisterNumber;
                        var frameLabel = this.Setup.Configuration.IsRTCP ? "UFRAME" : "UTOOL";

                        this.MoveSection
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  PR[{registerNumber},1] = {this.GenericVariable.Write(operation.TCPFrame.Position.X)} ;")
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  PR[{registerNumber},2] = {this.GenericVariable.Write(operation.TCPFrame.Position.Y)} ;")
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  PR[{registerNumber},3] = {this.GenericVariable.Write(operation.TCPFrame.Position.Z)} ;")
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  PR[{registerNumber},4] = {this.GenericVariable.Write(operation.TCPFrame.OrientationEuler.X)} ;")
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  PR[{registerNumber},5] = {this.GenericVariable.Write(operation.TCPFrame.OrientationEuler.Y)} ;")
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  PR[{registerNumber},6] = {this.GenericVariable.Write(operation.TCPFrame.OrientationEuler.Z)} ;")
                            .WriteLine(this.LineNumber.Increment() +
                                       $"  {frameLabel}[{operation.TCPFrame.Number}] = PR[{registerNumber}] ; ")
                            .WriteLine(this.LineNumber.Increment() + $"  {frameLabel}_NUM = {operation.TCPFrame.Number} ;");
                        break;
                    }
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
        internal virtual string FormatPositioningPath(IOperation operation, IPoint point)
        {
            //// Example: CNT100

            switch (point.MotionType)
            {
                case PointMotionType.Joint when point.MotionSpace == PointMotionSpace.JointSpace:
                case PointMotionType.Joint when point.MotionSpace == PointMotionSpace.CartesianSpace:
                    switch (operation.Menus.MotionSettings.JointMotionTerminationType)
                    {
                        case JointMotionTerminationType.Fine:
                            return " FINE";
                        case JointMotionTerminationType.Continuous:
                            return $" CNT{operation.Menus.MotionSettings.CntJointPositioningPathValue}";
                        case JointMotionTerminationType.NotSpecified:
                        default:
                            return string.Empty;
                    }

                case PointMotionType.Linear:
                    switch (operation.Menus.MotionSettings.LinearMotionTerminationType)
                    {
                        case LinearMotionTerminationType.Fine:
                            return " FINE";
                        case LinearMotionTerminationType.Continuous:
                            return $" CNT{operation.Menus.MotionSettings.CntLinearPositioningPathValue}";
                        case LinearMotionTerminationType.NotSpecified:
                        default:
                            return string.Empty;
                    }

                case PointMotionType.Circular:
                    switch (operation.Menus.MotionSettings.CircularMotionTerminationType)
                    {
                        case CircularMotionTerminationType.Fine:
                            return " FINE";
                        case CircularMotionTerminationType.Continuous:
                            return $" CNT{operation.Menus.MotionSettings.CntCircularPositioningPathValue}";
                        case CircularMotionTerminationType.NotSpecified:
                        default:
                            return string.Empty;
                    }

                default:
                    return string.Empty;
            }
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
        internal virtual string FormatAcceleration(IOperation operation, IPoint point)
        {
            //// Example: ACC100

            switch (point.MotionType)
            {
                case PointMotionType.Joint when point.MotionSpace == PointMotionSpace.JointSpace:
                    return string.Empty;
                case PointMotionType.Joint when point.MotionSpace == PointMotionSpace.CartesianSpace &&
                                                operation.Menus.MotionSettings
                                                    .IsJointAccelerationDecelerationEnabled:
                    return $" ACC{operation.Menus.MotionSettings.JointAccelerationAndDecelerationForJointMotion}";
                case PointMotionType.Linear
                    when operation.Menus.MotionSettings.IsLinearAccelerationDecelerationEnabled:
                    return $" ACC{operation.Menus.MotionSettings.JointAccelerationAndDecelerationForLinearMotion}";
                case PointMotionType.Circular
                    when operation.Menus.MotionSettings.IsCircularAccelerationDecelerationEnabled:
                    return
                        $" ACC{operation.Menus.MotionSettings.JointAccelerationAndDecelerationForCircularMotion}";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        ///     Formats the additional motion instructions at a given point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The formatted additional motion instructions.
        /// </returns>
        internal virtual string FormatAdditionalMotionInstructions(IOperation operation, IPoint point)
        {
            string extraMotionInstructions = string.Empty;

            // Path instruction PTH
            if (operation.Menus.MotionSettings.IsPthMotionEnabled)
            {
                switch (point.MotionType)
                {
                    // Only works with CNT
                    case PointMotionType.Joint
                        when point.MotionSpace == PointMotionSpace.CartesianSpace
                             && operation.Menus.MotionSettings.JointMotionTerminationType ==
                             JointMotionTerminationType.Continuous:
                    case PointMotionType.Linear
                        when operation.Menus.MotionSettings.LinearMotionTerminationType ==
                             LinearMotionTerminationType.Continuous:
                    case PointMotionType.Circular
                        when operation.Menus.MotionSettings.CircularMotionTerminationType ==
                             CircularMotionTerminationType.Continuous:
                        extraMotionInstructions += " PTH";
                        break;
                }
            }

            // Minimum Rotation MROT  ;
            if (operation.Menus.MotionSettings.IsMinimumRotationEnabled &&
                point.MotionType == PointMotionType.Joint &&
                point.MotionSpace == PointMotionSpace.CartesianSpace)
            {
                extraMotionInstructions += " MROT";
            }

            // Coordinated Motion statement COORD;
            if (operation.Menus.MotionSettings.IsCoordinatedMotionEnabled)
            {
                // The COORD motion option applies only to linear (LC) and circular (CC) motion instructions.
                if (point.MotionType == PointMotionType.Linear || point.MotionType == PointMotionType.Circular)
                {
                    extraMotionInstructions += " COORD";
                }
            }

            if (this.Setup.Configuration.IsRTCP && point.MotionType != PointMotionType.Joint)
            {
                extraMotionInstructions += " RTCP";
            }

            return extraMotionInstructions;
        }

        /// <summary>
        ///     Formats the robot group.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The formatted robot group.
        /// </returns>
        internal virtual string FormatRobotGroups(IOperation operation, IPoint point)
        {
            // Example:    GP1:
            return $"\r\n   GP{this.RobotGroup.Number}:";
        }

        /// <summary>
        ///     Formats the user frame and tool frame used by <paramref name="operation"/>.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The formatted user frame and tool frame.
        /// </returns>
        internal virtual string FormatPositionUfAndUt(IOperation operation, IPoint point)
        {
            int ufNumber = operation.UserFrame.Number;
            int utNumber = operation.TCPFrame.Number;

            if (this.Setup.Configuration.IsRTCP)
            {
                // Swap the UF and UT values if the current configuration is RTCP
                ufNumber = operation.TCPFrame.Number;
                utNumber = operation.UserFrame.Number;
            }

            // Example:    UF: 1, UT: 1
            return $"\r\n\tUF : {ufNumber},\tUT : {utNumber}";
        }

        /// <summary>
        ///     Formats the robot configuration at a given point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The formatted robot configuration.
        /// </returns>
        internal virtual string FormatConfig(IOperation operation, IPoint point)
        {
            // Example:, CONFIG : 'N U T, 0, 0, 0'
            return $",\tCONFIG : '{this.FormatConfigBits(operation, point)},{this.FormatTurnBits(operation, point)}'";
        }

        /// <summary>
        ///     Formats the calculated configuration bits at a given point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The formatted configuration bits.
        /// </returns>
        internal virtual string FormatConfigBits(IOperation operation, IPoint point)
        {
            // "F" Flip "N" No flip
            // "L" Left "R" Right (multi-turn J5)
            // "U" Up "D" Down
            // "T" Front "B" Back

            //// Example : N U T
            return (point.RobotConfiguration.IsWristPositive ? "F" : "N") + " " +
                   (point.RobotConfiguration.IsElbowUp ? "U" : "D") + " " +
                   (point.RobotConfiguration.IsBaseFront ? "T" : "B");
        }

        /// <summary>
        ///     Formats the calculated turn bits at a given point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The formatted turn bits.
        /// </returns>
        internal virtual string FormatTurnBits(IOperation operation, IPoint point)
        {
            ////  Example: 0, 0, 0
            var j1 = point.GetJointValue(this.RobotGroup.RobotJoints[0].RobotmasterId);
            var j4 = point.GetJointValue(this.RobotGroup.RobotJoints[3].RobotmasterId);
            var j6 = point.GetJointValue(this.RobotGroup.RobotJoints[5].RobotmasterId);

            return
                $" {Math.Sign(j1) * (int)((Math.Abs(j1) + 180) / 360)}," +
                $" {Math.Sign(j4) * (int)((Math.Abs(j4) + 180) / 360)}," +
                $" {Math.Sign(j6) * (int)((Math.Abs(j6) + 180) / 360)}";
        }

        /// <summary>
        ///     Formats the robot joint values at a given point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The formatted joint space (JJ) position.
        /// </returns>
        internal virtual string FormatRobotJointValues(IOperation operation, IPoint point)
        {
            /* Example:
            *     J1 = 0.00 deg,       J2 = 0.00 deg,       J3 = 0.00 deg,
            *     J4 = 0.00 deg,       J5 = 0.00 deg,       J6 = 0.00 deg
            */

            string output = string.Empty;
            var i = 0;
            foreach (FanucJoint robotJoint in this.RobotGroup.RobotJoints)
            {
                // Add a line return every 3 values
                output += "," + ((i % 3 == 0) ? "\r\n" : string.Empty);
                output += "\t" + robotJoint.FanucId + " = ";
                output += robotJoint.IsRevolute ?
                    this.GenericAngleVariable.Write(point.GetJointValue(robotJoint.RobotmasterId)) :
                    this.GenericLengthVariable.Write(point.GetJointValue(robotJoint.RobotmasterId)); // formatted joint value with units
                i++;
            }

            return output;
        }

        /// <summary>
        ///     Formats external axis positions at a given point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The formatted external axis positions.
        /// </returns>
        internal virtual string FormatExternalAxisValues(IOperation operation, IPoint point)
        {
            /* Example:
             *          E1 =    36.10 deg
             *    GP2:
             *          UF : 1, UT : 1,
             *          J1 = 55.00 deg,       J2 = 8.00 deg
             */

            string output = string.Empty;

            // External axis in the robot group
            var i = 0;
            foreach (FanucJoint externalJointInRobotGroup in this.RobotGroup.ExternalJoints)
            {
                // Example : E1 =    36.10 deg
                output += "," + ((i % 3 == 0) ? "\r\n" : string.Empty); // next line every 3 values
                output += "\t" + externalJointInRobotGroup.FanucId + " = ";
                output += externalJointInRobotGroup.IsRevolute ?
                    this.GenericAngleVariable.Write(point.GetJointValue(externalJointInRobotGroup.RobotmasterId)) :
                    this.GenericLengthVariable.Write(point.GetJointValue(externalJointInRobotGroup.RobotmasterId)); // formatted joint value with units
                i++;
            }

            // External axis in other groups
            foreach (FanucGroup group in this.OtherGroups)
            {
                // Example:
                //  GP2:
                //         UF : 1, UT : 1,
                //         J1 = 55.00 deg,       J2 = 8.00 deg
                output += $"\r\n   GP{group.Number}:";
                output += this.FormatPositionUfAndUt(operation, point);
                i = 0;
                foreach (FanucJoint externalJointOutsideRobotGroup in group.ExternalJoints)
                {
                    output += "," + ((i % 3 == 0) ? "\r\n" : string.Empty); // next line every 3 values
                    output += "\t" + externalJointOutsideRobotGroup.FanucId + " = ";
                    output += externalJointOutsideRobotGroup.IsRevolute ?
                        this.GenericAngleVariable.Write(point.GetJointValue(externalJointOutsideRobotGroup.RobotmasterId)) :
                        this.GenericLengthVariable.Write(point.GetJointValue(externalJointOutsideRobotGroup.RobotmasterId)); // formatted joint value with units
                    i++;
                }
            }

            return output;
        }

        /// <summary>
        ///     Formats the position of a given point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The formatted position.
        /// </returns>
        internal virtual string FormatPosition(IOperation operation, IPoint point)
        {
            // Example:        X = -5.00  mm,       Y = -15.00  mm,       Z = 100.00  mm
            return
                $",\r\n" +
                $"\tX = {this.GenericLengthVariable.Write(point.Position.X)}," +
                $"\tY = {this.GenericLengthVariable.Write(point.Position.Y)}," +
                $"\tZ = {this.GenericLengthVariable.Write(point.Position.Z)}";
        }

        /// <summary>
        ///     Formats the orientation of a given point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        /// <returns>
        ///     The formatted orientation.
        /// </returns>
        internal virtual string FormatOrientation(IOperation operation, IPoint point)
        {
            // Example:       W = 180.00 deg,       P = 0.00 deg,       R = 0.00 deg
            return
                $",\r\n" +
                $"\tW = {this.GenericAngleVariable.Write(point.OrientationEuler.X)}," +
                $"\tP = {this.GenericAngleVariable.Write(point.OrientationEuler.Y)}," +
                $"\tR = {this.GenericAngleVariable.Write(point.OrientationEuler.Z)}";
        }

        /// <summary>
        ///     Finalizes the data after the robot program output.
        /// </summary>
        internal virtual void FinalizeDataAfterRobotProgram()
        {
            this.FinalizeFileStructure();
        }

        /// <summary>
        ///     Generates the robot code files.
        /// </summary>
        internal virtual void GenerateFiles()
        {
            // No file output if removed. Files are written on disk after this call.
            this.RootDirectory.GenerateFiles();
        }

        /// <summary>
        ///     Outputs the robot code of all events before the <paramref name="point"/> output.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void OutputBeforePointEvents(IOperation operation, IPoint point)
        {
            foreach (var beforeEvent in point.Events.BeforeEvents)
            {
                this.OutputBeforePointEvent(operation, point, beforeEvent);
            }
        }

        /// <summary>
        ///     Outputs the robot code of all events inline with the <paramref name="point"/> output.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void OutputInlineEvents(IOperation operation, IPoint point)
        {
            foreach (var inlineEvent in point.Events.InlineEvents)
            {
                this.OutputInlineEvent(operation, point, inlineEvent);
            }
        }

        /// <summary>
        ///     Outputs the robot code of all events after the <paramref name="point"/> output.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void OutputAfterPointEvents(IOperation operation, IPoint point)
        {
            foreach (var afterEvent in point.Events.AfterEvents)
            {
                this.OutputAfterPointEvent(operation, point, afterEvent);
            }
        }

        /// <summary>
        ///     This class represents a Fanuc group (GP1, GP2,...).
        ///     A group can contain robot joints and external joints.
        /// </summary>
        internal class FanucGroup
        {
            /// <summary>
            ///     Gets or sets the group number (1 for GP1,...).
            /// </summary>
            internal int Number { get; set; }

            /// <summary>
            ///     Gets or sets the list of robot joints in the group.
            ///     The values of these joints are not output for Cartesian moves.
            /// </summary>
            internal List<FanucJoint> RobotJoints { get; set; } = new List<FanucJoint>();

            /// <summary>
            ///     Gets or sets the list of external axis joints in the group.
            /// </summary>
            internal List<FanucJoint> ExternalJoints { get; set; } = new List<FanucJoint>();
        }

        /// <summary>
        ///     This class represents a Fanuc axis.
        /// </summary>
        internal class FanucJoint
        {
            /// <summary>
            ///     Gets or sets the unique Robotmaster joint id (J1, GP1_E1, GP2_J1, ROB2_J1,...).
            /// </summary>
            internal string RobotmasterId { get; set; }

            /// <summary>
            ///     Gets or sets the Fanuc joint id (J1, E1,...).
            ///     This should be unique within a group but not across all groups.
            /// </summary>
            internal string FanucId { get; set; }

            /// <summary>
            ///     Gets or sets a value indicating whether an axis is revolute (rotary).
            ///     The axis is assumed to be a prismatic (rail) otherwise.
            /// </summary>
            internal bool IsRevolute { get; set; }
        }
    }

    /// <summary>
    ///     Extension methods for the Fanuc processor.
    /// </summary>
#pragma warning disable SA1402
#pragma warning disable SA1204
    internal static class FanucExtensions
#pragma warning restore SA1204
#pragma warning restore SA1402
    {
        /// <summary>
        ///     Caps the length of the string to the provided number of characters.
        ///     Strings shorter than the max number of characters will be unchanged.
        /// </summary>
        /// <param name="s">
        ///     The string to modify.
        /// </param>
        /// <param name="maxLength">
        ///     The max number of characters the string can be in length.
        /// </param>
        /// <returns>
        ///     A formatted string that will be no longer than the max number of characters provided.
        /// </returns>
        public static string CapLength(this string s, int maxLength)
        {
            return s.Length <= maxLength ? s : s.Substring(0, maxLength);
        }
    }
}