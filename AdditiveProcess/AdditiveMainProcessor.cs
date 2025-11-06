// <copyright file="AdditiveMainProcessor.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc.AdditiveProcess
{
    using Robotmaster.Processor.Core.Common.Enums;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <summary>
    ///     The Additive Manufacturing main processor.
    /// </summary>
    internal partial class AdditiveMainProcessor : MainProcessor
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="AdditiveMainProcessor"/> class.
        ///     This class inherits from <see cref="MainProcessor"/> therefore all <see cref="MainProcessor"/>'s
        ///     fields, properties, and methods are accessible and override-able here.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="context">
        ///     The current context.
        /// </param>
        internal AdditiveMainProcessor(IOperation operation, IMainProcessorContext context)
            : base(operation, context)
        {
            //// BASE - Default constructor - LEAVE EMPTY
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
            if (operation.OperationType == OperationType.TaskOperation)
            {
                if (point.Flags.IsFirstPointOfContact)
                {
                    this.AddProcessActivationEvent(operation, point);
                }
                else if (point.Flags.IsLastPointOfContact)
                {
                    this.AddProcessDeactivationEvent(operation, point);
                }
            }
        }
    }
}
