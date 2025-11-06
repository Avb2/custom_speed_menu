// <copyright file="PlasmaCuttingMainProcessorCustom.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc.PlasmaCuttingProcess
{
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.PlasmaCuttingSettings;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <summary>
    ///     Easy-to-customize section of the main processor.
    ///     This customizable section is part of the main processor class therefore
    ///     all the fields, properties, and methods are accessible here.
    /// </summary>
    internal partial class PlasmaCuttingMainProcessor : MainProcessor
    {
        /// <summary>
        ///     Adds plasma comments to an operation.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void AddPlasmaOperationComments(IOperation operation)
        {
            //// PLASMA CUTTING - Add header comments for relevant cutting information
            operation.FirstPoint.Events.CreateCommentEvent()
                .SetCommentText($"Current: {operation.ApplicationData.PlasmaCutting.StandardSettings.Current}A")
                .AddBefore();
            operation.FirstPoint.Events.CreateCommentEvent()
                .SetCommentText($"Consumable height: {operation.Tool.Length - operation.ApplicationData.PlasmaCutting.StandardSettings.CutHeight:##.###} mm")
                .AddBefore();
            operation.FirstPoint.Events.CreateCommentEvent()
                .SetCommentText(
                    $"Cut height: {operation.ApplicationData.PlasmaCutting.StandardSettings.CutHeight} mm")
                .AddBefore();
        }

        /// <summary>
        ///   Adds the set process events.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void AddSetProcessEvents(IOperation operation, IPoint point)
        {
            //// PLASMA CUTTING
            // After the set process point.
            // Add a set process event.
            // Example:
            //     PlasmaSetProcess
            if (operation.Menus.PlasmaCuttingSettings.PlasmaProcessOutputType != PlasmaProcessOutputType.Disabled)
            {
                point.Events
                    .CreatePlasmaProcessEvent()
                    .SetAsProgramCall(operation.Menus.PlasmaCuttingSettings.PlasmaProcessOutputType == PlasmaProcessOutputType.AsProgramCall)
                    .SetPlasmaProcessCommand(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.PlasmaProcessCommand, operation))
                    .SetPlasmaProcessArguments(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.PlasmaProcessArguments, operation))
                    .AddAfter();
            }

            if (!string.IsNullOrEmpty(operation.Menus.PlasmaCuttingSettings.PlasmaProcessComment))
            {
                point.Events.CreateCommentEvent()
                    .SetCommentText(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.PlasmaProcessComment, operation))
                    .AddAfter();
            }
        }

        /// <summary>
        ///     Edits the IHS Origin point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void EditIhsOriginPoint(IOperation operation, IPoint point)
        {
            // See EditPlasmaOperationTouchSection for the touch sensing output
        }

        /// <summary>
        ///     Edits the IHS Target point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void EditIhsTargetPoint(IOperation operation, IPoint point)
        {
            // See EditPlasmaOperationTouchSection for the touch sensing output
        }

        /// <summary>
        ///   Adds the plasma on events.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void AddPlasmaOnEvents(IOperation operation, IPoint point)
        {
            //// PLASMA CUTTING
            // After the transfer point.
            // Add a plasma On event.
            // Example:
            //     PlasmaOn
            if (operation.Menus.PlasmaCuttingSettings.PlasmaOnOutputType != PlasmaOnOutputType.Disabled)
            {
                point.Events
                    .CreatePlasmaOnEvent()
                    .SetAsProgramCall(operation.Menus.PlasmaCuttingSettings.PlasmaOnOutputType == PlasmaOnOutputType.AsProgramCall)
                    .SetPlasmaOnCommand(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.PlasmaOnCommand, operation))
                    .SetPlasmaOnArguments(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.PlasmaOnArguments, operation))
                    .AddAfter();
            }
        }

        /// <summary>
        ///   Adds the transfer events.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void AddTransferEvents(IOperation operation, IPoint point)
        {
            //// PLASMA CUTTING
            // After the transfer point.
            // Add a plasma transfer event.
            // Example:
            //     PlasmaTransfer
            if (operation.Menus.PlasmaCuttingSettings.PlasmaTransferOutputType != PlasmaTransferOutputType.Disabled)
            {
                point.Events
                    .CreatePlasmaTransferEvent()
                    .SetAsProgramCall(operation.Menus.PlasmaCuttingSettings.PlasmaTransferOutputType == PlasmaTransferOutputType.AsProgramCall)
                    .SetPlasmaTransferCommand(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.PlasmaTransferCommand, operation))
                    .SetPlasmaTransferArguments(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.PlasmaTransferArguments, operation))
                    .AddAfter();
            }
        }

        /// <summary>
        ///   Adds the pierce events.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void AddPierceEvents(IOperation operation, IPoint point)
        {
            //// PLASMA CUTTING
            // After the pierce point.
            // Add a pierce events.
            // Example:
            //     WaitTime
            if (operation.Menus.PlasmaCuttingSettings.PlasmaPierceOutputType != PlasmaPierceOutputType.Disabled)
            {
                point.Events
                    .CreatePierceEvent()
                    .SetAsProgramCall(operation.Menus.PlasmaCuttingSettings.PlasmaPierceOutputType == PlasmaPierceOutputType.AsProgramCall)
                    .SetPierceCommand(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.PierceCommand, operation))
                    .SetPierceArguments(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.PierceArguments, operation))
                    .AddAfter();
            }
        }

        /// <summary>
        ///   Adds the plasma off events.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void AddPlasmaOffEvents(IOperation operation, IPoint point)
        {
            // Add a plasma Off event.
            // Example:
            //     PlasmaOff
            if (operation.Menus.PlasmaCuttingSettings.PlasmaOffOutputType != PlasmaOffOutputType.Disabled)
            {
                point.Events
                    .CreatePlasmaOffEvent()
                    .SetAsProgramCall(operation.Menus.PlasmaCuttingSettings.PlasmaOffOutputType == PlasmaOffOutputType.AsProgramCall)
                    .SetPlasmaOffCommand(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.PlasmaOffCommand, operation))
                    .SetPlasmaOffArguments(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.PlasmaOffArguments, operation))
                    .AddAfter();
            }
        }

        /// <summary>
        ///   Adds the avc on events.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void AddAvcOnEvents(IOperation operation, IPoint point)
        {
            // Add AVC On Events
            // Example:
            //     PlasmaAvcOn
            if (operation.Menus.PlasmaCuttingSettings.AvcOnOutputType != AvcOnOutputType.Disabled)
            {
                point.Events
                    .CreateAvcOnEvent()
                    .SetAsProgramCall(operation.Menus.PlasmaCuttingSettings.AvcOnOutputType == AvcOnOutputType.AsProgramCall)
                    .SetAvcOnCommand(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.AvcOnCommand, operation))
                    .SetAvcOnArguments(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.AvcOnArguments, operation))
                    .AddAfter();
            }
        }

        /// <summary>
        ///   Adds the set process events.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void AddAvcOffEvents(IOperation operation, IPoint point)
        {
            // Add AVC Off Event
            // Example:
            //     PlasmaAvcOff
            if (operation.Menus.PlasmaCuttingSettings.AvcOffOutputType != AvcOffOutputType.Disabled)
            {
                point.Events
                    .CreateAvcOffEvent()
                    .SetAsProgramCall(operation.Menus.PlasmaCuttingSettings.AvcOffOutputType == AvcOffOutputType.AsProgramCall)
                    .SetAvcOffCommand(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.AvcOffCommand, operation))
                    .SetAvcOffArguments(this.ParsePlasmaMenuSetting(operation.Menus.PlasmaCuttingSettings.AvcOffArguments, operation))
                    .AddAfter();
            }
        }
    }
}
