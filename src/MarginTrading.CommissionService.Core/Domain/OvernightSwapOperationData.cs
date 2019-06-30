using System;
using System.Collections.Generic;

namespace MarginTrading.CommissionService.Core.Domain
{
    public class OvernightSwapOperationData : OperationDataBase<CommissionOperationState>
    {
        /// <summary>
        /// Null is for sub-operations
        /// </summary>
        public int? NumberOfFinancingDays { get; set; }
        
        /// <summary>
        /// Null is for sub-operations
        /// </summary>
        public int? FinancingDaysPerYear { get; set; }
        
        /// <summary>
        /// Trading day
        /// </summary>
        public DateTime TradingDay { get; set; }
    }
}