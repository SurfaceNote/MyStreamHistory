namespace MyStreamHistory.API.Services
{
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using System;
    using System.Linq;

    public class RouteService
    {
        private readonly IActionDescriptorCollectionProvider _actionDescriptorProvider;

        public RouteService(IActionDescriptorCollectionProvider actionDescriptorProvider)
        {
            _actionDescriptorProvider = actionDescriptorProvider;
        }

        /// <summary>
        /// Gets path for controller and action
        /// </summary>
        /// <param name="controllerName">Name of controller (without suffix Controller).</param>
        /// <param name="actionName"></param>
        public string GetRoutePath(string controllerName, string actionName)
        {
            var actionDescriptors = _actionDescriptorProvider.ActionDescriptors.Items
                .Where(ad => ad.RouteValues.ContainsKey("controller") && ad.RouteValues["controller"] == controllerName
                          && ad.RouteValues.ContainsKey("action") && ad.RouteValues["action"] == actionName)
                .ToList();

            if (!actionDescriptors.Any())
            {
                throw new InvalidOperationException($"Route for {controllerName}.{actionName} not found");
            }

            var routeTemplate = actionDescriptors.First().AttributeRouteInfo?.Template;
            if (string.IsNullOrEmpty(routeTemplate))
            {
                throw new InvalidOperationException($"No route template found for {controllerName}.{actionName}.");
            }

            return $"/{routeTemplate.Trim('/')}".ToLower();
        }

        /// <summary>
        /// Checks if the request path matches the specified controller and action.
        /// </summary>
        /// <param name="requestPath"></param>
        /// <param name="controllerName">Name of controller (without suffix Controller).</param>
        /// <param name="actionName"></param>
        public bool IsRouteMatch(string requestPath, string controllerName, string actionName)
        {
            if (string.IsNullOrEmpty(requestPath))
            {
                return false;
            }

            var routePath = GetRoutePath(controllerName, actionName);
            return requestPath.Trim('/').ToLower() == routePath.Trim('/').ToLower() ||
                   requestPath.StartsWith(routePath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
