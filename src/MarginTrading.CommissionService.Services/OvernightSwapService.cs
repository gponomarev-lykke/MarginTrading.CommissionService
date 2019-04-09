﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Common;
using Lykke.Cqrs;
using Lykke.SettingsReader;
using MarginTrading.CommissionService.Core;
using MarginTrading.CommissionService.Core.Caches;
using MarginTrading.CommissionService.Core.Domain;
using MarginTrading.CommissionService.Core.Domain.Abstractions;
using MarginTrading.CommissionService.Core.Extensions;
using MarginTrading.CommissionService.Core.Repositories;
using MarginTrading.CommissionService.Core.Services;
using MarginTrading.CommissionService.Core.Settings;
using MarginTrading.SettingsService.Contracts;
using MarginTrading.SettingsService.Contracts.AssetPair;
using Microsoft.Extensions.Internal;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace MarginTrading.CommissionService.Services
{
	/// <inheritdoc />
	/// <summary>
	/// Take care of overnight swap calculation and charging.
	/// </summary>
	public class OvernightSwapService : IOvernightSwapService
	{
		private const string DistributedLockKey = "CommissionService:OvernightSwapProcess";
		
		private readonly IAssetPairsApi _assetPairsApi;
		private readonly ICommissionCalcService _commissionCalcService;
		private readonly IOvernightSwapHistoryRepository _overnightSwapHistoryRepository;
		private readonly IInterestRatesRepository _interestRatesRepository;
		private readonly IPositionReceiveService _positionReceiveService;
		private readonly ISystemClock _systemClock;
		private readonly IConvertService _convertService;
		private readonly ILog _log;
		private readonly IDatabase _database;
		private readonly CommissionServiceSettings _commissionServiceSettings;

		private Dictionary<string, decimal> _currentInterestRates;

		public OvernightSwapService(
			IAssetPairsApi assetPairsApi,
			ICommissionCalcService commissionCalcService,
			IOvernightSwapHistoryRepository overnightSwapHistoryRepository,
			IInterestRatesRepository interestRatesRepository,
			IPositionReceiveService positionReceiveService,
			ISystemClock systemClock,
			IConvertService convertService,
			ILog log,
			IDatabase database,
			CommissionServiceSettings commissionServiceSettings)
		{
			_assetPairsApi = assetPairsApi;
			_commissionCalcService = commissionCalcService;
			_overnightSwapHistoryRepository = overnightSwapHistoryRepository;
			_interestRatesRepository = interestRatesRepository;
			_positionReceiveService = positionReceiveService;
			_systemClock = systemClock;
			_convertService = convertService;
			_log = log;
			_database = database;
			_commissionServiceSettings = commissionServiceSettings;
		}

		/// <summary>
		/// Filter orders that are already calculated
		/// </summary>
		/// <returns></returns>
		private async Task<IReadOnlyList<OpenPosition>> GetOrdersForCalculationAsync(DateTime tradingDay)
		{
			var openPositions = (await _positionReceiveService.GetActive()).ToList();
			
			//prepare the list of orders. Explicit end of the day is ok for DateTime From by requirements.
			var allLast = await _overnightSwapHistoryRepository.GetAsync(tradingDay, null);

			if (allLast.Any())
			{
				var lastMaxCalcTime = allLast.Max(x => x.TradingDay);
				
				if (lastMaxCalcTime.Date > tradingDay)
				{
					throw new Exception($"Calculation started for {tradingDay:d}, but there already was calculation for a newer date {lastMaxCalcTime:d}");
				}
			}
			
			var calculatedIds = allLast.Where(x => x.IsSuccess).Select(x => x.PositionId).ToHashSet();
			//select only non-calculated positions, changed before current invocation time
			var filteredOrders = openPositions.Where(x => !calculatedIds.Contains(x.Id) 
			                                              && x.OpenTimestamp.Date <= tradingDay.Date);

			//detect orders for which last calculation failed and it was closed
			var failedClosedOrders = allLast.Where(x => !x.IsSuccess)
				.Select(x => x.PositionId)
				.Except(openPositions.Select(y => y.Id)).ToList();
			if (failedClosedOrders.Any())
			{
				await _log.WriteErrorAsync(nameof(OvernightSwapService), nameof(GetOrdersForCalculationAsync), new Exception(
						$"Overnight swap calculation failed for some positions and they were closed before recalculation: {string.Join(", ", failedClosedOrders)}."),
					DateTime.UtcNow);
			}
			
			return filteredOrders.ToList();
		}

		public async Task<IReadOnlyList<IOvernightSwapCalculation>> Calculate(string operationId,
			DateTime creationTimestamp, int numberOfFinancingDays, int financingDaysPerYear, DateTime tradingDay)
		{
			if (!await _database.LockTakeAsync(DistributedLockKey, Environment.MachineName,
				_commissionServiceSettings.DistributedLockTimeout))
			{
				throw new Exception("Overnight swap calculation process is already in progress.");
			}

			var resultingCalculations = new List<IOvernightSwapCalculation>();
			try
			{
				var filteredPositions = await GetOrdersForCalculationAsync(tradingDay);

				await _log.WriteInfoAsync(nameof(OvernightSwapService), nameof(Calculate),
					$"Started, # of positions: {filteredPositions.Count}.", DateTime.UtcNow);

				var assetPairs = (await _assetPairsApi.List())
					.Select(x => _convertService.Convert<AssetPairContract, AssetPair>(x)).ToList();
				_currentInterestRates = (await _interestRatesRepository.GetAllLatest())
					.ToDictionary(x => x.AssetPairId, x => x.Rate);
				
				foreach (var position in filteredPositions)
				{
					try
					{
						var assetPair = assetPairs.First(x => x.Id == position.AssetPairId);
						var calculation = await ProcessPosition(position, assetPair, operationId, 
							numberOfFinancingDays, financingDaysPerYear, tradingDay);
						if (calculation != null)
						{
							resultingCalculations.Add(calculation);
						}
					}
					catch (Exception ex)
					{
						resultingCalculations.Add(await ProcessPosition(position, null, operationId, 
							numberOfFinancingDays, financingDaysPerYear, tradingDay, ex));
						await _log.WriteErrorAsync(nameof(OvernightSwapService), nameof(Calculate),
							$"Error calculating swaps for position: {position?.ToJson()}. Operation : {operationId}", ex);
					}
				}

				await _overnightSwapHistoryRepository.BulkInsertAsync(resultingCalculations);
				
				await _log.WriteInfoAsync(nameof(OvernightSwapService), nameof(Calculate),
					$"Finished, # of successful calculations: {resultingCalculations.Count(x => x.IsSuccess)}, # of failed: {resultingCalculations.Count(x => !x.IsSuccess)}.", DateTime.UtcNow);
			}
			finally
			{
				await _database.LockReleaseAsync(DistributedLockKey, Environment.MachineName);
			}

			return resultingCalculations;
		}

		/// <summary>
		/// Calculate overnight swap
		/// </summary>
		private async Task<IOvernightSwapCalculation> ProcessPosition(IOpenPosition position, IAssetPair assetPair,
			string operationId, int numberOfFinancingDays, int financingDaysPerYear, DateTime tradingDay,
			Exception exception = null)
		{
			if (exception != null)
			{
				return new OvernightSwapCalculation(
					operationId: operationId,
					accountId: position.AccountId,
					instrument: position.AssetPairId,
					direction: position.Direction,
					time: _systemClock.UtcNow.DateTime,
					volume: position.CurrentVolume,
					swapValue: default,
					positionId: position.Id,
					details: null,
					tradingDay: tradingDay,
					isSuccess: false,
					exception: exception);
			}
			
			var (swap, details) = await _commissionCalcService.GetOvernightSwap(_currentInterestRates, position,
				assetPair, numberOfFinancingDays, financingDaysPerYear);

			return new OvernightSwapCalculation(
				operationId: operationId,
				accountId: position.AccountId,
				instrument: position.AssetPairId,
				direction: position.Direction,
				time: _systemClock.UtcNow.DateTime,
				volume: position.CurrentVolume,
				swapValue: swap,
				positionId: position.Id,
				details: details,
				tradingDay: tradingDay,
				isSuccess: true);
		}

		public async Task<int> SetWasCharged(string positionOperationId, bool type)
		{
			return await _overnightSwapHistoryRepository.SetWasCharged(positionOperationId, type);
		}

		public async Task<(int Total, int Failed, int NotProcessed)> GetOperationState(string id)
		{
			//may be position charge id or operation id
			var operationId = OvernightSwapCalculation.ExtractOperationId(id);

			return await _overnightSwapHistoryRepository.GetOperationState(operationId);
		}
	}
}