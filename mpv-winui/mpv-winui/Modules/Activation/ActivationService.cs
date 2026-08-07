using Microsoft.Windows.AppLifecycle;
using mpv_winui.Modules.Player;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;

namespace mpv_winui.Modules.Activation
{
    public class ActivationService
    {
        private static readonly Lazy<ActivationService> _lazyValue = new(() => new ActivationService(), true);

        public static ActivationService Instance => _lazyValue.Value;

        private ActivationService()
        {
        }

        /// <summary>
        /// 解析 mpv-winui://?file=... 协议参数。unpackaged 模式下自定义协议经 ShellExecute
        /// 启动时 AppInstance 上报的是 Launch 而非 Protocol，URI 在 Arguments 里，
        /// 因此 Launch 分支也要走这里。
        /// </summary>
        private static string? ParseMpvWinuiUri(string? arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) return null;
            if (!arg.StartsWith("mpv-winui:", StringComparison.OrdinalIgnoreCase)) return null;
            if (!Uri.TryCreate(arg, UriKind.Absolute, out var uri)) return null;
            var query = uri.Query;
            if (!query.StartsWith("?file=", StringComparison.Ordinal)) return null;
            var path = query.Substring(6);
            return string.IsNullOrEmpty(path) ? null : Uri.UnescapeDataString(path);
        }

        /// <summary>
        /// 收集 unpackaged Launch 激活的文件/URI 参数：
        /// GetCommandLineArgs()[1..] 是托管视图（argv[0] 为 dll）；
        /// ILaunchActivatedEventArgs.Arguments 是原始命令行（首 token 为 exe），
        /// 两种来源都取、跳过首 token，再去重。
        /// </summary>
        private static IEnumerable<string> CommandLineCandidates(AppActivationArguments activatedArgs)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var cmd = Environment.GetCommandLineArgs();
            for (int i = 1; i < cmd.Length; i++)
            {
                var t = cmd[i].Trim().Trim('"');
                if (t.Length > 0 && seen.Add(t)) yield return t;
            }

            if (activatedArgs.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launchArgs
                && !string.IsNullOrWhiteSpace(launchArgs.Arguments))
            {
                var tokens = Regex.Matches(launchArgs.Arguments, "\"([^\"]*)\"|(\\S+)")
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
                    .ToList();
                for (int i = 1; i < tokens.Count; i++)
                {
                    var t = tokens[i].Trim().Trim('"');
                    if (t.Length > 0 && seen.Add(t)) yield return t;
                }
            }
        }

        public IReadOnlyList<string>? Parse(AppActivationArguments activatedArgs)
        {
            switch (activatedArgs.Kind)
            {
                case ExtendedActivationKind.File:
                {
                    if (activatedArgs.Data is Windows.ApplicationModel.Activation.IFileActivatedEventArgs fileArgs)
                    {
                        if (fileArgs.Files?.Count > 0)
                        {
                            return fileArgs.Files.Select(x => x.Path).ToList();
                        }
                    }
                    break;
                }

                case ExtendedActivationKind.Protocol:
                {
                    if (activatedArgs.Data is Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs protocolArgs)
                    {
                        if (protocolArgs.Uri.Scheme == "mpv-winui")
                        {
                            var query = protocolArgs.Uri.Query;
                            if (query.StartsWith("?file="))
                            {
                                var file = query.Substring(6);
                                if (!string.IsNullOrEmpty(file))
                                {
                                    return (string[])[Uri.UnescapeDataString(file)];
                                }
                            }
                        }
                    }
                    break;
                }

                case ExtendedActivationKind.Launch:
                {
                    foreach (var candidate in CommandLineCandidates(activatedArgs))
                    {
                        if (ParseMpvWinuiUri(candidate) is string uriPath)
                        {
                            return (string[])[uriPath];
                        }

                        if (File.Exists(candidate) || Directory.Exists(candidate))
                        {
                            return (string[])[candidate];
                        }
                    }
                    break;
                }

                default:
                {
                    break;
                }
            }

            return [];
        }

        public async Task<IReadOnlyList<FileItem>?> ParseFileItemsAsync(AppActivationArguments activatedArgs)
        {
            switch (activatedArgs.Kind)
            {
                case ExtendedActivationKind.File:
                {
                    if (activatedArgs.Data is Windows.ApplicationModel.Activation.IFileActivatedEventArgs fileArgs)
                    {
                        if (fileArgs.Files?.Count > 0)
                        {
                            return fileArgs.Files
                                .Where(x => !x.IsOfType(StorageItemTypes.None))
                                .Select(x => new FileItem(x.Path, x.IsOfType(StorageItemTypes.File) ? FileType.File : FileType.Folder))
                                .ToList();
                        }
                    }
                    break;
                }

                case ExtendedActivationKind.Protocol:
                {
                    if (activatedArgs.Data is Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs protocolArgs)
                    {
                        if (protocolArgs.Uri.Scheme == "mpv-winui")
                        {
                            var query = protocolArgs.Uri.Query;
                            if (query.StartsWith("?file="))
                            {
                                var path = query.Substring(6);
                                if (!string.IsNullOrEmpty(path))
                                {
                                    FileItem? item = await Task.Run(() =>
                                    {
                                        if (Uri.TryCreate(path, UriKind.Absolute, out Uri? uri) && !uri.IsFile)
                                        {
                                            return new FileItem(path, FileType.Url);
                                        }

                                        if (Directory.Exists(path))
                                        {
                                            return new FileItem(path, FileType.Folder);
                                        }

                                        if (File.Exists(path))
                                        {
                                            return new FileItem(path, FileType.File);
                                        }

                                        return null;
                                    });

                                    return item == null ? [] : (FileItem[])[item];
                                }
                            }
                        }
                    }
                    break;
                }

                case ExtendedActivationKind.Launch:
                {
                    foreach (var candidate in CommandLineCandidates(activatedArgs))
                    {
                        var path = ParseMpvWinuiUri(candidate) ?? candidate;
                        FileItem? item = await Task.Run(() =>
                        {
                            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile)
                            {
                                return new FileItem(path, FileType.Url);
                            }

                            if (Directory.Exists(path))
                            {
                                return new FileItem(path, FileType.Folder);
                            }

                            if (File.Exists(path))
                            {
                                return new FileItem(path, FileType.File);
                            }

                            return null;
                        });

                        if (item is not null)
                        {
                            return (FileItem[])[item];
                        }
                    }
                    break;
                }

                default:
                {
                    break;
                }
            }

            return [];
        }
    }
}
