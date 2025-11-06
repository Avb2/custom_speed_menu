// <copyright file="AdditivePostProcessor.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc.AdditiveProcess
{
    using Robotmaster.Processor.Core.Common.Enums;
    using Robotmaster.Processor.Core.Common.Interfaces.FileFramework;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <summary>
    ///     The Additive Manufacturing post processor.
    /// </summary>
    internal class AdditivePostProcessor : PostProcessor
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="AdditivePostProcessor"/> class.
        ///     This class inherits from <see cref="PostProcessor"/> therefore all <see cref="PostProcessor"/>'s
        ///     fields, properties, and methods are accessible and override-able here.
        /// </summary>
        /// <param name="program">
        ///     The current program.
        /// </param>
        /// <param name="context">
        ///     The current context.
        /// </param>
        internal AdditivePostProcessor(IProgram program, IPostProcessorContext context)
            : base(program, context)
        {
            //// BASE - Default constructor - LEAVE EMPTY
        }

        /// <inheritdoc/>
        internal override void OutputFileHeader(ITextFile file, IOperation operation, IPoint point)
        {
            //// BASE - Default output
            base.OutputFileHeader(file, operation, point);

            //// ADDITIVE MANUFACTURING -  /APPL header section output
            file.RootSection.Header
                .WriteLine($"/APPL")
                .Indent(2)
                .WriteLine($"{operation.Menus.AdditiveSettings.ApplicationName};");
        }

        /// <inheritdoc/>
        internal override string FormatSpeed(IOperation operation, IPoint point)
        {
            if (operation.OperationType == OperationType.TaskOperation
                && point.Flags.IsInProcess)
            {
                return " WELD_SPEED";
            }

            return base.FormatSpeed(operation, point);
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
