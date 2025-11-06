// <copyright file="CustomValueKeys.cs" company="Hypertherm Inc.">
// Copyright (c) Hypertherm Inc. All rights reserved.
// </copyright>

namespace Robotmaster.Processor.Fanuc
{
    /// <summary>
    ///     Struct containing strings used as keys in Custom Values the processor directly references.
    /// </summary>
    internal readonly struct CustomValueKeys
    {
        /// <summary>
        ///     Robot-domain robot number key.
        /// </summary>
        public const string RobotNumber = "RobotNumber";

        /// <summary>
        ///     Robot-domain MASH menu group number key.
        /// </summary>
        public const string MashGrpNumber = "MashGrpNumber";
    }
}
