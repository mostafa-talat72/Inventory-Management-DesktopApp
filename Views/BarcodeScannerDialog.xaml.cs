using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using ZXing;
using ZXing.Common;

namespace ProductApp.Views;

public partial class BarcodeScannerDialog : UserControl
{
    public event EventHandler<string?>? ScanFinished;

    public string? ResultCode { get; private set; }

    private VideoCaptureDevice? _videoSource;
    private bool _decoding;
    private bool _closed;

    private static readonly BarcodeReaderGeneric BarcodeDecoder = new()
    {
        AutoRotate = true,
        TryInverted = true,
        Options = new DecodingOptions
        {
            TryHarder = true,
            PossibleFormats = new System.Collections.Generic.List<BarcodeFormat>
            {
                BarcodeFormat.EAN_13, BarcodeFormat.EAN_8, BarcodeFormat.UPC_A,
                BarcodeFormat.UPC_E, BarcodeFormat.CODE_128, BarcodeFormat.CODE_39,
                BarcodeFormat.CODE_93, BarcodeFormat.QR_CODE, BarcodeFormat.ITF
            }
        }
    };

    public BarcodeScannerDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) => StopCamera();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TryStartCamera();
    }

    private void TryStartCamera()
    {
        try
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (devices.Count == 0)
            {
                ShowNoCamera("لم يتم العثور على كاميرا - يمكنك إدخال الباركود يدوياً");
                return;
            }

            _videoSource = new VideoCaptureDevice(devices[0].MonikerString);
            _videoSource.NewFrame += VideoSource_NewFrame;
            _videoSource.VideoResolution = _videoSource.VideoCapabilities
                .OrderByDescending(c => c.FrameSize.Width * c.FrameSize.Height)
                .FirstOrDefault(c => c.FrameSize.Width <= 1024)
                ?? _videoSource.VideoCapabilities.FirstOrDefault();
            _videoSource.Start();

            TxtCameraStatus.Text = "وجّه المنتج نحو الكاميرا";
        }
        catch (System.Exception)
        {
            ShowNoCamera("تعذر تشغيل الكاميرا - يمكنك إدخال الباركود يدوياً");
        }
    }

    private void ShowNoCamera(string message)
    {
        TxtCameraStatus.Text = message;
        StatusBar.Visibility = Visibility.Visible;
        TxtStatus.Text = message;
    }

    private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
    {
        try
        {
            var frame = (Bitmap)eventArgs.Frame.Clone();
            Dispatcher.BeginInvoke(new Action(() => ShowPreview(frame)));

            if (_decoding || _closed) return;
            _decoding = true;
            try
            {
                var result = DecodeBitmap(frame);
                if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                {
                    _closed = true;
                    var code = result.Text.Trim();
                    ResultCode = code;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        System.Media.SystemSounds.Asterisk.Play();
                        ShowSuccess(code);
                    }));
                }
            }
            catch
            {
                // تجاهل الإطارات غير الصالحة
            }
            finally
            {
                _decoding = false;
                frame.Dispose();
            }
        }
        catch
        {
            // تجاهل أخطاء الإطارات
        }
    }

    private static Result? DecodeBitmap(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int width = bmp.Width;
            int height = bmp.Height;
            byte[] pixels = new byte[width * height * 3];
            for (int y = 0; y < height; y++)
                Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * width * 3, width * 3);
            return BarcodeDecoder.Decode(new RGBLuminanceSource(pixels, width, height));
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private void ShowPreview(Bitmap frame)
    {
        try
        {
            var hBitmap = frame.GetHbitmap();
            try
            {
                var src = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                CameraPreview.Source = src;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
        catch
        {
            // تجاهل أخطاء العرض
        }
        finally
        {
            frame.Dispose();
        }
    }

    private void ShowSuccess(string code)
    {
        CameraPreview.Source = null;
        TxtCameraStatus.Text = "";
        StatusBar.Visibility = Visibility.Visible;
        TxtStatus.Text = "تم المسح بنجاح: " + code;
        TxtStatus.Foreground = System.Windows.Media.Brushes.White;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Finish(code);
        };
        timer.Start();
    }

    private void Finish(string? code)
    {
        StopCamera();
        ScanFinished?.Invoke(this, code);
    }

    private void StopCamera()
    {
        try
        {
            if (_videoSource != null)
            {
                _videoSource.NewFrame -= VideoSource_NewFrame;
                if (_videoSource.IsRunning)
                    _videoSource.SignalToStop();
                _videoSource = null;
            }
        }
        catch
        {
            // تجاهل أخطاء الإيقاف
        }
    }

    private void BtnAccept_Click(object sender, RoutedEventArgs e)
    {
        var code = TxtManualCode.Text?.Trim() ?? "";
        if (code.Length == 0)
        {
            TxtStatus.Text = "الرجاء إدخال الباركود أولاً";
            StatusBar.Visibility = Visibility.Visible;
            return;
        }
        ResultCode = code;
        Finish(code);
    }

    private void TxtManualCode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            BtnAccept_Click(sender, new RoutedEventArgs());
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Finish(null);
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}