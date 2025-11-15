using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;

namespace MyStreamHistory.Shared.Infrastructure.Configuration
{
    public static class ConfigurationExtenstions
    {
        public static T GetValidated<T>(this IConfigurationSection section) where T : class, new()
        {
            var options = section.Get<T>() ?? new T();

            Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);

            return options;
        }
    }
}
