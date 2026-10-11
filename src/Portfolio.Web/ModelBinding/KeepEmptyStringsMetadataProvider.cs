using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Portfolio.ModelBinding;

/// <summary>
/// By default MVC converts empty form values to null. Our entity string columns are NOT NULL,
/// so this keeps blank optional fields as empty strings instead of null.
/// </summary>
public sealed class KeepEmptyStringsMetadataProvider : IDisplayMetadataProvider
{
    public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
        => context.DisplayMetadata.ConvertEmptyStringToNull = false;
}
