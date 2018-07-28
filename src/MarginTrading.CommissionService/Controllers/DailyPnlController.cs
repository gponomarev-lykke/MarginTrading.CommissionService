﻿using System;
using System.Threading.Tasks;
using Lykke.Cqrs;
using Lykke.MarginTrading.CommissionService.Contracts;
using Lykke.MarginTrading.CommissionService.Contracts.Commands;
using MarginTrading.CommissionService.Core.Extensions;
using MarginTrading.CommissionService.Core.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Internal;

namespace MarginTrading.CommissionService.Controllers
{
    
    [Route("api/daily-pnl")]
    public class DailyPnlController : Controller, IDailyPnlApi
    {
        private readonly ICqrsEngine _cqrsEngine;
        private readonly CqrsContextNamesSettings _cqrsContextNamesSettings;
        private readonly ISystemClock _systemClock;

        public DailyPnlController(ICqrsEngine cqrsEngine,
            CqrsContextNamesSettings cqrsContextNamesSettings,
            ISystemClock systemClock)
        {
            _cqrsEngine = cqrsEngine;
            _cqrsContextNamesSettings = cqrsContextNamesSettings;
            _systemClock = systemClock;
        }

        [Route("start")]
        [HttpPost]
        public Task StartDailyPnlProcess(string operationId, DateTime tradingDay)
        {
            _cqrsEngine.SendCommand(
                new StartDailyPnlProcessCommand(
                    operationId.RequiredNotNullOrWhiteSpace(nameof(operationId)),
                    _systemClock.UtcNow.UtcDateTime,
                    tradingDay), 
                _cqrsContextNamesSettings.CommissionService,
                _cqrsContextNamesSettings.CommissionService);
            
            return Task.CompletedTask;
        }
    }
}