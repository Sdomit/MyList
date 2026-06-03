using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyList.Models;

namespace MyList.Services;

public sealed class ClipboardAssetService
{
    public const string AssetDirectoryName = "clipboard-assets";

    private readonly LogService _log = LogService.Instance;

    public string AppDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MyList");

    public string AssetDirectory => Path.Combine(AppDataDirectory, AssetDirectoryName);

    public ClipboardImageAssetInfo? SaveClipboardImage(BitmapSource bitmapSource)
    {
        if (bitmapSource is null)
        {
            return null;
        }

        Directory.CreateDirectory(AssetDirectory);

        var assetFileName = $"{Guid.NewGuid():N}.png";
        var assetPath = Path.Combine(AssetDirectory, assetFileName);

        try
        {
            using (var stream = File.Create(assetPath))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(stream);
            }
        }
        catch (Exception ex)
        {
            _log.Log(ex, $"Failed to save clipboard image: {assetPath}");
            try
            {
                if (File.Exists(assetPath))
                {
                    File.Delete(assetPath);
                }
            }
            catch
            {
                // best-effort cleanup of the partial file
            }

            return null;
        }

        return new ClipboardImageAssetInfo(
            assetFileName,
            bitmapSource.PixelWidth,
            bitmapSource.PixelHeight);
    }

    public ImageSource? LoadClipboardImagePreview(ItemModel item)
    {
        var assetPath = GetAbsoluteAssetPath(item?.ClipboardImageAssetPath);
        if (assetPath is null || !File.Exists(assetPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(assetPath);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            _log.Log(ex, $"Failed to load clipboard image preview: {assetPath}");
            return null;
        }
    }

    public bool TryCopyImageToClipboard(ItemModel item)
    {
        var bitmap = LoadClipboardBitmap(item);
        if (bitmap is null)
        {
            return false;
        }

        try
        {
            System.Windows.Clipboard.SetImage(bitmap);
            return true;
        }
        catch (Exception ex)
        {
            _log.Log(ex, "Failed to copy image to clipboard.");
            return false;
        }
    }

    public void ExportReferencedAssets(IEnumerable<ItemModel> items, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var assetName in GetReferencedAssetNames(items))
        {
            var sourcePath = GetAbsoluteAssetPath(assetName);
            if (sourcePath is null || !File.Exists(sourcePath))
            {
                continue;
            }

            var destinationPath = Path.Combine(destinationDirectory, assetName);
            try
            {
                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
            catch (Exception ex)
            {
                _log.Log(ex, $"Failed to export clipboard asset '{assetName}'.");
            }
        }
    }

    public void ImportReferencedAssets(IEnumerable<ItemModel> items, string sourceDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        Directory.CreateDirectory(AssetDirectory);

        foreach (var assetName in GetReferencedAssetNames(items))
        {
            var sourcePath = Path.Combine(sourceDirectory, assetName);
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            var destinationPath = Path.Combine(AssetDirectory, assetName);
            try
            {
                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
            catch (Exception ex)
            {
                _log.Log(ex, $"Failed to import clipboard asset '{assetName}'.");
            }
        }
    }

    public void CleanupUnusedAssets(IEnumerable<ItemModel> items)
    {
        Directory.CreateDirectory(AssetDirectory);
        var referencedAssets = GetReferencedAssetNames(items).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in Directory.GetFiles(AssetDirectory))
        {
            var fileName = Path.GetFileName(filePath);
            if (referencedAssets.Contains(fileName))
            {
                continue;
            }

            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                _log.Log(ex, $"Failed to delete unused clipboard asset '{fileName}'.");
            }
        }
    }

    public string NormalizeAssetReference(string? assetReference)
    {
        return string.IsNullOrWhiteSpace(assetReference)
            ? string.Empty
            : Path.GetFileName(assetReference.Trim());
    }

    private BitmapSource? LoadClipboardBitmap(ItemModel item)
    {
        var assetPath = GetAbsoluteAssetPath(item?.ClipboardImageAssetPath);
        if (assetPath is null || !File.Exists(assetPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(assetPath);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            frame.Freeze();
            return frame;
        }
        catch (Exception ex)
        {
            _log.Log(ex, $"Failed to load clipboard bitmap: {assetPath}");
            return null;
        }
    }

    private string? GetAbsoluteAssetPath(string? assetReference)
    {
        var normalized = NormalizeAssetReference(assetReference);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return Path.Combine(AssetDirectory, normalized);
    }

    private IEnumerable<string> GetReferencedAssetNames(IEnumerable<ItemModel> items)
    {
        return items
            .Where(item => item.IsClipboardImage)
            .Select(item => NormalizeAssetReference(item.ClipboardImageAssetPath))
            .Where(assetName => !string.IsNullOrWhiteSpace(assetName))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
