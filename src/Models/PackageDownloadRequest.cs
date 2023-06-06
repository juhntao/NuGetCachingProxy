using System.Diagnostics.CodeAnalysis;

using MongoDB.Entities;

namespace Nefarius.Web.Caching.Models;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public sealed class PackageDownloadRequest : FileEntity
{
    public string PackageId { get; set; } = null!;

    public string PackageVersion { get; set; } = null!;

    public string PackageFileName { get; set; } = null!;
}