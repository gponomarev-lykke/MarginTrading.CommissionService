// Copyright (c) 2019 Lykke Corp.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Configuration;

namespace MarginTrading.CommissionService.Infrastructure
{
    public static class ConfigurationRootExtensions
    {
        public static bool NotThrowExceptionsOnServiceValidation(this IConfigurationRoot configuration)
        {
            return !string.IsNullOrEmpty(configuration["NOT_THROW_EXCEPTIONS_ON_SERVICES_VALIDATION"]) &&
                   bool.TryParse(configuration["NOT_THROW_EXCEPTIONS_ON_SERVICES_VALIDATION"],
                       out var trowExceptionsOnInvalidService) && trowExceptionsOnInvalidService;
        }

        public static string InstanceId(this IConfigurationRoot configuration)
        {
            return configuration.GetValue<string>("InstanceId");
        }
    }
}