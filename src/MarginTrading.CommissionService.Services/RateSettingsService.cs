using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Log;
using Lykke.RabbitMqBroker.Publisher;
using Lykke.RabbitMqBroker.Subscriber;
using MarginTrading.CommissionService.Core;
using MarginTrading.CommissionService.Core.Domain;
using MarginTrading.CommissionService.Core.Domain.Rates;
using MarginTrading.CommissionService.Core.Repositories;
using MarginTrading.CommissionService.Core.Services;
using MarginTrading.CommissionService.Core.Settings.Rates;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace MarginTrading.CommissionService.Services
{
    public class RateSettingsService : IRateSettingsService
    {
        private readonly IMarginTradingBlobRepository _blobRepository;
        private readonly IDatabase _redisDatabase;
        private readonly IEventSender _eventSender;
        private readonly ILog _log;
        private readonly DefaultRateSettings _defaultRateSettings;

        public RateSettingsService(
            IMarginTradingBlobRepository blobRepository,
            IDatabase redisDatabase,
            IEventSender eventSender,
            ILog log,
            DefaultRateSettings defaultRateSettings)
        {
            _blobRepository = blobRepository;
            _redisDatabase = redisDatabase;
            _eventSender = eventSender;
            _log = log;
            _defaultRateSettings = defaultRateSettings;
        }

        #region Order Execution

        public async Task<OrderExecutionRate> GetOrderExecutionRate(string assetPairId)
        {
            //first we try to grab from Redis
            var serializedData = await _redisDatabase.KeyExistsAsync(GetKey(LykkeConstants.OrderExecutionKey))
                ? await _redisDatabase.HashGetAsync(GetKey(LykkeConstants.OrderExecutionKey), assetPairId)
                : (RedisValue)string.Empty;
            var cachedData = serializedData.HasValue ? Deserialize<OrderExecutionRate>(serializedData) : null;

            //now we try to refresh the cache from repository
            if (cachedData == null)
            {
                var repoData = await RefreshRedisFromRepo((List<OrderExecutionRate>)null);
                cachedData = repoData?.FirstOrDefault(x => x.AssetPairId == assetPairId);
                if (cachedData == null)
                {
                    await _log.WriteWarningAsync(nameof(RateSettingsService), nameof(GetOrderExecutionRate),
                        $"No order execution rate for {assetPairId}. Using the default one.");

                    var rateFromDefault =
                        OrderExecutionRate.FromDefault(_defaultRateSettings.DefaultOrderExecutionSettings, assetPairId);
                    
                    await _redisDatabase.HashSetAsync(GetKey(LykkeConstants.OrderExecutionKey), 
                         new [] {new HashEntry(assetPairId, Serialize(rateFromDefault))});
                    
                    return rateFromDefault;
                }
            }

            return cachedData;
        }
        
        public async Task<IReadOnlyList<OrderExecutionRate>> GetOrderExecutionRatesForApi()
        {
            var cachedData = await _redisDatabase.KeyExistsAsync(GetKey(LykkeConstants.OrderExecutionKey))
                ? (await _redisDatabase.HashGetAllAsync(GetKey(LykkeConstants.OrderExecutionKey)))
                    .Select(x => Deserialize<OrderExecutionRate>(x.Value)).ToList()
                : new List<OrderExecutionRate>();

            // Refresh the data from the repo if it is absent in Redis
            if (cachedData.Count == 0)
            {
                cachedData = await RefreshRedisFromRepo(cachedData);
            }

            return cachedData;
        }

        private async Task<List<OrderExecutionRate>> RefreshRedisFromRepo(List<OrderExecutionRate> cachedData = null)
        {
            var repoData = (await _blobRepository.ReadAsync<IEnumerable<OrderExecutionRate>>(
                blobContainer: LykkeConstants.RateSettingsBlobContainer,
                key: LykkeConstants.OrderExecutionKey
            ))?.ToList();

            if (repoData != null && repoData.Count != 0)
            {
                await _redisDatabase.HashSetAsync(GetKey(LykkeConstants.OrderExecutionKey), 
                    repoData.Select(x => new HashEntry(x.AssetPairId, Serialize(x))).ToArray());
                cachedData = repoData;
            }

            return cachedData;
        }

        public async Task ReplaceOrderExecutionRates(List<OrderExecutionRate> rates)
        {
            rates = rates.Select(x =>
            {
                if (string.IsNullOrWhiteSpace(x.LegalEntity))
                {
                    x.LegalEntity = _defaultRateSettings.DefaultOrderExecutionSettings.LegalEntity;
                }
                return x;
            }).ToList();
            
            await _blobRepository.MergeListAsync(
                blobContainer: LykkeConstants.RateSettingsBlobContainer,
                key: LykkeConstants.OrderExecutionKey,
                objects: rates,
                selector: x => x.AssetPairId);
            
            await _redisDatabase.HashSetAsync(GetKey(LykkeConstants.OrderExecutionKey), 
                rates.Select(x => new HashEntry(x.AssetPairId, Serialize(x))).ToArray());

            await _eventSender.SendRateSettingsChanged(CommissionType.OrderExecution);
        }
        
        #endregion Order Execution

        #region Overnight Swaps

        public async Task<OvernightSwapRate> GetOvernightSwapRate(string assetPairId)
        {
            //first we try to grab from Redis
            var serializedData = await _redisDatabase.KeyExistsAsync(GetKey(LykkeConstants.OvernightSwapKey))
                ? await _redisDatabase.HashGetAsync(GetKey(LykkeConstants.OvernightSwapKey), assetPairId)
                : (RedisValue) string.Empty;
            var cachedData = serializedData.HasValue ? Deserialize<OvernightSwapRate>(serializedData) : null;

            //now we try to refresh the cache from repository
            if (cachedData == null)
            {
                var repoData = await RefreshRedisFromRepo((List<OvernightSwapRate>)null);
                cachedData = repoData?.FirstOrDefault(x => x.AssetPairId == assetPairId);
                if (cachedData == null)
                {
                    await _log.WriteWarningAsync(nameof(RateSettingsService), nameof(GetOvernightSwapRate),
                        $"No overnight swap rate for {assetPairId}. Using the default one.");
                    
                    var rateFromDefault =
                        OvernightSwapRate.FromDefault(_defaultRateSettings.DefaultOvernightSwapSettings, assetPairId);
                    
                    await _redisDatabase.HashSetAsync(GetKey(LykkeConstants.OvernightSwapKey), 
                        new [] {new HashEntry(assetPairId, Serialize(rateFromDefault))});
                    
                    return rateFromDefault;
                }
            }

            return cachedData;
        }
        
        public async Task<IReadOnlyList<OvernightSwapRate>> GetOvernightSwapRatesForApi()
        {
            var cachedData = await _redisDatabase.KeyExistsAsync(GetKey(LykkeConstants.OvernightSwapKey))
                ? (await _redisDatabase.HashGetAllAsync(GetKey(LykkeConstants.OvernightSwapKey)))
                    .Select(x => Deserialize<OvernightSwapRate>(x.Value)).ToList()
                : new List<OvernightSwapRate>();

            // Refresh the data from the repo if it is absent in Redis
            if (cachedData.Count == 0)
            {
                cachedData = await RefreshRedisFromRepo(cachedData);
            }

            return cachedData;
        }

        private async Task<List<OvernightSwapRate>> RefreshRedisFromRepo(List<OvernightSwapRate> cachedData = null)
        {
            var repoData = (await _blobRepository.ReadAsync<IEnumerable<OvernightSwapRate>>(
                blobContainer: LykkeConstants.RateSettingsBlobContainer,
                key: LykkeConstants.OvernightSwapKey
            ))?.ToList();

            if (repoData != null && repoData.Count != 0)
            {
                await _redisDatabase.HashSetAsync(GetKey(LykkeConstants.OvernightSwapKey), 
                    repoData.Select(x => new HashEntry(x.AssetPairId, Serialize(x))).ToArray());
                cachedData = repoData;
            }

            return cachedData;
        }

        public async Task ReplaceOvernightSwapRates(List<OvernightSwapRate> rates)
        {
            await _blobRepository.MergeListAsync(
                blobContainer: LykkeConstants.RateSettingsBlobContainer,
                key: LykkeConstants.OvernightSwapKey,
                objects: rates,
                selector: x => x.AssetPairId);
            
            await _redisDatabase.HashSetAsync(GetKey(LykkeConstants.OvernightSwapKey), 
                rates.Select(x => new HashEntry(x.AssetPairId, Serialize(x))).ToArray());

            await _eventSender.SendRateSettingsChanged(CommissionType.OvernightSwap);
        }
        
        #endregion Overnight Swaps
        
        #region On Behalf

        public async Task<OnBehalfRate> GetOnBehalfRate()
        {
            //first we try to grab from Redis
            var serializedData = await _redisDatabase.KeyExistsAsync(GetKey(LykkeConstants.OnBehalfKey))
                ? await _redisDatabase.StringGetAsync(GetKey(LykkeConstants.OnBehalfKey))
                : (RedisValue) string.Empty;
            var cachedData = serializedData.HasValue ? Deserialize<OnBehalfRate>(serializedData) : null;

            //now we try to refresh the cache from repository
            if (cachedData == null)
            {
                cachedData = await RefreshRedisFromRepo((OnBehalfRate)null);
                if (cachedData == null)
                {
                    await _log.WriteWarningAsync(nameof(RateSettingsService), nameof(GetOnBehalfRate),
                        $"No OnBehalf rate saved, using the default one.");
                    
                    cachedData = OnBehalfRate.FromDefault(_defaultRateSettings.DefaultOnBehalfSettings);
                    
                    await _redisDatabase.StringSetAsync(GetKey(LykkeConstants.OnBehalfKey), Serialize(cachedData));
                }
            }

            return cachedData;
        }

        private async Task<OnBehalfRate> RefreshRedisFromRepo(OnBehalfRate cachedData = null)
        {
            var repoData = await _blobRepository.ReadAsync<OnBehalfRate>(
                blobContainer: LykkeConstants.RateSettingsBlobContainer,
                key: LykkeConstants.OnBehalfKey
            );

            if (repoData != null)
            {
                await _redisDatabase.StringSetAsync(GetKey(LykkeConstants.OnBehalfKey), Serialize(repoData));
                cachedData = repoData;
            }

            return cachedData;
        }

        public async Task ReplaceOnBehalfRate(OnBehalfRate rate)
        {
            if (string.IsNullOrWhiteSpace(rate.LegalEntity))
            {
                rate.LegalEntity = _defaultRateSettings.DefaultOrderExecutionSettings.LegalEntity;
            }

            await _blobRepository.WriteAsync(
                blobContainer: LykkeConstants.RateSettingsBlobContainer,
                key: LykkeConstants.OnBehalfKey,
                obj: rate);
            
            await _redisDatabase.StringSetAsync(GetKey(LykkeConstants.OnBehalfKey), Serialize(rate));

            await _eventSender.SendRateSettingsChanged(CommissionType.OnBehalf);
        }
        
        #endregion On Behalf

        private string Serialize<TMessage>(TMessage obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        private TMessage Deserialize<TMessage>(string data)
        {
            return JsonConvert.DeserializeObject<TMessage>(data);
        }

        private string GetKey(string key)
        {
            return $"CommissionService:RateSettings:{key}";
        }
    }
}