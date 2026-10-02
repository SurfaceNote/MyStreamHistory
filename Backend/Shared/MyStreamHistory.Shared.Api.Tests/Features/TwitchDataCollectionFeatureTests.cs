using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MyStreamHistory.Shared.Api.Features;
using Xunit;

namespace MyStreamHistory.Shared.Api.Tests.Features;

public class TwitchDataCollectionFeatureTests
{
    [Theory]
    [InlineData("Development", null, false)]
    [InlineData("Production", null, true)]
    [InlineData("Staging", null, true)]
    [InlineData("Development", "true", true)]
    [InlineData("Production", "false", false)]
    public void Configuration_SelectsEnvironmentDefaultAndSupportsOverrides(string environment, string? value, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [TwitchDataCollectionFeature.ConfigurationKey] = value }).Build();
        var feature = TwitchDataCollectionFeature.FromConfiguration(configuration, new TestEnvironment(environment));
        Assert.Equal(expected, feature.Enabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActionFilter_BlocksDisabledCollectionAndAllowsEnabledCollection(bool enabled)
    {
        var context = new ActionExecutingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        new TwitchDataCollectionFilter(new TwitchDataCollectionFeature(enabled)).OnActionExecuting(context);

        if (enabled)
            Assert.Null(context.Result);
        else
            Assert.Equal(503, Assert.IsType<ObjectResult>(context.Result).StatusCode);
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
