// <copyright file="MainProcessor.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc
{
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <summary>
    ///     The top-level main processor class of the brand.
    ///     It contains the code that runs per operation when "Calculate" is clicked in the Robotmaster UI.
    /// </summary>
    internal partial class MainProcessor
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MainProcessor"/> class.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="context">
        ///     The current context.
        /// </param>
        internal MainProcessor(IOperation operation, IMainProcessorContext context)
        {
            this.Context = context;
            this.Cell = context.Cell;
            this.Setup = context.Setup;
            this.Program = context.Program;
            this.RobotProgram = context.RobotProgram;
            this.Operation = operation;
        }

        /// <summary>
        ///     Gets the current context.
        /// </summary>
        internal IMainProcessorContext Context { get; }

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
        ///     Gets the current robot program.
        /// </summary>
        internal IRobotProgram RobotProgram { get; }

        /// <summary>
        ///     Gets the current operation.
        /// </summary>
        internal IOperation Operation { get; }

        /// <summary>
        ///     The main processor entry point.
        /// </summary>
        internal void Run()
        {
            if (!this.IsMainProcessorInputValid())
            {
                return;
            }

            this.EditOperation(this.Operation);
            for (var point = this.Operation.FirstPoint; point != null; point = point.NextPoint)
            {
                this.EditPoint(this.Operation, point);
            }
        }

        /// <summary>
        ///     Gets a value indicating whether the processor inputs are valid.
        ///     If <c>false</c> post processing and file generation will be canceled.
        /// </summary>
        /// <returns>
        ///     <c>true</c> if the inputs are valid; otherwise, <c>false</c>.
        /// </returns>
        internal virtual bool IsMainProcessorInputValid()
        {
            // Can add checks here as needed.
            return true;
        }
    }
}
