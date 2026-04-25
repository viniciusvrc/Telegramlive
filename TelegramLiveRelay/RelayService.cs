using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramLiveRelay
{
    internal sealed class RelayService : IDisposable
    {
        private readonly object _processSync = new object();
        private Process _ffmpegProcess;
        private Process _ytDlpProcess;
        private bool _stopRequested;

        public bool IsRunning
        {
            get
            {
                lock (_processSync)
                {
                    return IsProcessRunning(_ffmpegProcess);
                }
            }
        }

        public event Action<string> LogReceived;
        public event Action<string> StatusChanged;

        public void Start(RelayOptions options)
        {
            StartInternal(options, true);
        }

        private void StartInternal(RelayOptions options, bool resetStopRequest)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("A transmissao ja esta em execucao.");
            }

            if (resetStopRequest)
            {
                lock (_processSync)
                {
                    _stopRequested = false;
                }
            }
            else if (IsStopRequested())
            {
                throw new OperationCanceledException();
            }

            ValidateExecutable(options.FfmpegPath, "ffmpeg.exe");
            ValidateExecutable(options.YtDlpPath, "yt-dlp.exe");

            RaiseStatusChanged("Resolvendo URL do YouTube...");
            var sourceUrl = ResolveYoutubeUrl(options);

            if (IsStopRequested())
            {
                throw new OperationCanceledException();
            }

            RaiseStatusChanged("Iniciando ffmpeg...");
            var targetUrl = BuildTargetUrl(options.ServerUrl, options.StreamKey);
            var ffmpegArguments = BuildFfmpegArguments(sourceUrl, targetUrl, options.ResolutionOption, options.SizeOption, options.AudioQualityOption);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = options.FfmpegPath,
                    Arguments = ffmpegArguments,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(options.FfmpegPath) ?? AppDomain.CurrentDomain.BaseDirectory
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs args) { ForwardLog(args.Data); };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args) { ForwardLog(args.Data); };
            process.Exited += delegate
            {
                HandleFfmpegExited(process, options);
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Nao foi possivel iniciar o ffmpeg.");
            }

            _ffmpegProcess = process;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            RaiseStatusChanged("Transmitindo para o Telegram...");
            RaiseLogReceived("Comando ffmpeg iniciado.");
        }

        public void Stop()
        {
            Process ytDlpProcess;
            Process ffmpegProcess;

            lock (_processSync)
            {
                _stopRequested = true;
                ytDlpProcess = _ytDlpProcess;
                ffmpegProcess = _ffmpegProcess;
            }

            if (ytDlpProcess == null && ffmpegProcess == null)
            {
                RaiseStatusChanged("Transmissao parada.");
                return;
            }

            RaiseStatusChanged("Encerrando transmissao...");
            StopProcess(ytDlpProcess);
            StopProcess(ffmpegProcess);
            RaiseStatusChanged("Transmissao parada.");
        }

        private string ResolveYoutubeUrl(RelayOptions options)
        {
            var attempts = BuildAttemptSequence(options);
            var failures = new List<string>();

            foreach (var attempt in attempts)
            {
                RaiseLogReceived("Tentando resolver YouTube: " + attempt.Description);
                var result = ExecuteYtDlp(options, attempt);
                if (result.Success)
                {
                    RaiseLogReceived("URL do YouTube resolvida com sucesso usando " + attempt.Description + ".");
                    return result.ResolvedUrl;
                }

                failures.Add("[" + attempt.Description + "] " + SimplifyError(result.Error));
                RaiseLogReceived("Falha em " + attempt.Description + ".");
            }

            throw new InvalidOperationException(BuildFriendlyYoutubeError(failures));
        }

        private List<YtDlpAttempt> BuildAttemptSequence(RelayOptions options)
        {
            var attempts = new List<YtDlpAttempt>();
            attempts.Add(new YtDlpAttempt("sem cookies", null));

            if (!string.IsNullOrWhiteSpace(options.CookiesFilePath))
            {
                attempts.Add(new YtDlpAttempt("arquivo cookies.txt", options.CookiesFilePath));
            }

            return attempts;
        }

        private YtDlpResult ExecuteYtDlp(RelayOptions options, YtDlpAttempt attempt)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = options.YtDlpPath,
                    Arguments = BuildYtDlpArguments(options, attempt),
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(options.YtDlpPath) ?? AppDomain.CurrentDomain.BaseDirectory
                }
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Nao foi possivel iniciar o yt-dlp.");
            }

            lock (_processSync)
            {
                _ytDlpProcess = process;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            var exitCode = process.ExitCode;

            lock (_processSync)
            {
                if (ReferenceEquals(_ytDlpProcess, process))
                {
                    _ytDlpProcess = null;
                }
            }

            process.Dispose();

            if (IsStopRequested())
            {
                throw new OperationCanceledException();
            }

            stdout = stdout.Trim();
            stderr = stderr.Trim();

            if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                return new YtDlpResult(false, null, stderr);
            }

            var resolvedUrl = stdout
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(resolvedUrl))
            {
                return new YtDlpResult(false, null, "O yt-dlp nao retornou uma URL de reproducao valida.");
            }

            return new YtDlpResult(true, resolvedUrl, null);
        }

        private static string BuildYtDlpArguments(RelayOptions options, YtDlpAttempt attempt)
        {
            var args = new StringBuilder();
            args.Append("-f \"best[ext=mp4]/best\" ");
            args.Append("--no-playlist ");
            args.Append("--get-url ");
            args.Append("--user-agent \"Mozilla/5.0\" ");

            if (!string.IsNullOrWhiteSpace(attempt.CookiesFilePath))
            {
                args.Append(string.Format("--cookies \"{0}\" ", attempt.CookiesFilePath));
            }

            var denoPath = Path.Combine(Path.GetDirectoryName(options.YtDlpPath) ?? AppDomain.CurrentDomain.BaseDirectory, "deno.exe");
            if (File.Exists(denoPath))
            {
                args.Append(string.Format("--js-runtimes \"deno:{0}\" ", denoPath));
            }

            args.Append(string.Format("\"{0}\"", options.YoutubeUrl));
            return args.ToString();
        }

        private static string BuildFriendlyYoutubeError(List<string> failures)
        {
            var builder = new StringBuilder();
            builder.AppendLine("O yt-dlp nao conseguiu resolver a URL informada.");
            builder.AppendLine("Tentativas executadas:");

            foreach (var failure in failures)
            {
                builder.AppendLine("- " + failure);
            }

            builder.AppendLine();
            builder.AppendLine("Se o YouTube continuar bloqueando, coloque um arquivo cookies.txt valido na pasta do programa ou informe o caminho manualmente.");
            return builder.ToString().Trim();
        }

        private static string SimplifyError(string stderr)
        {
            if (string.IsNullOrWhiteSpace(stderr))
            {
                return "Falha sem detalhes adicionais.";
            }

            var lowerError = stderr.ToLowerInvariant();

            if (lowerError.Contains("sign in to confirm"))
            {
                return "O YouTube exigiu login para confirmar que a requisicao nao e de bot.";
            }

            if (lowerError.Contains("too many requests"))
            {
                return "O YouTube retornou 429 Too Many Requests.";
            }

            if (lowerError.Contains("signature solving failed") ||
                lowerError.Contains("n challenge solving failed") ||
                lowerError.Contains("only images are available") ||
                lowerError.Contains("requested format is not available"))
            {
                return "O yt-dlp nao conseguiu resolver os desafios atuais do YouTube. Coloque um runtime JS suportado em tools\\deno.exe e tente novamente.";
            }

            if (stderr.Length > 240)
            {
                return stderr.Substring(0, 240).Trim() + "...";
            }

            return stderr;
        }

        private static string BuildTargetUrl(string serverUrl, string streamKey)
        {
            var normalizedServer = serverUrl.Trim().TrimEnd('/');
            var normalizedKey = streamKey.Trim().TrimStart('/');
            return string.Format("{0}/{1}", normalizedServer, normalizedKey);
        }

        private static string BuildFfmpegArguments(string sourceUrl, string targetUrl, string resolutionOption, string sizeOption, string audioQualityOption)
        {
            var args = new StringBuilder();
            args.Append("-hide_banner -loglevel info ");
            args.Append("-re ");
            args.Append(string.Format("-i \"{0}\" ", sourceUrl));
            args.Append("-map 0:v:0 -map 0:a? ");

            var normalizedResolution = string.IsNullOrWhiteSpace(resolutionOption) ? "Original" : resolutionOption.Trim();
            var normalizedSize = string.IsNullOrWhiteSpace(sizeOption) ? "100%" : sizeOption.Trim();
            var requiresResize = !string.Equals(normalizedSize, "100%", StringComparison.OrdinalIgnoreCase);
            var targetHeight = GetTargetHeight(normalizedResolution);

            if (targetHeight > 0 || requiresResize)
            {
                args.Append("-vf \"");
                args.Append(BuildScaleExpression(targetHeight, normalizedSize));
                args.Append("\" -c:v libx264 -preset ultrafast -tune zerolatency -pix_fmt yuv420p ");
                args.Append(BuildVideoRateArguments(targetHeight));
            }
            else
            {
                args.Append("-c:v copy ");
            }

            args.Append(BuildAudioArguments(audioQualityOption));
            args.Append("-f flv ");
            args.Append(string.Format("\"{0}\"", targetUrl));
            return args.ToString();
        }

        private static int GetTargetHeight(string normalizedResolution)
        {
            if (string.Equals(normalizedResolution, "1080p", StringComparison.OrdinalIgnoreCase))
            {
                return 1080;
            }

            if (string.Equals(normalizedResolution, "720p", StringComparison.OrdinalIgnoreCase))
            {
                return 720;
            }

            if (string.Equals(normalizedResolution, "480p", StringComparison.OrdinalIgnoreCase))
            {
                return 480;
            }

            if (string.Equals(normalizedResolution, "360p", StringComparison.OrdinalIgnoreCase))
            {
                return 360;
            }

            if (string.Equals(normalizedResolution, "240p", StringComparison.OrdinalIgnoreCase))
            {
                return 240;
            }

            if (string.Equals(normalizedResolution, "144p", StringComparison.OrdinalIgnoreCase))
            {
                return 144;
            }

            return 0;
        }

        private static string BuildScaleExpression(int targetHeight, string normalizedSize)
        {
            var sizeFactor = GetSizeFactor(normalizedSize);
            if (targetHeight > 0)
            {
                var scaledHeight = EnsureEven((int)Math.Round(targetHeight * sizeFactor));
                return string.Format("scale=-2:{0}", scaledHeight);
            }

            return string.Format(
                "scale=trunc(iw*{0}/2)*2:trunc(ih*{0}/2)*2",
                sizeFactor.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static string BuildVideoRateArguments(int targetHeight)
        {
            if (targetHeight <= 0)
            {
                return string.Empty;
            }

            if (targetHeight <= 144)
            {
                return "-r 15 -g 30 -b:v 300k -maxrate 350k -bufsize 600k ";
            }

            if (targetHeight <= 240)
            {
                return "-r 20 -g 40 -b:v 500k -maxrate 600k -bufsize 1000k ";
            }

            if (targetHeight <= 360)
            {
                return "-r 20 -g 40 -b:v 800k -maxrate 900k -bufsize 1400k ";
            }

            if (targetHeight <= 480)
            {
                return "-r 24 -g 48 -b:v 1200k -maxrate 1400k -bufsize 2000k ";
            }

            if (targetHeight <= 720)
            {
                return "-r 24 -g 48 -b:v 2000k -maxrate 2400k -bufsize 3200k ";
            }

            return "-r 25 -g 50 -b:v 3000k -maxrate 3500k -bufsize 4500k ";
        }

        private static double GetSizeFactor(string normalizedSize)
        {
            if (string.Equals(normalizedSize, "90%", StringComparison.OrdinalIgnoreCase))
            {
                return 0.90;
            }

            if (string.Equals(normalizedSize, "80%", StringComparison.OrdinalIgnoreCase))
            {
                return 0.80;
            }

            if (string.Equals(normalizedSize, "70%", StringComparison.OrdinalIgnoreCase))
            {
                return 0.70;
            }

            if (string.Equals(normalizedSize, "60%", StringComparison.OrdinalIgnoreCase))
            {
                return 0.60;
            }

            if (string.Equals(normalizedSize, "50%", StringComparison.OrdinalIgnoreCase))
            {
                return 0.50;
            }

            return 1.00;
        }

        private static int EnsureEven(int value)
        {
            if (value < 2)
            {
                return 2;
            }

            return value % 2 == 0 ? value : value - 1;
        }

        private static string BuildAudioArguments(string audioQualityOption)
        {
            var normalizedAudio = string.IsNullOrWhiteSpace(audioQualityOption) ? "Padrao" : audioQualityOption.Trim();
            if (string.Equals(normalizedAudio, "Leve", StringComparison.OrdinalIgnoreCase))
            {
                return "-c:a aac -b:a 96k -ar 44100 -ac 1 ";
            }

            if (string.Equals(normalizedAudio, "Baixa", StringComparison.OrdinalIgnoreCase))
            {
                return "-c:a aac -b:a 64k -ar 32000 -ac 1 ";
            }

            if (string.Equals(normalizedAudio, "Muito baixa", StringComparison.OrdinalIgnoreCase))
            {
                return "-c:a aac -b:a 48k -ar 22050 -ac 1 ";
            }

            return "-c:a aac -b:a 128k -ar 44100 -ac 1 ";
        }

        private static void ValidateExecutable(string path, string expectedName)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    string.Format("Arquivo obrigatorio nao encontrado: {0}. Coloque-o em tools\\{0}.", expectedName),
                    path);
            }
        }

        private void ForwardLog(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            RaiseLogReceived(line.Trim());
        }

        private void RaiseLogReceived(string message)
        {
            var handler = LogReceived;
            if (handler != null)
            {
                handler(message);
            }
        }

        private void RaiseStatusChanged(string message)
        {
            var handler = StatusChanged;
            if (handler != null)
            {
                handler(message);
            }
        }

        private bool IsStopRequested()
        {
            lock (_processSync)
            {
                return _stopRequested;
            }
        }

        private void StopProcess(Process process)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                if (process.HasExited)
                {
                    return;
                }

                process.Kill();
                process.WaitForExit(5000);
            }
            catch
            {
            }
        }

        private void HandleFfmpegExited(Process process, RelayOptions options)
        {
            var exitCode = TryGetExitCode(process);

            lock (_processSync)
            {
                if (ReferenceEquals(_ffmpegProcess, process))
                {
                    _ffmpegProcess = null;
                }
            }

            try
            {
                if (!IsStopRequested() && options.RepeatEnabled && exitCode == 0)
                {
                    RaiseStatusChanged("Repetindo transmissao...");
                    Task.Factory.StartNew(
                        delegate
                        {
                            try
                            {
                                StartInternal(options, false);
                            }
                            catch (Exception ex)
                            {
                                RaiseLogReceived(ex.Message);
                                RaiseStatusChanged("Falha ao repetir.");
                            }
                        });
                    return;
                }

                RaiseStatusChanged(IsStopRequested() ? "Transmissao parada." : "Transmissao finalizada.");
            }
            catch (Exception ex)
            {
                RaiseLogReceived("Erro ao finalizar o ffmpeg: " + ex.Message);
                RaiseStatusChanged(IsStopRequested() ? "Transmissao parada." : "Transmissao finalizada.");
            }
            finally
            {
                try
                {
                    process.Dispose();
                }
                catch
                {
                }
            }
        }

        private static int TryGetExitCode(Process process)
        {
            try
            {
                if (!process.WaitForExit(1000))
                {
                    return int.MinValue;
                }

                return process.ExitCode;
            }
            catch
            {
                return int.MinValue;
            }
        }

        private static bool IsProcessRunning(Process process)
        {
            if (process == null)
            {
                return false;
            }

            try
            {
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private sealed class YtDlpAttempt
        {
            public YtDlpAttempt(string description, string cookiesFilePath)
            {
                Description = description;
                CookiesFilePath = cookiesFilePath;
            }

            public string Description { get; private set; }

            public string CookiesFilePath { get; private set; }
        }

        private sealed class YtDlpResult
        {
            public YtDlpResult(bool success, string resolvedUrl, string error)
            {
                Success = success;
                ResolvedUrl = resolvedUrl;
                Error = error;
            }

            public bool Success { get; private set; }

            public string ResolvedUrl { get; private set; }

            public string Error { get; private set; }
        }
    }
}
