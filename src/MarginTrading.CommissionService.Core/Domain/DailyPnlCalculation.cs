﻿using System;
using JetBrains.Annotations;
using MarginTrading.CommissionService.Core.Domain.Abstractions;
using MarginTrading.CommissionService.Core.Extensions;

namespace MarginTrading.CommissionService.Core.Domain
{
    public class DailyPnlCalculation : IDailyPnlCalculation
    {
        private const string Separator = "_";
        
        public string Id => GetId(OperationId, PositionId);
        
        public string OperationId { get; }
        public string AccountId { get; }
        public string Instrument { get; }
        public DateTime Time { get; }
        public DateTime TradingDay { get; }
        public decimal Volume { get; }
        public decimal FxRate { get; }
        public string PositionId { get; }
        public decimal Pnl { get; }
        public bool? WasCharged { get; }

        public DailyPnlCalculation([NotNull] string operationId, [NotNull] string accountId,
            [NotNull] string instrument, DateTime time, DateTime tradingDay, decimal volume, decimal fxRate,
            [NotNull] string positionId, decimal pnl, bool? wasCharged)
        {
            OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
            AccountId = accountId ?? throw new ArgumentNullException(nameof(operationId));
            Instrument = instrument ?? throw new ArgumentNullException(nameof(operationId));
            Time = time;
            TradingDay = tradingDay;
            Volume = volume;
            FxRate = fxRate;
            PositionId = positionId ?? throw new ArgumentNullException(nameof(operationId));
            Pnl = pnl;
            WasCharged = wasCharged;
        }

        public static string GetId(string operationId, string positionId) => $"{operationId}{Separator}{positionId}";

        public static (string OperationId, string PositionId) ExtractKeysFromId(string operationPositionId)
        {
            var separatorIndex = operationPositionId.LastIndexOf(Separator, StringComparison.InvariantCulture)
                .RequiredGreaterThan(-1, nameof(operationPositionId));
            return (operationPositionId.Substring(0, separatorIndex), 
                operationPositionId.Substring(separatorIndex + 1));
        }

        public static string ExtractOperationId(string id)
        {
            var separatorIndex = id.LastIndexOf(Separator, StringComparison.InvariantCulture);

            return separatorIndex == -1 ? id : id.Substring(0, separatorIndex);
        }
    }
}