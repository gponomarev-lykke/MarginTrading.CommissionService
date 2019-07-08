﻿// Copyright (c) 2019 Lykke Corp.
// See the LICENSE file in the project root for more information.

using System;

namespace MarginTrading.CommissionService.Core.Domain
{
    public class MatchedOrder
    {
        public string OrderId { get; set; }
        public string MarketMakerId { get; set; }
        public decimal LimitOrderLeftToMatch { get; set; }
        public decimal Volume { get; set; }
        public decimal Price { get; set; }
        public string ClientId { get; set; }
        public DateTime MatchedDate { get; set; }

        public static MatchedOrder Create(MatchedOrder src)
        {
            return new MatchedOrder
            {
                OrderId = src.OrderId,
                MarketMakerId = src.MarketMakerId,
                LimitOrderLeftToMatch = src.LimitOrderLeftToMatch,
                Volume = src.Volume,
                Price = src.Price,
                ClientId = src.ClientId,
                MatchedDate = src.MatchedDate
            };
        }
    }
}