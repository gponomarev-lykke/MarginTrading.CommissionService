﻿using System;
using JetBrains.Annotations;

namespace MarginTrading.CommissionService.Core.Domain.Rates
{
    public class OrderExecutionRate
    {
        [NotNull] public string AssetPairId { get; set; }
        
        public decimal CommissionCap { get; set; }
        
        public decimal CommissionFloor { get; set; }
        
        public decimal CommissionRate { get; set; }
        
        [NotNull] public string CommissionAsset { get; set; }
    }
}