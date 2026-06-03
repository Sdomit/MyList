using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MyList.Models;

namespace MyList.Services;

public sealed class IconService
{
    private readonly ConcurrentDictionary<string, ImageSource> _cache = new();
    private readonly ClipboardAssetService _clipboardAssetService;
    private readonly object _folderIconLock = new();
    private ImageSource? _folderIcon;
    private ImageSource? _packagedFolderIcon;
    private ImageSource? _genericImageIcon;
    private ImageSource? _mtabIcon;
    private ImageSource? _clipboardIcon;
    private ImageSource? _commandIcon;
    private ImageSource? _powershellIcon;
    private static readonly Guid ImageListInterfaceGuid = new("46EB5926-582E-4017-9FDF-E8998DAA0950");

    public IconService(ClipboardAssetService clipboardAssetService)
    {
        _clipboardAssetService = clipboardAssetService;
    }

    public ImageSource? GetIcon(ItemModel item)
    {
        var key = BuildCacheKey(item);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var icon = LoadIcon(item);
        if (icon is not null)
        {
            _cache[key] = icon;
        }

        return icon;
    }

    public Task<ImageSource?> GetIconAsync(ItemModel item, CancellationToken cancellationToken = default)
    {
        var key = BuildCacheKey(item);
        if (_cache.TryGetValue(key, out var cached))
        {
            return Task.FromResult<ImageSource?>(cached);
        }

        return Task.Run(() =>
        {
            if (_cache.TryGetValue(key, out var asyncCached))
            {
                return asyncCached;
            }

            var icon = LoadIcon(item);
            if (icon is not null)
            {
                _cache[key] = icon;
            }

            return icon;
        }, cancellationToken);
    }

    public void QueueIconRefresh(ItemModel item)
    {
        _ = QueueIconRefreshAsync(item);
    }

    public static string BuildCacheKey(ItemModel item)
    {
        return $"{item.Path}|{item.IconPath}|{item.UseSystemIcon}|{item.Type}|{item.IsClipboardText}|{item.IsClipboardImage}|{item.ClipboardImageAssetPath}|{item.IsMtab}|{item.IsActionItem}|{item.ActionKind}";
    }

    private async Task QueueIconRefreshAsync(ItemModel item)
    {
        var key = BuildCacheKey(item);
        var icon = await GetIconAsync(item).ConfigureAwait(false);
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        await dispatcher.InvokeAsync(() =>
        {
            if (BuildCacheKey(item) == key)
            {
                item.Icon = icon;
            }
        }, DispatcherPriority.Background);
    }

    private ImageSource? LoadIcon(ItemModel item)
    {
        try
        {
            if (item.IsMtab)
            {
                return GetPackagedIcon(ref _mtabIcon, "mtab.ico") ?? GetFolderIcon();
            }

            if (item.IsActionItem)
            {
                return item.ActionKind == ActionKind.PowerShell
                    ? GetPackagedIcon(ref _powershellIcon, "powershell.ico") ?? GetGenericImageIcon()
                    : GetPackagedIcon(ref _commandIcon, "cmd.ico") ?? GetGenericImageIcon();
            }

            if (item.IsClipboardImage)
            {
                return _clipboardAssetService.LoadClipboardImagePreview(item)
                    ?? GetPackagedIcon(ref _clipboardIcon, "clipboard.ico")
                    ?? GetGenericImageIcon();
            }

            if (item.IsClipboardText)
            {
                return GetPackagedIcon(ref _clipboardIcon, "clipboard.ico") ?? GetGenericImageIcon();
            }

            if (item.Type == ItemType.Folder)
            {
                return GetShellIcon(item.Path, ItemType.Folder) ?? GetFolderIcon();
            }

            if (!string.IsNullOrWhiteSpace(item.IconPath) && System.IO.File.Exists(item.IconPath))
            {
                using var overrideIcon = Icon.ExtractAssociatedIcon(item.IconPath!);
                return ConvertIcon(overrideIcon);
            }

            if (!item.UseSystemIcon)
            {
                return null;
            }

            return GetShellIcon(item.Path, item.Type)
                ?? GetShellIcon(System.IO.Path.GetExtension(item.Path), ItemType.File, useFileAttributesFallback: true)
                ?? GetKnownFileTypeFallback(item.Path);
        }
        catch
        {
            return null;
        }
    }

    private ImageSource? GetFolderIcon()
    {
        lock (_folderIconLock)
        {
            if (_folderIcon is not null)
            {
                return _folderIcon;
            }

            var shellFolderIcon = GetShellIcon("folder", ItemType.Folder, useFileAttributesFallback: true);
            if (shellFolderIcon is not null)
            {
                _folderIcon = shellFolderIcon;
                return _folderIcon;
            }

            var packaged = GetPackagedIcon(ref _packagedFolderIcon, "folder.ico");
            if (packaged is not null)
            {
                return packaged;
            }

            return null;
        }
    }

