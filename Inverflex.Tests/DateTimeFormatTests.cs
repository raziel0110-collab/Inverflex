using System.Globalization;
using System.Text.Json;
using Inverflex.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Inverflex.Tests;

public sealed class DateTimeFormatTests
{
    [Fact]
    public void JsonConverterReadsAndWritesTheRequestedUtcFormat()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UtcDateTimeOffsetJsonConverter());
        var local = new DateTimeOffset(2025, 5, 4, 10, 6, 7, TimeSpan.FromHours(-5));

        var json = JsonSerializer.Serialize(local, options);
        var parsed = JsonSerializer.Deserialize<DateTimeOffset>(json, options);

        Assert.Equal("\"04-05-2025 15:06:07\"", json);
        Assert.Equal(TimeSpan.Zero, parsed.Offset);
        Assert.Equal(local.UtcDateTime, parsed.UtcDateTime);
    }

    [Fact]
    public async Task ModelBinderParsesQueryTimestampAsUtc()
    {
        var bindingContext = CreateBindingContext(typeof(DateTimeOffset?), "04-05-2025 15:06:07");

        await new UtcDateTimeOffsetModelBinder().BindModelAsync(bindingContext);

        Assert.True(bindingContext.Result.IsModelSet);
        var parsed = Assert.IsType<DateTimeOffset>(bindingContext.Result.Model);
        Assert.Equal(TimeSpan.Zero, parsed.Offset);
        Assert.Equal(new DateTime(2025, 5, 4, 15, 6, 7, DateTimeKind.Utc), parsed.UtcDateTime);
    }

    [Fact]
    public async Task ModelBinderRejectsOtherQueryFormats()
    {
        var bindingContext = CreateBindingContext(typeof(DateTimeOffset?), "2025-05-04T15:06:07Z");

        await new UtcDateTimeOffsetModelBinder().BindModelAsync(bindingContext);

        Assert.False(bindingContext.Result.IsModelSet);
        Assert.False(bindingContext.ModelState.IsValid);
    }

    private static ModelBindingContext CreateBindingContext(Type modelType, string value)
    {
        var metadataProvider = new EmptyModelMetadataProvider();
        var metadata = metadataProvider.GetMetadataForType(modelType);
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new(), new ModelStateDictionary());
        var values = new QueryCollection(new Dictionary<string, StringValues> { ["date"] = new(value) });
        var valueProvider = new QueryStringValueProvider(BindingSource.Query, values, CultureInfo.InvariantCulture);
        return DefaultModelBindingContext.CreateBindingContext(actionContext, valueProvider, metadata, null, "date");
    }
}
