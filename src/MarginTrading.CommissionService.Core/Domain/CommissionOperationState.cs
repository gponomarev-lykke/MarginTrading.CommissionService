namespace MarginTrading.CommissionService.Core.Domain
{
    public enum CommissionOperationState
    {
        Initiated = 0,
        Calculated = 1,
        Succeeded = 2,
        Failed = 3,
    }
}