﻿using System.Threading.Tasks;
using JetBrains.Annotations;
using Refit;

namespace Lykke.MarginTrading.CommissionService.Contracts
{
    /// <summary>
    /// Api for launching overnight swap process. FOR TESTING ONLY
    /// </summary>
    [PublicAPI]
    public interface IOvernightSwapApi
    {
        /// <summary>
        /// Starts overnight swap process
        /// </summary>
        [Post("/api/overnightswap/start")]
        Task StartOvernightSwapProcess([NotNull] string operationId, 
            int numberOfFinancingDays = 0, int financingDaysPerYear = 0);
    }
}