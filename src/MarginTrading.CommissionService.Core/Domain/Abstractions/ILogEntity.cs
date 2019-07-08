﻿// Copyright (c) 2019 Lykke Corp.
// See the LICENSE file in the project root for more information.

using System;

namespace MarginTrading.CommissionService.Core.Domain.Abstractions
{
    public interface ILogEntity
    {
        DateTime DateTime { get; set; }
        string Level { get; set; }
        string Env { get; set; }
        string AppName { get; set; }
        string Version { get; set; }
        string Component { get; set; }
        string Process { get; set; }
        string Context { get; set; }
        string Type { get; set; }
        string Stack { get; set; }
        string Msg { get; set; }
    }
}