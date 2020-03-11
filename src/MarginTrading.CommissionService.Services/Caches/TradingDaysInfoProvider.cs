// Copyright (c) 2019 Lykke Corp.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Common.Log;
using MarginTrading.Backend.Contracts;
using MarginTrading.CommissionService.Core.Caches;
using MarginTrading.CommissionService.Core.Domain;
using MarginTrading.CommissionService.Core.Services;
using MarginTrading.SettingsService.Contracts;
using MongoDB.Bson;

namespace MarginTrading.CommissionService.Services.Caches
{
    public class TradingDaysInfoProvider : ITradingDaysInfoProvider
    {
        private readonly IScheduleSettingsApi _scheduleApi;
        private readonly ILog _log;

        private Dictionary<string, TradingDayInfo> _cache =
            new Dictionary<string, TradingDayInfo>();

        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public TradingDaysInfoProvider(IScheduleSettingsApi scheduleApi,
            ILog log)
        {
            _scheduleApi = scheduleApi;
            _log = log;
        }

        /// <summary>
        /// Calculates number of nights between the last and the next trading days
        /// </summary>
        /// <param name="marketId">ID of the market</param>
        /// <param name="currentDateTime">Current timestamp</param>
        /// <returns></returns>
        public int GetNumberOfNightsUntilNextTradingDay(string marketId, DateTime currentDateTime)
        {
            _lock.EnterReadLock();
            TradingDayInfo marketInfo;

            try
            {
                if (_cache.TryGetValue(marketId, out marketInfo) && marketInfo.LastTradingDay.Date == currentDateTime.Date)
                {
                    return CalculateNumberOfDays(marketInfo);
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _lock.EnterWriteLock();

            try
            {
                var infos = _scheduleApi.GetMarketsInfo(new[] {marketId}, marketInfo?.NextTradingDayStart.AddMilliseconds(1))
                    .GetAwaiter().GetResult();

                if (!infos.TryGetValue(marketId, out var info))
                {
                    _log.WriteWarningAsync(nameof(TradingDaysInfoProvider), nameof(GetNumberOfNightsUntilNextTradingDay),
                        $"Trading day for market {marketId} on date {currentDateTime} was not found. Returning default value.");

                    return 1;
                }

                var days = new TradingDayInfo
                {
                    LastTradingDay = info.LastTradingDay,
                    NextTradingDayStart = info.NextTradingDayStart
                };

                _cache[marketId] = days;

                return CalculateNumberOfDays(days);

            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Initialize(Dictionary<string, TradingDayInfo> infos)
        {
            _lock.EnterWriteLock();

            try
            {
                _cache = infos;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private int CalculateNumberOfDays(TradingDayInfo info)
        {
            _log.WriteInfoAsync(nameof(TradingDaysInfoProvider), nameof(CalculateNumberOfDays), 
                $"Calculating number of days using the following data: {info.ToJson()}");
            
            var days = info.NextTradingDayStart.Subtract(info.LastTradingDay).Days;

            if (days > 0)
            {
                return days;
            }
            
            _log.WriteWarningAsync(nameof(TradingDaysInfoProvider), nameof(CalculateNumberOfDays),
                $"Number of days between last day {info.LastTradingDay} and next day {info.NextTradingDayStart} is not correct. Returning default value.");

            return 1;
        }
    }
}