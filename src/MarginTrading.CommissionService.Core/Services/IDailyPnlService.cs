﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarginTrading.CommissionService.Core.Domain.Abstractions;

namespace MarginTrading.CommissionService.Core.Services
{
    /// <summary>
    /// Take care of daily pnl calculation.
    /// </summary>
    public interface IDailyPnlService
    {
        /// <summary>
        /// Entry point for daily pnl calculation. Successfully calculated pnl are returned.
        /// </summary>
        /// <param name="operationId"></param>
        /// <param name="tradingDay"></param>
        /// <returns></returns>
        Task<IReadOnlyList<IDailyPnlCalculation>> Calculate(string operationId, DateTime tradingDay);
    }
}