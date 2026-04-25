using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TelegramLiveRelay
{
    internal sealed class MainForm : Form
    {
        private const string AccessPasswordValue = "@viniciusvrc";
        private const string CookiesExtensionUrl = "https://chromewebstore.google.com/detail/get-cookiestxt-locally/cclelndahbckbenkjhflpdbgdldlbecc";
        private const string DefaultTelegramRtmpServer = "rtmps://dc1-1.rtmp.t.me/s/";
        private readonly TextBox _serverUrlTextBox;
        private readonly TextBox _streamKeyTextBox;
        private readonly TextBox _youtubeUrlTextBox;
        private readonly TextBox _cookiesFileTextBox;
        private readonly ComboBox _resolutionComboBox;
        private readonly ComboBox _sizeComboBox;
        private readonly ComboBox _audioQualityComboBox;
        private readonly CheckBox _repeatCheckBox;
        private readonly TextBox _logTextBox;
        private readonly Button _startButton;
        private readonly Button _stopButton;
        private readonly Button _installExtensionButton;
        private readonly Button _themeButton;
        private readonly Label _statusLabel;
        private readonly Label _cookieCheckLabel;
        private readonly Label _denoCheckLabel;
        private readonly Label _footerLabel;
        private readonly Timer _stateTimer;
        private readonly RelayService _relayService;
        private readonly string _settingsPath;
        private bool _isBusy;
        private bool _accessGranted;
        private bool _darkThemeEnabled;

        public MainForm()
        {
            Text = "Telegramlive";
            Width = 860;
            Height = 720;
            MinimumSize = new Size(820, 620);
            StartPosition = FormStartPosition.CenterScreen;

            _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.txt");
            _relayService = new RelayService();
            _relayService.LogReceived += delegate(string message) { SafeUi(delegate { AppendLog(message); }); };
            _relayService.StatusChanged += delegate(string message) { SafeUi(delegate { UpdateStatus(message); }); };
            _stateTimer = new Timer();
            _stateTimer.Interval = 400;
            _stateTimer.Tick += StateTimer_Tick;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 1,
                RowCount = 5
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var introLabel = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4),
                Text = "Preencha o RTMP do Telegram e o link do YouTube. Se precisar do cookies.txt, use o botao Instalar plugin."
            };

            var headerPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 4)
            };
            headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var formPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(0, 12, 0, 12)
            };
            formPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            formPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _serverUrlTextBox = CreateTextBox();
            _streamKeyTextBox = CreateTextBox();
            _youtubeUrlTextBox = CreateTextBox();
            _cookiesFileTextBox = CreateTextBox();
            _resolutionComboBox = CreateResolutionComboBox();
            _sizeComboBox = CreateSizeComboBox();
            _audioQualityComboBox = CreateAudioQualityComboBox();
            _repeatCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "Repetir",
                Margin = new Padding(10, 6, 0, 0)
            };

            var youtubePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0)
            };
            youtubePanel.Controls.Add(_youtubeUrlTextBox);
            youtubePanel.Controls.Add(_repeatCheckBox);

            AddField(formPanel, 0, "Servidor RTMP", _serverUrlTextBox);
            AddField(formPanel, 1, "Chave da stream", _streamKeyTextBox);
            AddField(formPanel, 2, "Link do YouTube", youtubePanel);
            AddField(formPanel, 3, "Arquivo de cookies", _cookiesFileTextBox);
            AddField(formPanel, 4, "Resolucao", _resolutionComboBox);
            AddField(formPanel, 5, "Tamanho do video", _sizeComboBox);
            AddField(formPanel, 6, "Qualidade do audio", _audioQualityComboBox);

            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(0, 0, 0, 12)
            };

            _startButton = new Button
            {
                Text = "Iniciar",
                AutoSize = true,
                Padding = new Padding(10, 4, 10, 4),
                MinimumSize = new Size(92, 32),
                Margin = new Padding(0, 0, 8, 8)
            };
            _startButton.Click += StartButton_Click;

            _stopButton = new Button
            {
                Text = "Parar",
                AutoSize = true,
                Enabled = false,
                Padding = new Padding(10, 4, 10, 4),
                MinimumSize = new Size(92, 32),
                Margin = new Padding(0, 0, 8, 8)
            };
            _stopButton.Click += delegate { StopRelay(); };

            var saveButton = new Button
            {
                Text = "Salvar",
                AutoSize = true,
                Padding = new Padding(10, 4, 10, 4),
                MinimumSize = new Size(92, 32),
                Margin = new Padding(0, 0, 8, 8)
            };
            saveButton.Click += delegate { SaveSettings(true); };

            _installExtensionButton = new Button
            {
                Text = "Instalar plugin",
                AutoSize = true,
                Padding = new Padding(10, 4, 10, 4),
                MinimumSize = new Size(120, 32),
                Margin = new Padding(0, 0, 8, 8)
            };
            _installExtensionButton.Click += InstallExtensionButton_Click;

            _themeButton = new Button
            {
                Text = "Modo claro",
                AutoSize = true,
                Padding = new Padding(10, 4, 10, 4),
                MinimumSize = new Size(110, 32),
                Margin = new Padding(8, 0, 0, 0),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _themeButton.Click += ThemeButton_Click;

            _statusLabel = new Label
            {
                AutoSize = true,
                Padding = new Padding(12, 8, 0, 0),
                Text = "Status: aguardando"
            };

            _cookieCheckLabel = new Label
            {
                AutoSize = true,
                Padding = new Padding(12, 8, 0, 0),
                Text = "Cookies: pendente"
            };

            _denoCheckLabel = new Label
            {
                AutoSize = true,
                Padding = new Padding(12, 8, 0, 0),
                Text = "Deno: pendente"
            };

            actionPanel.Controls.Add(_startButton);
            actionPanel.Controls.Add(_stopButton);
            actionPanel.Controls.Add(_installExtensionButton);
            actionPanel.Controls.Add(saveButton);
            actionPanel.Controls.Add(_statusLabel);
            actionPanel.Controls.Add(_cookieCheckLabel);
            actionPanel.Controls.Add(_denoCheckLabel);

            _logTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 10),
                BackColor = Color.White
            };

            _footerLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(0, 10, 0, 0),
                Text = "Desenvolvido por: Viniciusvrc  Parceria: Erick Costa"
            };

            headerPanel.Controls.Add(introLabel, 0, 0);
            headerPanel.Controls.Add(_themeButton, 1, 0);

            root.Controls.Add(headerPanel, 0, 0);
            root.Controls.Add(formPanel, 0, 1);
            root.Controls.Add(actionPanel, 0, 2);
            root.Controls.Add(_logTextBox, 0, 3);
            root.Controls.Add(_footerLabel, 0, 4);

            Controls.Add(root);

            _cookiesFileTextBox.TextChanged += delegate { UpdateDependencyStatus(); };
            _youtubeUrlTextBox.TextChanged += delegate { UpdateDependencyStatus(); };

            Load += delegate
            {
                LoadSettings();
                ApplyTheme();
                RefreshActionButtons();
                _stateTimer.Start();
            };
            FormClosing += MainForm_FormClosing;
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (!EnsureAccessGranted())
                {
                    return;
                }

                AutoFillCookiesPath();
                var options = BuildOptions();
                SaveSettings(false);

                ToggleRunningState(true);
                AppendLog("Iniciando transmissao...");

                Task.Factory.StartNew(
                    delegate
                    {
                        try
                        {
                            _relayService.Start(options);
                        }
                        catch (Exception ex)
                        {
                            SafeUi(
                                delegate
                                {
                                    ToggleRunningState(false);
                                    if (ex is OperationCanceledException)
                                    {
                                        UpdateStatus("Transmissao cancelada.");
                                        AppendLog("Transmissao cancelada.");
                                        return;
                                    }

                                    UpdateStatus("Falha ao iniciar.");
                                    AppendLog(ex.Message);
                                    MessageBox.Show(this, ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                });
                        }
                    });
            }
            catch (Exception ex)
            {
                ToggleRunningState(false);
                UpdateStatus("Falha ao iniciar.");
                AppendLog(ex.Message);
                MessageBox.Show(this, ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopRelay()
        {
            _relayService.Stop();
            ToggleRunningState(false);
            RefreshActionButtons();
        }

        private RelayOptions BuildOptions()
        {
            var serverUrl = _serverUrlTextBox.Text.Trim();
            var streamKey = _streamKeyTextBox.Text.Trim();
            var youtubeUrl = _youtubeUrlTextBox.Text.Trim();
            var cookiesFilePath = _cookiesFileTextBox.Text.Trim();
            var resolutionOption = (_resolutionComboBox.SelectedItem as string ?? "Original").Trim();
            var sizeOption = (_sizeComboBox.SelectedItem as string ?? "100%").Trim();
            var audioQualityOption = (_audioQualityComboBox.SelectedItem as string ?? "Padrao").Trim();

            if (string.IsNullOrWhiteSpace(serverUrl) ||
                string.IsNullOrWhiteSpace(streamKey) ||
                string.IsNullOrWhiteSpace(youtubeUrl))
            {
                throw new InvalidOperationException("Preencha servidor RTMP, stream key e URL do YouTube.");
            }

            Uri ignored;
            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out ignored))
            {
                throw new InvalidOperationException("O servidor RTMP precisa ser uma URL valida.");
            }

            if (!Uri.TryCreate(youtubeUrl, UriKind.Absolute, out ignored))
            {
                throw new InvalidOperationException("A URL do YouTube precisa ser valida.");
            }

            if (!string.IsNullOrWhiteSpace(cookiesFilePath) && !File.Exists(cookiesFilePath))
            {
                throw new InvalidOperationException("O arquivo cookies informado nao existe.");
            }

            var toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
            return new RelayOptions(
                serverUrl,
                streamKey,
                youtubeUrl,
                cookiesFilePath,
                resolutionOption,
                sizeOption,
                audioQualityOption,
                _repeatCheckBox.Checked,
                Path.Combine(toolsPath, "ffmpeg.exe"),
                Path.Combine(toolsPath, "yt-dlp.exe"));
        }

        private bool EnsureAccessGranted()
        {
            if (_accessGranted)
            {
                return true;
            }

            var password = PromptForPassword();
            if (password == null)
            {
                AppendLog("Inicio cancelado. A senha nao foi informada.");
                return false;
            }

            if (!string.Equals(password, AccessPasswordValue, StringComparison.Ordinal))
            {
                AppendLog("Senha invalida.");
                MessageBox.Show(this, "Senha invalida.", "Telegramlive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            _accessGranted = true;
            SaveSettings(false);
            AppendLog("Acesso liberado com sucesso.");
            return true;
        }

        private string PromptForPassword()
        {
            using (var dialog = new Form())
            {
                dialog.Text = "Liberar acesso";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(360, 150);

                var infoLabel = new Label
                {
                    Left = 16,
                    Top = 16,
                    Width = 320,
                    AutoSize = false,
                    Text = "Digite a senha para liberar o uso do programa."
                };

                var passwordTextBox = new TextBox
                {
                    Left = 16,
                    Top = 52,
                    Width = 320,
                    UseSystemPasswordChar = true
                };

                var confirmButton = new Button
                {
                    Text = "Confirmar",
                    Left = 176,
                    Top = 96,
                    Width = 75,
                    DialogResult = DialogResult.OK
                };

                var cancelButton = new Button
                {
                    Text = "Cancelar",
                    Left = 261,
                    Top = 96,
                    Width = 75,
                    DialogResult = DialogResult.Cancel
                };

                dialog.Controls.Add(infoLabel);
                dialog.Controls.Add(passwordTextBox);
                dialog.Controls.Add(confirmButton);
                dialog.Controls.Add(cancelButton);
                dialog.AcceptButton = confirmButton;
                dialog.CancelButton = cancelButton;

                ApplyThemeToControl(dialog);
                return dialog.ShowDialog(this) == DialogResult.OK ? passwordTextBox.Text : null;
            }
        }

        private void ThemeButton_Click(object sender, EventArgs e)
        {
            _darkThemeEnabled = !_darkThemeEnabled;
            ApplyTheme();
            SaveSettings(false);
        }

        private void LoadSettings()
        {
            if (!File.Exists(_settingsPath))
            {
                ApplyDefaultValues();
                UpdateDependencyStatus();
                return;
            }

            try
            {
                var lines = File.ReadAllLines(_settingsPath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || !line.Contains("="))
                    {
                        continue;
                    }

                    var separatorIndex = line.IndexOf('=');
                    var key = line.Substring(0, separatorIndex);
                    var value = line.Substring(separatorIndex + 1);

                    if (string.Equals(key, "ServerUrl", StringComparison.OrdinalIgnoreCase))
                    {
                        _serverUrlTextBox.Text = value;
                    }
                    else if (string.Equals(key, "StreamKey", StringComparison.OrdinalIgnoreCase))
                    {
                        _streamKeyTextBox.Text = value;
                    }
                    else if (string.Equals(key, "YoutubeUrl", StringComparison.OrdinalIgnoreCase))
                    {
                        _youtubeUrlTextBox.Text = value;
                    }
                    else if (string.Equals(key, "CookiesFilePath", StringComparison.OrdinalIgnoreCase))
                    {
                        _cookiesFileTextBox.Text = IsLegacyBrowserCookieSource(value) ? string.Empty : value;
                    }
                    else if (string.Equals(key, "ResolutionOption", StringComparison.OrdinalIgnoreCase))
                    {
                        SetResolutionOption(value);
                    }
                    else if (string.Equals(key, "SizeOption", StringComparison.OrdinalIgnoreCase))
                    {
                        SetComboBoxValue(_sizeComboBox, value, "100%");
                    }
                    else if (string.Equals(key, "AudioQualityOption", StringComparison.OrdinalIgnoreCase))
                    {
                        SetComboBoxValue(_audioQualityComboBox, value, "Padrao");
                    }
                    else if (string.Equals(key, "RepeatEnabled", StringComparison.OrdinalIgnoreCase))
                    {
                        _repeatCheckBox.Checked = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (string.Equals(key, "Theme", StringComparison.OrdinalIgnoreCase))
                    {
                        _darkThemeEnabled = string.Equals(value, "Escuro", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (string.Equals(key, "AccessGranted", StringComparison.OrdinalIgnoreCase))
                    {
                        _accessGranted = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                    }
                }

                AutoFillCookiesPath();
                ApplyDefaultValues();
                UpdateDependencyStatus();
            }
            catch
            {
                AppendLog("Nao foi possivel carregar o arquivo appsettings.txt.");
                ApplyDefaultValues();
            }
        }

        private void SaveSettings(bool showConfirmation)
        {
            var lines = new[]
            {
                "ServerUrl=" + _serverUrlTextBox.Text.Trim(),
                "StreamKey=" + _streamKeyTextBox.Text.Trim(),
                "YoutubeUrl=" + _youtubeUrlTextBox.Text.Trim(),
                "CookiesFilePath=" + _cookiesFileTextBox.Text.Trim(),
                "ResolutionOption=" + (_resolutionComboBox.SelectedItem as string ?? "Original"),
                "SizeOption=" + (_sizeComboBox.SelectedItem as string ?? "100%"),
                "AudioQualityOption=" + (_audioQualityComboBox.SelectedItem as string ?? "Padrao"),
                "RepeatEnabled=" + (_repeatCheckBox.Checked ? "true" : "false"),
                "Theme=" + (_darkThemeEnabled ? "Escuro" : "Claro"),
                "AccessGranted=" + (_accessGranted ? "true" : "false"),
                "AccessPassword=" + (_accessGranted ? AccessPasswordValue : string.Empty)
            };

            File.WriteAllLines(_settingsPath, lines);

            if (showConfirmation)
            {
                MessageBox.Show(this, "Dados salvos com sucesso.", "Telegramlive", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _stateTimer.Stop();
            _relayService.Dispose();
        }

        private void ToggleRunningState(bool isRunning)
        {
            _isBusy = isRunning;
            RefreshActionButtons();
        }

        private void StateTimer_Tick(object sender, EventArgs e)
        {
            RefreshActionButtons();
        }

        private void RefreshActionButtons()
        {
            var shouldKeepStopEnabled = _isBusy || _relayService.IsRunning;
            _startButton.Enabled = !shouldKeepStopEnabled;
            _stopButton.Enabled = shouldKeepStopEnabled;
        }

        private void UpdateDependencyStatus()
        {
            var cookiesPath = _cookiesFileTextBox.Text.Trim();
            var cookieStatus = EvaluateCookieStatus(cookiesPath);
            _cookieCheckLabel.Text = "Cookies: " + cookieStatus;
            _cookieCheckLabel.ForeColor = cookieStatus.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0
                ? Color.ForestGreen
                : (_darkThemeEnabled ? Color.LightCoral : Color.DarkRed);

            var denoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "deno.exe");
            var denoExists = File.Exists(denoPath);
            _denoCheckLabel.Text = denoExists ? "Deno: OK" : "Deno: ausente";
            _denoCheckLabel.ForeColor = denoExists ? Color.ForestGreen : (_darkThemeEnabled ? Color.LightCoral : Color.DarkRed);
        }

        private static string EvaluateCookieStatus(string cookiesPath)
        {
            if (string.IsNullOrWhiteSpace(cookiesPath))
            {
                return "faltando";
            }

            if (!File.Exists(cookiesPath))
            {
                return "arquivo nao encontrado";
            }

            try
            {
                using (var reader = new StreamReader(cookiesPath))
                {
                    var header = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(header) || header.IndexOf("Netscape HTTP Cookie File", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return "formato invalido";
                    }
                }

                var content = File.ReadAllText(cookiesPath);
                var hasYoutube = content.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0;
                var hasSession = content.IndexOf("LOGIN_INFO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 content.IndexOf("SAPISID", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 content.IndexOf("SID", StringComparison.OrdinalIgnoreCase) >= 0;

                if (hasYoutube && hasSession)
                {
                    return "OK";
                }

                if (hasYoutube)
                {
                    return "parcial";
                }

                return "sem youtube";
            }
            catch
            {
                return "erro ao ler";
            }
        }

        private void UpdateStatus(string message)
        {
            _statusLabel.Text = "Status: " + message;
            if (IsTerminalStatus(message))
            {
                ToggleRunningState(false);
            }
            else if (_isBusy)
            {
                RefreshActionButtons();
            }
        }

        private static bool IsTerminalStatus(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.IndexOf("finalizada", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("parada", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("cancelada", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("falha", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AppendLog(string message)
        {
            _logTextBox.AppendText(string.Format("[{0:HH:mm:ss}] {1}{2}", DateTime.Now, message, Environment.NewLine));
        }

        private void ApplyTheme()
        {
            _themeButton.Text = _darkThemeEnabled ? "Modo escuro" : "Modo claro";
            ApplyThemeToControl(this);
            UpdateDependencyStatus();
        }

        private void ApplyThemeToControl(Control control)
        {
            var backColor = _darkThemeEnabled ? Color.FromArgb(30, 34, 40) : Color.FromArgb(245, 245, 245);
            var panelColor = _darkThemeEnabled ? Color.FromArgb(37, 41, 48) : Color.White;
            var textColor = _darkThemeEnabled ? Color.Gainsboro : Color.FromArgb(28, 28, 28);
            var buttonBack = _darkThemeEnabled ? Color.FromArgb(61, 90, 128) : Color.FromArgb(220, 228, 239);
            var buttonText = _darkThemeEnabled ? Color.White : Color.FromArgb(20, 20, 20);

            if (control is Form)
            {
                control.BackColor = backColor;
                control.ForeColor = textColor;
            }
            else if (control is TableLayoutPanel || control is FlowLayoutPanel || control is Panel)
            {
                control.BackColor = backColor;
                control.ForeColor = textColor;
            }
            else if (control is TextBox)
            {
                var textBox = (TextBox)control;
                textBox.BackColor = textBox.ReadOnly
                    ? (_darkThemeEnabled ? Color.FromArgb(20, 24, 28) : Color.White)
                    : panelColor;
                textBox.ForeColor = textColor;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is ComboBox)
            {
                control.BackColor = panelColor;
                control.ForeColor = textColor;
            }
            else if (control is Button)
            {
                var button = (Button)control;
                button.BackColor = buttonBack;
                button.ForeColor = buttonText;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = _darkThemeEnabled ? Color.FromArgb(94, 129, 172) : Color.FromArgb(166, 180, 200);
            }
            else
            {
                control.BackColor = backColor;
                control.ForeColor = textColor;
            }

            for (var index = 0; index < control.Controls.Count; index++)
            {
                ApplyThemeToControl(control.Controls[index]);
            }
        }

        private void SafeUi(Action action)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(action);
                return;
            }

            action();
        }

        private static TextBox CreateTextBox()
        {
            return new TextBox
            {
                Dock = DockStyle.Top,
                Width = 560
            };
        }

        private static ComboBox CreateResolutionComboBox()
        {
            var comboBox = new ComboBox
            {
                Dock = DockStyle.Top,
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            comboBox.Items.AddRange(new object[] { "Original", "1080p", "720p", "480p", "360p", "240p", "144p" });
            comboBox.SelectedItem = "360p";
            return comboBox;
        }

        private static ComboBox CreateSizeComboBox()
        {
            var comboBox = new ComboBox
            {
                Dock = DockStyle.Top,
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            comboBox.Items.AddRange(new object[] { "100%", "90%", "80%", "70%", "60%", "50%" });
            comboBox.SelectedItem = "50%";
            return comboBox;
        }

        private static ComboBox CreateAudioQualityComboBox()
        {
            var comboBox = new ComboBox
            {
                Dock = DockStyle.Top,
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            comboBox.Items.AddRange(new object[] { "Padrao", "Leve", "Baixa", "Muito baixa" });
            comboBox.SelectedItem = "Padrao";
            return comboBox;
        }

        private void SetResolutionOption(string value)
        {
            SetComboBoxValue(_resolutionComboBox, value, "Original");
        }

        private static void SetComboBoxValue(ComboBox comboBox, string value, string fallback)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            for (var index = 0; index < comboBox.Items.Count; index++)
            {
                var item = comboBox.Items[index] as string;
                if (string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }

            for (var index = 0; index < comboBox.Items.Count; index++)
            {
                var item = comboBox.Items[index] as string;
                if (string.Equals(item, fallback, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private void AutoFillCookiesPath()
        {
            if (IsLegacyBrowserCookieSource(_cookiesFileTextBox.Text))
            {
                _cookiesFileTextBox.Text = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(_cookiesFileTextBox.Text) && File.Exists(_cookiesFileTextBox.Text))
            {
                return;
            }

            var detectedPath = DetectCookiesFile();
            if (!string.IsNullOrWhiteSpace(detectedPath))
            {
                _cookiesFileTextBox.Text = detectedPath;
                AppendLog("Arquivo cookies preenchido automaticamente: " + detectedPath);
                UpdateDependencyStatus();
            }
        }

        private static string DetectCookiesFile()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var directCandidates = new[]
            {
                Path.Combine(baseDir, "www.youtube.com_cookies.txt"),
                Path.Combine(baseDir, "cookies.txt"),
                Path.Combine(baseDir, "youtube-cookies.txt"),
                Path.Combine(baseDir, "cookies", "cookies.txt"),
                Path.Combine(baseDir, "tools", "cookies.txt")
            };

            foreach (var candidate in directCandidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            var patternMatches = Directory.GetFiles(baseDir, "*cookie*.txt", SearchOption.TopDirectoryOnly);
            if (patternMatches.Length > 0)
            {
                return patternMatches[0];
            }

            return null;
        }

        private void InstallExtensionButton_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = CookiesExtensionUrl,
                    UseShellExecute = true
                });
                AppendLog("Pagina da extensao aberta no navegador.");
            }
            catch (Exception ex)
            {
                AppendLog("Nao foi possivel abrir a pagina da extensao.");
                AppendLog(ex.Message);
                MessageBox.Show(
                    this,
                    "Nao foi possivel abrir a pagina da extensao automaticamente.",
                    "Extensao de cookies",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static bool IsLegacyBrowserCookieSource(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.Trim().StartsWith("browser:", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddField(TableLayoutPanel panel, int row, string label, Control input)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 8, 8, 8)
            }, 0, row);

            panel.Controls.Add(input, 1, row);
        }

        private void ApplyDefaultValues()
        {
            if (string.IsNullOrWhiteSpace(_serverUrlTextBox.Text))
            {
                _serverUrlTextBox.Text = DefaultTelegramRtmpServer;
            }
        }
    }
}
