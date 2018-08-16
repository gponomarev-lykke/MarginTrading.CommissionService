﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.Common;
using Lykke.Common.Chaos;
using Lykke.Cqrs;
using Lykke.MarginTrading.CommissionService.Contracts.Commands;
using Lykke.MarginTrading.CommissionService.Contracts.Events;
using MarginTrading.CommissionService.Core.Domain;
using MarginTrading.CommissionService.Core.Domain.Abstractions;
using MarginTrading.CommissionService.Core.Repositories;
using MarginTrading.CommissionService.Core.Services;
using MarginTrading.CommissionService.Core.Workflow.DailyPnl.Events;
using MarginTrading.CommissionService.Workflow.ChargeCommission;
using Microsoft.Extensions.Internal;

namespace MarginTrading.CommissionService.Workflow.OvernightSwap
{
    public class DailyPnlCommandsHandler
    {
        public const string OperationName = "DailyPnlCommission";
        private readonly IDailyPnlService _dailyPnlService;
        private readonly IOperationExecutionInfoRepository _executionInfoRepository;
        private readonly ISystemClock _systemClock;
        private readonly IChaosKitty _chaosKitty;
        private readonly ILog _log;
        private readonly IThreadSwitcher _threadSwitcher;
        private readonly IDailyPnlListener _dailyPnlListener;

        public DailyPnlCommandsHandler(
            IDailyPnlService dailyPnlService,
            IOperationExecutionInfoRepository executionInfoRepository,
            ISystemClock systemClock,
            IChaosKitty chaosKitty,
            ILog log,
            IThreadSwitcher threadSwitcher,
            IDailyPnlListener dailyPnlListener)
        {
            _dailyPnlService = dailyPnlService;
            _executionInfoRepository = executionInfoRepository;
            _systemClock = systemClock;
            _chaosKitty = chaosKitty;
            _log = log;
            _threadSwitcher = threadSwitcher;
            _dailyPnlListener = dailyPnlListener;
        }

        /// <summary>
        /// Calculate PnL
        /// </summary>
        [UsedImplicitly]
        private async Task<CommandHandlingResult> Handle(StartDailyPnlProcessCommand command,
            IEventPublisher publisher)
        {
            //ensure idempotency of the whole operation
            var executionInfo = await _executionInfoRepository.GetOrAddAsync(
                operationName: OperationName, 
                operationId: command.OperationId,
                factory: () => new OperationExecutionInfo<DailyPnlOperationData>(
                    operationName: OperationName,
                    id: command.OperationId,
                    lastModified: _systemClock.UtcNow.UtcDateTime,
                    data: new DailyPnlOperationData
                    {
                        TradingDay = command.CreationTimestamp,
                        State = CommissionOperationState.Initiated,
                    }
                ));

            if (ChargeCommissionSaga.SwitchState(executionInfo?.Data, CommissionOperationState.Initiated,
                CommissionOperationState.Started))
            {
                IReadOnlyList<IDailyPnlCalculation> calculatedPnLs = null;
                try
                {
                    calculatedPnLs = await _dailyPnlService.Calculate(command.OperationId, command.CreationTimestamp);
                }
                catch (Exception exception)
                {
                    publisher.PublishEvent(new DailyPnlsStartFailedEvent(
                        operationId: command.OperationId,
                        creationTimestamp: _systemClock.UtcNow.UtcDateTime,
                        failReason: exception.Message
                    ));
                    await _log.WriteErrorAsync(nameof(DailyPnlCommandsHandler), nameof(Handle), exception, _systemClock.UtcNow.UtcDateTime);
                    return CommandHandlingResult.Ok();//no retries
                }

                publisher.PublishEvent(new DailyPnlsCalculatedEvent(
                    operationId: command.OperationId,
                    creationTimestamp: _systemClock.UtcNow.UtcDateTime,
                    total: calculatedPnLs.Count,
                    failed: 0 //todo not implemented: check
                ));

                _threadSwitcher.SwitchThread(() => _dailyPnlListener.TrackCharging(
                    operationId: command.OperationId, 
                    operationIds: calculatedPnLs.Select(x => x.Id), 
                    publisher: publisher
                ));

                foreach (var pnl in calculatedPnLs)
                {
                    //prepare state for sub operations
                    await _executionInfoRepository.GetOrAddAsync(
                        operationName: OperationName, 
                        operationId: pnl.GetId(),
                        factory: () => new OperationExecutionInfo<DailyPnlOperationData>(
                            operationName: OperationName,
                            id: pnl.GetId(),
                            lastModified: _systemClock.UtcNow.UtcDateTime,
                            data: new DailyPnlOperationData
                            {
                                TradingDay = command.CreationTimestamp,
                                State = CommissionOperationState.Started,
                            }
                        ));
                    
                    publisher.PublishEvent(new DailyPnlCalculatedInternalEvent(
                        operationId: pnl.GetId(),
                        creationTimestamp: _systemClock.UtcNow.DateTime,
                        accountId: pnl.AccountId,
                        positionId: pnl.PositionId,
                        assetPairId: pnl.Instrument,
                        pnl: pnl.Pnl,
                        tradingDay: pnl.TradingDay,
                        volume: pnl.Volume,
                        fxRate: pnl.FxRate
                    ));

                    _chaosKitty.Meow(nameof(OvernightSwapCommandsHandler));
                }
                
                await _executionInfoRepository.Save(executionInfo);
            }
            
            return CommandHandlingResult.Ok();
        }

    }
}