// Copyright (c) 2019 Lykke Corp.
// See the LICENSE file in the project root for more information.

namespace MarginTrading.CommissionService.Core.Domain
{
    public class OnBehalfOperationData: OperationDataBase<CommissionOperationState>
    {
        public string AccountId { get; set; }
        
        public string OrderId { get; set; }
    }
}