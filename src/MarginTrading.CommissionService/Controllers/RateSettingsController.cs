﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lykke.MarginTrading.CommissionService.Contracts;
using Lykke.MarginTrading.CommissionService.Contracts.Models;
using MarginTrading.CommissionService.Core;
using MarginTrading.CommissionService.Core.Domain;
using MarginTrading.CommissionService.Core.Domain.Rates;
using MarginTrading.CommissionService.Core.Repositories;
using MarginTrading.CommissionService.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace MarginTrading.CommissionService.Controllers
{
    [Route("api/rates")]
    public class RateSettingsController : Controller, IRateSettingsApi
    {
        private readonly IRateSettingsService _rateSettingsService;
        private readonly IConvertService _convertService;

        public RateSettingsController(
            IRateSettingsService rateSettingsService,
            IConvertService convertService)
        {
            _rateSettingsService = rateSettingsService;
            _convertService = convertService;
        }

        [ProducesResponseType(typeof(IReadOnlyList<OrderExecutionRateContract>), 200)]
        [ProducesResponseType(400)]
        [HttpGet("get-order-exec")]
        public async Task<IReadOnlyList<OrderExecutionRateContract>> GetOrderExecutionRates()
        {
            return (await _rateSettingsService.GetOrderExecutionRatesForApi())
                ?.Select(x => _convertService.Convert<OrderExecutionRate, OrderExecutionRateContract>(x)).ToList()
                   ?? new List<OrderExecutionRateContract>();
        }

        /// <summary>
        /// Replace order execution rates
        /// </summary>
        /// <param name="rates"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [HttpPost("replace-order-exec")]
        public async Task ReplaceOrderExecutionRates([FromBody] OrderExecutionRateContract[] rates)
        {
            if (rates == null || !rates.Any() || rates.Any(x => 
                    string.IsNullOrWhiteSpace(x.AssetPairId)
                    || string.IsNullOrWhiteSpace(x.CommissionAsset)))
            {
                throw new ArgumentNullException(nameof(rates));
            }

            await _rateSettingsService.ReplaceOrderExecutionRates(rates
                .Select(x => _convertService.Convert<OrderExecutionRateContract, OrderExecutionRate>(x))
                .ToList());
        }

        
        
        [ProducesResponseType(typeof(IReadOnlyList<OvernightSwapRateContract>), 200)]
        [ProducesResponseType(400)]
        [HttpGet("get-overnight-swap")]
        public async Task<IReadOnlyList<OvernightSwapRateContract>> GetOvernightSwapRates()
        {
            return (await _rateSettingsService.GetOvernightSwapRatesForApi())
                   ?.Select(x => _convertService.Convert<OvernightSwapRate, OvernightSwapRateContract>(x)).ToList()
                   ?? new List<OvernightSwapRateContract>();
        }

        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [HttpPost("replace-overnight-swap")]
        public async Task ReplaceOvernightSwapRates([FromBody] OvernightSwapRateContract[] rates)
        {
            if (rates == null || !rates.Any() || rates.Any(x => 
                    string.IsNullOrWhiteSpace(x.AssetPairId)))
            {
                throw new ArgumentNullException(nameof(rates));
            }

            await _rateSettingsService.ReplaceOvernightSwapRates(rates
                .Select(x => _convertService.Convert<OvernightSwapRateContract, OvernightSwapRate>(x))
                .ToList());
        }

        
        
        [ProducesResponseType(typeof(OnBehalfRateContract), 200)]
        [ProducesResponseType(400)]
        [HttpGet("get-on-behalf")]
        public async Task<OnBehalfRateContract> GetOnBehalfRate()
        {
            var item = await _rateSettingsService.GetOnBehalfRateApi();
            return item == null ? null : _convertService.Convert<OnBehalfRate, OnBehalfRateContract>(item);
        }

        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [HttpPost("replace-on-behalf")]
        public async Task ReplaceOnBehalfRate([FromBody] OnBehalfRateContract rate)
        {
            if (string.IsNullOrWhiteSpace(rate?.CommissionAsset))
            {
                throw new ArgumentNullException(nameof(rate.CommissionAsset));
            }

            await _rateSettingsService.ReplaceOnBehalfRate(
                _convertService.Convert<OnBehalfRateContract, OnBehalfRate>(rate));
        }
    }
}