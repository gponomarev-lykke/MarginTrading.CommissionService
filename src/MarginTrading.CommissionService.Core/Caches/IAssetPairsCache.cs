﻿using System.Collections.Generic;
using MarginTrading.CommissionService.Core.Domain.Abstractions;
using JetBrains.Annotations;

namespace MarginTrading.CommissionService.Core.Caches
{
    public interface IAssetPairsCache
    {
        IAssetPair GetAssetPairById(string assetPairId);
        /// <summary>
        /// Tries to get an asset pair, if it is not found null is returned.
        /// </summary>
        /// <param name="assetPairId"></param>
        /// <returns></returns>
        [CanBeNull] IAssetPair GetAssetPairByIdOrDefault(string assetPairId);
        IAssetPair FindAssetPair(string asset1, string asset2, string legalEntity);
        
        void InitPairsCache(Dictionary<string, IAssetPair> instruments);
    }
}