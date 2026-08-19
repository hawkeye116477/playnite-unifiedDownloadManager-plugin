using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#if Vanara || PlayniteDeps
using Vanara.PInvoke;
#endif

namespace Playnite;

public partial class Paths
{
    private const string longPathPrefix = @"\\?\";
    private const string longPathUncPrefix = @"\\?\UNC\";
    public const int MaxPathLength = 32_767;
    public static readonly char[] DirectorySeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    [GeneratedRegex(@"^([a-zA-Z]:\\|\\\\)")]
    private static partial Regex IsFullPathRegex();

    public static string FixSeparators(string path)
    {
        if (path.IsNullOrWhiteSpace())
            return path;

        var sb = new StringBuilder(path.Length);
        foreach (var t in path)
        {
            var chr = t;
            if (chr == Path.AltDirectorySeparatorChar)
                chr = Path.DirectorySeparatorChar;

            if (chr == Path.DirectorySeparatorChar && sb.Length > 0 && sb[^1] == Path.DirectorySeparatorChar)
                continue;

            sb.Append(chr);
        }

        // For UNC and DOS device path support
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
            sb.Insert(0, @"\");

        return sb.ToString();
    }

    public static bool AreEqual(string? path1, string? path2)
    {
        if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2))
            return false;

        try
        {
            path1 = Path.GetFullPath(path1).TrimEnd(DirectorySeparators);
            path2 = Path.GetFullPath(path2).TrimEnd(DirectorySeparators);
            return path1.Equals(path2, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static string GetSafeFileName(string filename)
    {
        if (filename.IsNullOrWhiteSpace())
            return filename;

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(filename.Length);
        foreach (var chr in filename)
        {
            if (char.IsWhiteSpace(chr) && sb.Length > 0 && char.IsWhiteSpace(sb[^1]))
                continue;

            if (!invalid.Contains(chr))
                sb.Append(chr);
        }

        return sb.ToString().Trim();
    }

    public static bool IsFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // Don't use Path.IsPathRooted because it fails on paths starting with one backslash.
        return IsFullPathRegex().IsMatch(path);
    }

    public static string GetCommonDirectory(string[] paths)
    {
        var stop = paths.Min(a => a.Length);
        if (stop == 0)
            return string.Empty;

        foreach (var path in paths)
        {
            for (var j = 0; j < stop; j++)
            {
                if (path[j] != paths[0][j])
                {
                    stop = j;
                    goto cont;
                }
            }
        }

        cont:
        var common = paths[0][..stop];
        if (common.Length == 0)
            return string.Empty;

        if (common[^1] == Path.DirectorySeparatorChar)
            return common;

        return common.Substring(0, common.LastIndexOf(Path.DirectorySeparatorChar) + 1);
    }

    // There are some cases where this is still needed. For example, various Win32 API functions
    // just don't work with implicit long paths.
    [Obsolete("Generally don't use since modern .NET is long path compatible.")]
    public static string FormatAsLongPath(string path, bool forcePrefix = false)
    {
        if (path.IsNullOrWhiteSpace())
            return path;

        // Relative paths don't support long paths
        // https://docs.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation?tabs=cmd
        if (!Paths.IsFullPath(path))
            return path;

        // While the MAX_PATH value is 260 characters, a lower value is used because
        // methods can append "\" and string terminator characters to paths and
        // make them surpass the limit
        if ((path.Length >= 258 || forcePrefix) && !path.StartsWith(longPathPrefix, StringComparison.Ordinal))
        {
            if (path.StartsWith(@"\\", StringComparison.Ordinal))
                return string.Concat(longPathUncPrefix, path.AsSpan(2));

            return longPathPrefix + path;
        }

        return path;
    }

    public static string GetPathWithoutFileExtension(string path)
    {
        return Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, Path.GetFileNameWithoutExtension(path));
    }

#if Vanara || PlayniteDeps
    public static string GetFinalPathName(string path)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return path;
        }

        using var file = Kernel32.CreateFile(
            path,
            0,
            FileShare.ReadWrite | FileShare.Delete,
            null,
            FileMode.Open,
            FileFlagsAndAttributes.FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);

        var sb = new StringBuilder(Paths.MaxPathLength);
        var res = Kernel32.GetFinalPathNameByHandle(file, sb, (uint)sb.Capacity, Kernel32.FinalPathNameOptions.FILE_NAME_NORMALIZED);
        if (res == 0)
        {
            Win32Error.GetLastError().ThrowIfFailed();
        }

        var targetPath = sb.ToString();
        if (targetPath.StartsWith(longPathUncPrefix, StringComparison.Ordinal))
        {
            return targetPath.Replace(longPathUncPrefix, @"\\", StringComparison.Ordinal);
        }
        else
        {
            return targetPath.Replace(longPathPrefix, string.Empty, StringComparison.Ordinal);
        }
    }

    public static bool MatchesFilePattern(string filePath, string pattern)
    {
        if (filePath.IsNullOrEmpty() || pattern.IsNullOrEmpty())
        {
            return false;
        }

        if (pattern.Contains(';', StringComparison.Ordinal))
        {
            return ShlwApi.PathMatchSpecEx(filePath, pattern, ShlwApi.PMSF.PMSF_MULTIPLE) == 0;
        }
        else
        {
            return ShlwApi.PathMatchSpecEx(filePath, pattern, ShlwApi.PMSF.PMSF_NORMAL) == 0;
        }
    }
#endif
}
