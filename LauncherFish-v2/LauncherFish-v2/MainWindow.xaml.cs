using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Net.NetworkInformation;
using static System.Net.Mime.MediaTypeNames;

namespace LauncherFish_v2
{
    public sealed partial class MainWindow : Window
    {
        #region Start
        public MainWindow()
        {
            this.InitializeComponent();
            this.Activated += MainWindow_Activated; // Замена Loaded
            this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                (int)(Windows.System.DisplayInformation.GetForCurrentView().ScreenWidthInRawPixels / 2 - 400),
                (int)(Windows.System.DisplayInformation.GetForCurrentView().ScreenHeightInRawPixels / 2 - 300),
                800, 600)); // Центрирование окна

            // Настройка внешнего вида окна
            this.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            CheckInternetConnection();
        }
        #endregion

        #region Updater
        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            string versionUrl = "https://drive.google.com/uc?export=download&id=1EmYa-sZakv7oWdB2PORNr6KVECLPAhDi";
            string url = "https://drive.google.com/uc?export=download&id=1aXoeA6hxymwMc7ZiBG8ioiv8os9pVLzc";
            string localPath = "Release.zip";
            string currentVersion = "1.0";
            string newVersion = await GetNewVersionAsync(versionUrl);

            if (newVersion != currentVersion)
            {
                UpdateStatus("Скачивание...");
                await DownloadFileAsync(url, localPath);
                UpdateStatus("Установка...");
                await ExtractAndRun(localPath);
            }
            else
            {
                UpdateStatus("Запуск...");
                StartLauncher();
            }
        }

        private async Task<string> GetNewVersionAsync(string versionUrl)
        {
            using (var httpClient = new HttpClient())
            {
                return await httpClient.GetStringAsync(versionUrl);
            }
        }

        private async Task DownloadFileAsync(string url, string localPath)
        {
            using (var httpClient = new HttpClient())
            {
                using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var buffer = new byte[8192];

                    using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            long totalReadBytes = 0;
                            int readBytes;

                            while ((readBytes = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, readBytes);
                                totalReadBytes += readBytes;

                                await DispatcherQueue.TryEnqueueAsync(() =>
                                {
                                    if (totalBytes > 0)
                                    {
                                        DownloadProgressBar.Value = (totalReadBytes / (double)totalBytes) * 100;
                                    }
                                });
                            }
                        }
                    }
                }
            }
        }

        private void UpdateStatus(string status)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusText.Text = status;
            });
        }

        private async Task ExtractAndRun(string zipPath)
        {
            try
            {
                if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
                {
                    throw new FileNotFoundException("Файл архива не найден.", zipPath);
                }

                string extractPath = Path.Combine(Path.GetDirectoryName(zipPath) ?? "", "extracted");

                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }

                Directory.CreateDirectory(extractPath);

                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractPath));

                string newLauncherPath = Path.Combine(extractPath, "LaucnherFish.exe");
                if (!File.Exists(newLauncherPath))
                {
                    throw new FileNotFoundException("Новый лаунчер не найден.", newLauncherPath);
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(newLauncherPath)
                {
                    UseShellExecute = true
                });
                Application.Current.Quit();
            }
            catch (Exception ex)
            {
                await DispatcherQueue.TryEnqueueAsync(async () =>
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Ошибка",
                        Content = $"Ошибка при распаковке: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await dialog.ShowAsync();
                });
            }
        }

        private void StartLauncher()
        {
            string launcherPath = "LaucnherFish.exe";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(launcherPath)
            {
                UseShellExecute = true
            });
            Application.Current.Quit();
        }
        #endregion

        #region CheckInternet
        private void CheckInternetConnection()
        {
            bool isConnected = NetworkInterface.GetIsNetworkAvailable();

            DispatcherQueue.TryEnqueue(() =>
            {
                MyImage.Source = new BitmapImage(isConnected ?
                    new Uri("ms-appx:///img/connected.png") :
                    new Uri("ms-appx:///img/dontconnected.png"));
            });
        }
        #endregion
    }
}