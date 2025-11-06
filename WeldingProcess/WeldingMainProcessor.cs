// <copyright file="WeldingMainProcessor.cs" company="Hypertherm Inc.">
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
    ///     This is the top-level post processor for the Fanuc brand.
    /// </summary>
    internal partial class WeldingMainProcessor : MainProcessor
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="WeldingMainProcessor"/> class.
        ///     This class inherits from <see cref="MainProcessor"/> therefore all <see cref="MainProcessor"/>'s
        ///     fields, properties and methods are accessible and overridable here.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="context">
        ///     The current context.
        /// </param>
        internal WeldingMainProcessor(IOperation operation, IMainProcessorContext context)
            : base(operation, context)
        {
            //// BASE - Default constructor - LEAVE EMPTY
        }

        /// <summary>
        ///     Edits the macro call based on the tool change menu.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void EditOperationMacroCall(IOperation operation)
        {
            if (operation is IMacroOperation macroOperation
                && macroOperation.IsExitOperation
                && operation.Menus.WeldingSettings.IsMacroNameCallEnabled)
            {
                this.AddMacroEvent(operation, operation.FirstPoint);
            }
        }

        /// <summary>
        /// Modifies a welding operation.
        /// </summary>
        /// <param name="operation">The operation.</param>
        internal virtual void EditWeldingOperation(IOperation operation)
        {
            this.ResetTouchRegister(operation);
        }
    }
}
