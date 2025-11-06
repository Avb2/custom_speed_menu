// <copyright file="PlasmaCuttingMainProcessor.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc.PlasmaCuttingProcess
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Robotmaster.Processor.Core.Common.Enums;
    using Robotmaster.Processor.Core.Common.Enums.Applications.Points;
    using Robotmaster.Processor.Fanuc.Generated.Artifacts.Menus.Enums.TouchSensingSettings;
    using Robotmaster.Processor.Fanuc.Generated.Interfaces;

    /// <summary>
    ///     The Plasma Cutting main processor.
    /// </summary>
    internal partial class PlasmaCuttingMainProcessor : MainProcessor
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PlasmaCuttingMainProcessor"/> class.
        ///     This class inherits from <see cref="MainProcessor"/> therefore all <see cref="MainProcessor"/>'s
        ///     fields, properties, and methods are accessible and override-able here.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="context">
        ///     The current context.
        /// </param>
        internal PlasmaCuttingMainProcessor(IOperation operation, IMainProcessorContext context)
            : base(operation, context)
        {
            //// BASE - Default constructor - LEAVE EMPTY
        }

        /// <summary>
        ///   Gets the Plasma Setting Pattern.
        ///   This pattern is used to match the plasma cutting settings in the format {keyword1["optionalKeyword2"]:optionalFormat}.
        /// </summary>
        public string PlasmaSettingPattern { get; private set; } = @"\{(\w+)(?:\[""(\w+)""\])?(?::([a-zA-Z0-9\#\.]+))?\}";

        /// <inheritdoc/>
        internal override void EditOperation(IOperation operation)
        {
            //// BASE - Default processing
            base.EditOperation(operation); // DO NOT REMOVE

            //// PLASMA
            //// Default plasma cutting implementation (operation level)
            if (operation.ApplicationType == ApplicationType.PlasmaCutting)
            {
                this.EditPlasmaCuttingOperation(operation);
            }

            //// CUSTOMIZATION - Uncomment the example below
            //// Example: Add events to the first point
            // operation.FirstPoint.Events.CreateCommandEvent().SetCommandText("Some command").AddBefore();
            // operation.FirstPoint.Events.CreateCommentEvent().SetCommentText("Some comment").AddBefore();
        }

        /// <summary>
        ///     Edits an plasma cutting operation.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void EditPlasmaCuttingOperation(IOperation operation)
        {
            //// PLASMA CUTTING

            this.ResetTouchRegister(operation);

            this.AddPlasmaOperationComments(operation);
            this.EditPlasmaOperationTouchSection(operation);
        }

        /// <inheritdoc/>
        internal override void EditPoint(IOperation operation, IPoint point)
        {
            //// BASE - Default processing
            base.EditPoint(operation, point); // DO NOT REMOVE

            //// PLASMA CUTTING
            if (point.ApplicationData.PlasmaCutting != null)
            {
                if (operation.ApplicationData.PlasmaCutting.TouchGroups.Any() &&
                    !point.ApplicationData.PlasmaCutting.IsTouchSectionPoint)
                {
                    switch (operation.Menus.TouchSensingSettings.TouchOffsetOutputType)
                    {
                        case TouchOffsetOutputType.Sequential:
                            this.EditPointForSequentialTouchOffset(operation, point);
                            break;
                        case TouchOffsetOutputType.Interpolated:
                            this.EditPointForInterpolatedTouchOffset(operation, point);
                            break;
                    }
                }

                if (point == operation.FirstPoint)
                {
                    this.EditPlasmaOperationFirstPoint(operation);
                }

                switch (point.ApplicationData.PlasmaCutting.PointType)
                {
                    case PlasmaCuttingPointType.IHSOrigin:
                        this.EditIhsOriginPoint(operation, point);
                        break;

                    case PlasmaCuttingPointType.IHSTarget:
                        this.EditIhsTargetPoint(operation, point);
                        break;

                    case PlasmaCuttingPointType.Transfer:
                        this.EditTransferPoint(operation, point);
                        break;

                    case PlasmaCuttingPointType.Pierce:
                        this.EditPiercePoint(operation, point);
                        break;

                    case PlasmaCuttingPointType.InCut:
                        this.EditInCutPoint(operation, point);
                        break;
                }

                // Automated AVC
                this.EditAvcPoint(operation, point);

                if (point == operation.LastPoint)
                {
                    this.EditPlasmaOperationLastPoint(operation, point);
                }
            }

            //// CUSTOMIZATION - Uncomment the example below
            //// Example: Add event to sharp corners
            // if (point.Flags.IsInProcess &&
            //     point != operation.LastPoint &&
            //     point.PathDirection.AngleBetween(point.NextPoint.PathDirection) > 45)
            // {
            //    point.Events.CreateCommentEvent().SetCommentText("Sharp corner detected").AddBefore();
            // }
        }

        /// <summary>
        ///     Edits the firs point of the plasma cutting operation.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        internal virtual void EditPlasmaOperationFirstPoint(IOperation operation)
        {
            this.AddSetProcessEvents(operation, operation.FirstPoint);
        }

        /// <summary>
        ///     Edits the Transfer point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void EditTransferPoint(IOperation operation, IPoint point)
        {
            // Add plasma On event to every transfer point.
            this.AddPlasmaOnEvents(operation, point);

            // Add plasma Transfer event to every transfer point.
            this.AddTransferEvents(operation, point);
        }

        /// <summary>
        ///     Edits the Pierce point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void EditPiercePoint(IOperation operation, IPoint point)
        {
            // Add pierce delay events to every pierce point.
            this.AddPierceEvents(operation, point);
        }

        /// <summary>
        ///     Edits a cut point.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void EditInCutPoint(IOperation operation, IPoint point)
        {
            //// PLASMA CUTTING
            // Turn off the plasma after the last InCut point.
            // Add a plasma Off event.
            // Example:
            //     PlasmaOff
            if (point.NextPoint == null ||
                point.NextPoint.ApplicationData.PlasmaCutting.PointType != PlasmaCuttingPointType.InCut)
            {
                this.AddPlasmaOffEvents(operation, point);
            }
        }

        /// <summary>
        ///     Edits point for AVC commands.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void EditAvcPoint(IOperation operation, IPoint point)
        {
            //// PLASMA CUTTING
            if (!point.ApplicationData.PlasmaCutting.IsAvcEnabled)
            {
                // If the current point AVC flag is Off and next point AVC is On
                if (point.NextPoint?.ApplicationData.PlasmaCutting.IsAvcEnabled == true)
                {
                    // Add AVC On Event
                    this.AddAvcOnEvents(operation, point);
                }
            }
            else
            {
                // If the current point AVC flag is On and next point AVC is Off
                if (point.NextPoint?.ApplicationData.PlasmaCutting.IsAvcEnabled == false)
                {
                    // Add AVC Off Event
                    this.AddAvcOffEvents(operation, point);
                }
            }
        }

        /// <summary>
        ///     Edits the last plasma point of the operation.
        /// </summary>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <param name="point">
        ///     The current point.
        /// </param>
        internal virtual void EditPlasmaOperationLastPoint(IOperation operation, IPoint point)
        {
        }

        /// <inheritdoc/>
        internal override bool IsMainProcessorInputValid()
        {
            bool isMainProcessorInputValid = base.IsMainProcessorInputValid();

            if (this.Operation == this.RobotProgram.Operations
                    .FirstOrDefault(op => op.OperationType == OperationType.TaskOperation && op.ApplicationType != ApplicationType.PlasmaCutting))
            {
                this.Context.NotifyUser(
                    "Processor Warning: The plasma cutting process is only compatible with operations from the plasma cutting module." + Environment.NewLine +
                    "Consider using the cutting process instead.",
                    true);
            }

            return isMainProcessorInputValid;
        }

        /// <summary>
        ///     Parse a Plasma Menu Setting to replace all matches of the format {keyword1["optionalKeyword2"]:optionalFormat}
        ///     by the corresponding plasma cutting parameter formatted value.
        /// </summary>
        /// <param name="rawSetting">
        ///     The raw setting string.
        /// </param>
        /// <param name="operation">
        ///     The current operation.
        /// </param>
        /// <returns>
        ///     The updated string with matches replaced.
        /// </returns>
        internal virtual string ParsePlasmaMenuSetting(string rawSetting, IOperation operation)
        {
            // Find matches in the raw setting string of the format {keyword1["optionalKeyword2"]:optionalFormat}
            // Example: {CutChartData["CHARACTER87"]}
            // keyword1 = CutChartData
            // optionalKeyword2 = CHARACTER87
            // optionalFormat = 0.000
            var matches = Regex.Matches(rawSetting, this.PlasmaSettingPattern);

            var updatedSetting = rawSetting;

            // Replace each match by the Plasma Cutting value
            foreach (Match match in matches)
            {
                var keyword1 = match.Groups[1].Value;
                var optionalKeyword2 = match.Groups[2].Value;
                var optionalFormat = match.Groups[3].Value;

                string replacement;

                switch (keyword1)
                {
                    // If the first keyword is "CutChartData" then try to get the cut chart data using the optional keyword as a dictionary key
                    // Example: {CutChartData["CHARACTER87"]}
                    case "CutChartData":
                        {
                            var cutChartData = operation.ApplicationData.PlasmaCutting.CutChartData;
                            if (cutChartData.ContainsKey(optionalKeyword2))
                            {
                                // Get string with optional format if specified
                                replacement = optionalFormat.Length > 0
                                    ? string.Format(CultureInfo.InvariantCulture, "{0:" + optionalFormat + "}", cutChartData[optionalKeyword2])
                                    : cutChartData[optionalKeyword2].ToString();

                                // Replace the match by the replacement
                                updatedSetting = updatedSetting.Replace(match.Value, replacement);
                            }

                            break;
                        }

                    // Otherwise get the string plasma cutting standard setting property using the first keyword as a property name
                    // Example: ProcessId
                    case "PersistentRecordId":
                    case "Revision":
                    case "ProcessId":
                        {
                            // Get the property value of the underlying base type
                            var property = operation.ApplicationData.PlasmaCutting.StandardSettings.GetType().GetProperty(keyword1);

                            // Get the string value;
                            replacement = (string)property.GetValue(operation.ApplicationData.PlasmaCutting.StandardSettings);

                            // Replace the match by the replacement
                            updatedSetting = updatedSetting.Replace(match.Value, replacement);
                            break;
                        }

                    // Otherwise get the double plasma cutting standard setting property using the first keyword as a property name
                    // Example: ProcessId
                    case "Current":
                    case "ArcVoltage":
                    case "PierceDelay":
                    case "CuttingSpeed":
                    case "KerfWidth":
                    case "PierceHeight":
                    case "TransferHeight":
                    case "CutHeight":
                        {
                            var standardDoubleParameter =
                                (double)operation.ApplicationData.PlasmaCutting.StandardSettings.GetType().GetProperty(keyword1)
                                .GetValue(operation.ApplicationData.PlasmaCutting.StandardSettings);

                            replacement = optionalFormat.Length > 0
                                ? string.Format(CultureInfo.InvariantCulture, "{0:" + optionalFormat + "}", standardDoubleParameter)
                                : standardDoubleParameter.ToString(CultureInfo.InvariantCulture);

                            // Replace the match by the replacement
                            updatedSetting = updatedSetting.Replace(match.Value, replacement);
                            break;
                        }
                }
            }

            return updatedSetting;
        }
    }
}