    private ImageSource? GetKnownFileTypeFallback(string? path)
    {
        var extension = System.IO.Path.GetExtension(path ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        return extension.ToLowerInvariant() switch
        {
            ".bat" or ".cmd" => GetPackagedIcon(ref _commandIcon, "cmd.ico") ?? GetGenericImageIcon(),
            ".ps1" => GetPackagedIcon(ref _powershellIcon, "powershell.ico") ?? GetGenericImageIcon(),
            _ => null
        };
    }

    private ImageSource? GetGenericImageIcon()
    {
        lock (_folderIconLock)
        {
            if (_genericImageIcon is not null)
            {
                return _genericImageIcon;
            }

            var imageIcon = GetShellIcon(".png", ItemType.File, useFileAttributesFallback: true);
            if (imageIcon is null)
            {
                return null;
            }

            _genericImageIcon = imageIcon;
            return _genericImageIcon;
        }
    }

    private static ImageSource? GetShellIcon(string? path, ItemType type, bool useFileAttributesFallback = false)
    {
        var targetPath = path ?? string.Empty;
        var flags = SHGFI_ICON | SHGFI_LARGEICON | SHGFI_SYSICONINDEX;
        uint attributes;

        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            var folderExists = type == ItemType.Folder && System.IO.Directory.Exists(targetPath);
            var fileExists = type != ItemType.Folder && System.IO.File.Exists(targetPath);

            if (!folderExists && !fileExists)
            {
                useFileAttributesFallback = true;
            }
        }
        else
        {
            useFileAttributesFallback = true;
        }

        if (useFileAttributesFallback)
        {
            flags |= SHGFI_USEFILEATTRIBUTES;
            if (type == ItemType.Folder)
            {
                attributes = FILE_ATTRIBUTE_DIRECTORY;
                targetPath = string.IsNullOrWhiteSpace(targetPath) ? "folder" : targetPath;
            }
            else
            {
                attributes = FILE_ATTRIBUTE_NORMAL;
                var extension = System.IO.Path.GetExtension(targetPath);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    targetPath = extension;
                }
                else
                {
                    targetPath = ".txt";
                }
            }
        }
        else
        {
            attributes = 0;
        }

        var info = new SHFILEINFO();
        SHGetFileInfo(targetPath, attributes, ref info, (uint)Marshal.SizeOf(info), flags);

        var imageListIcon = GetSystemImageListIcon(info.iIcon);
        if (imageListIcon is not null)
        {
            if (info.hIcon != IntPtr.Zero)
            {
                DestroyIcon(info.hIcon);
            }

            return imageListIcon;
        }

        if (info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static ImageSource? GetPackagedIcon(ref ImageSource? cache, string fileName)
    {
        if (cache is not null)
        {
            return cache;
        }

        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "icons", fileName);
        if (!System.IO.File.Exists(iconPath))
        {
            return null;
        }

        try
        {
            var decoder = new IconBitmapDecoder(
                new Uri(iconPath, UriKind.Absolute),
                BitmapCreateOptions.IgnoreImageCache,
                BitmapCacheOption.OnLoad);

            var selectedFrame = decoder.Frames
                .OrderBy(frame => Math.Abs(GetFrameSize(frame) - PackagedIconTargetSize))
                .ThenByDescending(GetFrameSize)
                .FirstOrDefault();

            if (selectedFrame is null)
            {
                return null;
            }

            selectedFrame.Freeze();
            cache = selectedFrame;
            return cache;
        }
        catch
        {
            return null;
        }
    }

    private static int GetFrameSize(BitmapFrame frame)
    {
        return Math.Max(frame.PixelWidth, frame.PixelHeight);
    }

    private static ImageSource? GetSystemImageListIcon(int iconIndex)
    {
        if (iconIndex < 0)
        {
            return null;
        }

        if (TryGetSystemImageListIcon(SHIL_EXTRALARGE, iconIndex, out var extraLargeIcon))
        {
            return extraLargeIcon;
        }

        if (TryGetSystemImageListIcon(SHIL_LARGE, iconIndex, out var largeIcon))
        {
            return largeIcon;
        }

        return null;
    }

    private static bool TryGetSystemImageListIcon(int imageListKind, int iconIndex, out ImageSource? imageSource)
    {
        imageSource = null;

        var imageList = GetImageList(imageListKind);
        if (imageList is null)
        {
            return false;
        }

        if (imageList.GetIcon(iconIndex, ILD_TRANSPARENT, out var hIcon) != 0 || hIcon == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            imageSource = source;
            return true;
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static IImageList? GetImageList(int imageListKind)
    {
        var imageListGuid = ImageListInterfaceGuid;
        return SHGetImageList(imageListKind, ref imageListGuid, out var imageList) == 0
            ? imageList
            : null;
    }

    private static ImageSource? ConvertIcon(Icon? icon)
    {
        if (icon is null)
        {
            return null;
        }

        var source = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        source.Freeze();
        return source;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_SYSICONINDEX = 0x000004000;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const int SHIL_LARGE = 0x0;
    private const int SHIL_EXTRALARGE = 0x2;
    private const int ILD_TRANSPARENT = 0x00000001;
    private const int PackagedIconTargetSize = 48;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IMAGEINFO
    {
        public IntPtr hbmImage;
        public IntPtr hbmMask;
        public int Unused1;
        public int Unused2;
        public System.Drawing.Rectangle rcImage;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IMAGELISTDRAWPARAMS
    {
        public int cbSize;
        public IntPtr himl;
        public int i;
        public IntPtr hdcDst;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public int xBitmap;
        public int yBitmap;
        public int rgbBk;
        public int rgbFg;
        public int fStyle;
        public int dwRop;
        public int fState;
        public int Frame;
        public int crEffect;
    }

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, out int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, out int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, out int pi);
        [PreserveSig] int Draw(ref IMAGELISTDRAWPARAMS pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, out IntPtr picon);
        [PreserveSig] int GetImageInfo(int i, out IMAGEINFO pImageInfo);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(
        int iImageList,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IImageList ppv);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
