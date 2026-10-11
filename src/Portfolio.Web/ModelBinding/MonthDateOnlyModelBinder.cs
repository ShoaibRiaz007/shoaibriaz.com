using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Portfolio.ModelBinding;

/// <summary>
/// Binds <see cref="DateOnly"/> from either a full date ("2025-06-14") or the "yyyy-MM" value that
/// &lt;input type="month"&gt; submits, which the default binder rejects. Month values map to the 1st.
/// An empty value binds to null for <c>DateOnly?</c> ("Present" / not set).
/// </summary>
public sealed class MonthDateOnlyModelBinder : IModelBinder
{
    private static readonly string[] Formats = { "yyyy-MM", "yyyy-MM-dd" };

    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None) return Task.CompletedTask;

        context.ModelState.SetModelValue(context.ModelName, value);
        var raw = value.FirstValue?.Trim();

        if (string.IsNullOrEmpty(raw))
        {
            if (context.ModelMetadata.IsReferenceOrNullableType)
                context.Result = ModelBindingResult.Success(null);
            else
                context.ModelState.TryAddModelError(context.ModelName, "A date is required.");
            return Task.CompletedTask;
        }

        if (DateOnly.TryParseExact(raw, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            context.Result = ModelBindingResult.Success(date);
        else
            context.ModelState.TryAddModelError(context.ModelName, "Enter a valid month (yyyy-MM).");

        return Task.CompletedTask;
    }
}

/// <summary>Registers <see cref="MonthDateOnlyModelBinder"/> for DateOnly and DateOnly?.</summary>
public sealed class MonthDateOnlyModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return type == typeof(DateOnly) ? new MonthDateOnlyModelBinder() : null;
    }
}
