// <copyright file="PlasmaCuttingPostProcessor.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc.PlasmaCuttingProcess
{
    using Robotmaster.Processor.Core.Common.Enums.Applications.Points;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Events.Interfaces;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <summary>
    ///     The Plasma Cutting post processor.
    /// </summary>
    internal partial class PlasmaCuttingPostProcessor : PostProcessor
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PlasmaCuttingPostProcessor"/> class.
        ///     This class inherits from <see cref="PostProcessor"/> therefore all <see cref="PostProcessor"/>'s
        ///     fields, properties, and methods are accessible and override-able here.
        /// </summary>
        /// <param name="program">
        ///     The current program.
        /// </param>
        /// <param name="context">
        ///     The current context.
        /// </param>
        internal PlasmaCuttingPostProcessor(IProgram program, IPostProcessorContext context)
            : base(program, context)
        {
            //// BASE - Default constructor - LEAVE EMPTY
        }

        /// <summary>
        ///     Gets or sets a value for plasma cutting slowdown speed.
        ///     This value affected by the <see cref="IPlasmaSpeedOverride"/> event.
        /// </summary>
        internal virtual double PlasmaSpeedOverride { get; set; }

        /// <inheritdoc />
        internal override string FormatSpeed(IOperation operation, IPoint point)
        {
            //// Plasma cutting output (custom)
            //// CUSTOMIZATION - Insert customization below

            // If there is a valid value set for the Slowdown speed, replace the feedrate of In Cut moves with the Slowdown speed
            if (point.ApplicationData.PlasmaCutting?.PointType == PlasmaCuttingPointType.InCut
                && this.PlasmaSpeedOverride > 0)
            {
                return $"{this.Feedrate.Write(this.PlasmaSpeedOverride)}";
            }

            //// Default output (base)
            return base.FormatSpeed(operation, point);  // DO NOT REMOVE
        }
    }
}
