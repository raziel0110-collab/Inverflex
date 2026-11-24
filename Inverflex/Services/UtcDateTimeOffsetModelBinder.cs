using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Inverflex.Services;

public sealed class UtcDateTimeOffsetModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var modelType = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return modelType == typeof(DateTimeOffset) ? new UtcDateTimeOffsetModelBinder() : null;
    }
}

public sealed class UtcDateTimeOffsetModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueResult == ValueProviderResult.None) return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);
        var value = valueResult.FirstValue;
        if (string.IsNullOrWhiteSpace(value))
        {
            if (Nullable.GetUnderlyingType(bindingContext.ModelType) is not null)
                bindingContext.Result = ModelBindingResult.Success(null);
            else
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "La fecha es obligatoria.");
            return Task.CompletedTask;
        }

        if (DateTimeOffset.TryParseExact(value, UtcDateTimeOffsetJsonConverter.Format, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
            bindingContext.Result = ModelBindingResult.Success(date);
        else
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName,
                $"La fecha debe tener el formato {UtcDateTimeOffsetJsonConverter.Format} en UTC.");

        return Task.CompletedTask;
    }
}
