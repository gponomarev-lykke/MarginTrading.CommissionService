﻿namespace MarginTrading.CommissionService.Core.Settings
{
    public class RabbitMqSettings
    {
        public RabbitMqPublishersSettings Publishers { get; set; }
        public RabbitMqConsumersSettings Consumers { get; set; }
    }
}