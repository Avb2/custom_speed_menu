// <copyright file="PostProcessorMultiRobot.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Robotmaster.Processor.Core.Common.Interfaces;
    using Robotmaster.Processor.Core.Common.Interfaces.FileFramework;
    using Robotmaster.Processor.Fanuc.Generated.Classes;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <summary>
    ///     A partial post-processor that handles all multi-robot related processor logic.
    /// </summary>
    internal partial class PostProcessor
    {
        /// <summary>
        ///     Gets or sets the all robots active at least once in the session.
        /// </summary>
        internal List<IRobot> ActiveRobots { get; set; }

        /// <summary>
        ///     The mulit-robot program-level post processor entry point.
        /// </summary>
        internal void OutputMultiRobotSchedulerProgram()
        {
            if (this.ActiveRobots.Any(r => !r.CustomValues.ContainsKey(CustomValueKeys.MashGrpNumber) || !r.CustomValues.ContainsKey(CustomValueKeys.RobotNumber)))
            {
                this.Context.NotifyUser(
                    "Processor Warning: One or more of the robots in the cell does not contain a custom value with the key: " + CustomValueKeys.RobotNumber + "or" + CustomValueKeys.MashGrpNumber + "." + Environment.NewLine +
                    "The scheduler program will not be generated and some multi-robot features will not be supported.");
                return;
            }

            this.InitializeScheduleFile();
            this.SchedulePrograms();
        }

        /// <summary>
        ///     Initializes the scheduler file structure before the first program output.
        /// </summary>
        internal void InitializeScheduleFile()
        {
            // Initialize this.RobotProgram to enable getting the target root directory
            // Use the first robot program that contains operations so settings data can be accessed
            this.RobotProgram = this.Program.RobotPrograms.First(rp => rp.Operations.Count > 0);

            // Initialize scheduler file post variables
            this.LineNumber = this.RobotProgram.FirstOperation.Menus.ControllerSettings.IsLineNumberEnabled ?
                this.Context.CreateIntegerPostVariable(0, "{0,4:0}:") : this.LineNumber = this.Context.CreateIntegerPostVariable(0, "    :");

            // If program is batched, create parent sub directory for program output. Else, use target directory.
            var rootDirectory = this.GetTargetRootDirectory();

            // Create the schedule file. Trim the file extension and limit its length so the final name is within 36 characters.
            this.CurrentFile = rootDirectory.CreateChildTextFile(FormatFileName($"{this.Context.Session.FileName.Split('.')[0].CapLength(26)}_SCHEDULER"), ".LS");

            // Add the header and basic formatting to the schedule file.
            this.StartScheduleFile(this.CurrentFile);
        }

        /// <summary>
        ///     Starts the scheduler file formatting following the brand's multi-arm package convention.
        /// </summary>
        /// <param name="schedulerFile">
        ///     The scheduler file.
        /// </param>
        internal virtual void StartScheduleFile(ITextFile schedulerFile)
        {
            var movesSection = schedulerFile.RootSection.CreateChildSection("MOVES"); // Section for moves

            this.OutputSchedulerFileHeader(schedulerFile);

            movesSection.Header.WriteLine("/MN");
            movesSection.Footer.WriteLine("/END");
        }

        /// <summary>
        ///     Outputs the scheduler file header.
        /// </summary>
        /// <param name="schedulerFile">
        ///     The current scheduler file.
        /// </param>
        internal virtual void OutputSchedulerFileHeader(ITextFile schedulerFile)
        {
            ITextPlaceholder lineCountPlaceHolder = schedulerFile.RootSection.CreateTextPlaceholder("LINE_COUNT");

            schedulerFile.RootSection.Header
                .WriteLine($"/PROG  {schedulerFile.FileNameWithoutExtension}")
                .WriteLine($"/ATTR")
                .WriteLine($"OWNER           = MNEDITOR;")
                .WriteLine($"COMMENT         = \"BY ROBOTMASTER\";")
                .WriteLine($"PROG_SIZE       = 0;")
                .WriteLine($"CREATE          = DATE {this.Context.DateTime("yy-MM-dd")}  TIME {this.Context.DateTime("HH:mm:ss")};")
                .WriteLine($"MODIFIED        = DATE {this.Context.DateTime("yy-MM-dd")}  TIME {this.Context.DateTime("HH:mm:ss")};")
                .WriteLine($"FILE_NAME       = ;")
                .WriteLine($"VERSION         = 0;")
                .WriteLine($"LINE_COUNT      = 0;")
                .WriteLine($"MEMORY_SIZE     = 0;")
                .WriteLine($"PROTECT         = READ_WRITE;")
                .WriteLine($"TCD:  STACK_SIZE        = 0,")
                .WriteLine($"      TASK_PRIORITY     = 50,")
                .WriteLine($"      TIME_SLICE        = 0,")
                .WriteLine($"      BUSY_LAMP_OFF     = 0,")
                .WriteLine($"      ABORT_REQUEST     = 0,")
                .WriteLine($"      PAUSE_REQUEST     = 0;")
                .WriteLine($"DEFAULT_GROUP   = *,*,*,*,*;") // Do not set any motion groups, since this file will calling other tasks with defined motion groups.
                .WriteLine($"CONTROL_CODE = 00000000 00000000;");
        }

        /// <summary>
        ///     Add all robot programs, with the appropriate sync commands, to the schedule program.
        /// </summary>
        internal void SchedulePrograms()
        {
            var movesSection = this.CurrentFile.RootSection.ChildSections["MOVES"];

            // Output the session name a s a comment
            // Example: :  ! WeldPRO Multi-Arc Sample.RM Scheduler ;
            movesSection.Write(this.LineNumber.Increment())
                .WriteLine($"  ! {this.Context.Session.FileName} Scheduler ;");

            // Output robot flag resets, calls to robot programs, and waits for each robot program in the session.
            // Example:
            /* :  F[1:TASK DONE]=(OFF) ;
             * :  F[2:TASK DONE]=(OFF) ;
             * :  RUN PROG1_R1 ;
             * :  RUN PROG1_R2 ;
             * :  WAIT (F[1:TASK DONE]) ;
             * :  WAIT (F[2:TASK DONE]) ;
             * :  WAIT 0.01 (sec) ;
             * :  F[3:TASK DONE]=(OFF) ;
             * :  RUN PROG1_R1 ;
             * :  WAIT (F[3:TASK DONE]) ;
             * :  WAIT 0.01 (sec) ;
             * */
            foreach (var program in this.Scheduler.OrderedPrograms)
            {
                // Output TASK DONE flag reset for each robot.
                foreach (var robotProgram in program.RobotPrograms)
                {
                    // Do not output program calls for empty programs
                    if (robotProgram.Operations.Count is 0)
                    {
                        continue;
                    }

                    // Example: F[1:TASK DONE]=(OFF) ;
                    movesSection
                        .Write(this.LineNumber.Increment())
                        .WriteLine($"  F[{robotProgram.Robot.CustomValues[CustomValueKeys.RobotNumber]}:TASK DONE]=(OFF) ;");
                }

                // Output RUN command for each robot program.
                foreach (var robotProgram in program.RobotPrograms)
                {
                    // Do not output program calls for empty programs
                    if (robotProgram.Operations.Count is 0)
                    {
                        continue;
                    }

                    // Example: RUN PROG1_R1 ;
                    movesSection
                        .Write(this.LineNumber.Increment())
                        .WriteLine($"  RUN {FormatFileName(robotProgram.Name)} ;");
                }

                // Output WAIT command for each robot flag.
                foreach (var robotProgram in program.RobotPrograms)
                {
                    // Do not output program calls for empty programs
                    if (robotProgram.Operations.Count is 0)
                    {
                        continue;
                    }

                    // Example:  WAIT (F[1:TASK DONE]) ;
                    movesSection
                        .Write(this.LineNumber.Increment())
                        .WriteLine($"  WAIT (F[{robotProgram.Robot.CustomValues[CustomValueKeys.RobotNumber]}:TASK DONE]) ;");
                }

                // Do not output buffer for empty programs
                if (program.RobotPrograms.Any(rp => rp.Operations.Count > 0))
                {
                    // Output an additional WAIT time command to avoid an INTP-267 error
                    // Example:  WAIT 0.01 (sec) ;
                    movesSection
                        .Write(this.LineNumber.Increment())
                        .WriteLine($"  WAIT {this.RobotProgram.FirstOperation.Menus.ControllerSettings.SchedulerTimeBuffer} (sec) ;");
                }
            }
        }

        /// <summary>
        ///     Checks if a program is the first program in the Operations List.
        /// </summary>
        /// <param name="program">
        ///     The program to check.
        /// </param>
        /// <returns>
        ///     Boolean representing whether or not the program is the first program in the Operations List.
        /// </returns>
        internal bool IsFirstProgram(IProgram program)
        {
            IProgram firstNonEmptyProgram = this.Cell.Setups
                .SelectMany(setup => setup.Programs)
                .FirstOrDefault(p => program.RobotPrograms.Any(rp => rp.Operations.Count > 0));

            // Check the first program in the first non-empty setup
            if (firstNonEmptyProgram != program)
            {
                return false;
            }

            // If this program is the first non-empty program, return true
            return true;
        }

        /// <summary>
        ///     Gets all robots actively used in the session.
        /// </summary>
        /// <returns>
        ///     A list of robots.
        /// </returns>
        internal List<IRobot> GetActiveRobotsInCell()
        {
            var robotPrograms = this.Cell.Setups.SelectMany(s => s.Programs).SelectMany(p => p.RobotPrograms);
            var activeRobots = robotPrograms.Select(rp => rp.Robot).Distinct().ToList();
            return activeRobots;
        }
    }
}
