﻿// Copyright (c) 2019 Lykke Corp.
// See the LICENSE file in the project root for more information.

using System;

namespace MarginTrading.CommissionService.Core.Services
{
    public interface IDateService
    {
        DateTime Now();
    }
}