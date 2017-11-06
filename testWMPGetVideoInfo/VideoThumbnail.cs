using Meta.Vlc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace testWMPGetVideoInfo
{
    public class VideoThumbnail
    {
        private string FilePath;
        VideoContext Context;
        VlcMediaPlayer mediaPlayer;
        float position = 0.1f;
        bool waiting = true;
        VideoAudioInfo _VideoAudioInfo;
        VideoResolution _VideoResolution;
        Stream ThumbnailStream;
        public VideoThumbnail(string filePath)
        {
            FilePath = filePath;
            _VideoAudioInfo = new VideoAudioInfo();
            _VideoResolution = new VideoResolution();
        }

        public async Task<VideoAudioInfo> Execute(Stream thumbnailStream)
        {
            ThumbnailStream = thumbnailStream;
            LibVlcManager.LoadLibVlc(AppDomain.CurrentDomain.BaseDirectory + @"..\..\libvlc");
            Vlc vlc = new Vlc(new[] { "-I", "dummy", "--ignore-config", "--no-video-title" });
            Console.WriteLine($"LibVlcManager: {LibVlcManager.GetVersion()}s");
            mediaPlayer = vlc.CreateMediaPlayer();
            mediaPlayer.SetVideoDecodeCallback(LockCB, unLockCB, DisplayCB, IntPtr.Zero);
            mediaPlayer.SetVideoFormatCallback(VideoFormatCallback, VideoCleanupCallback);
            mediaPlayer.Playing += MediaPlayer_Playing;
            mediaPlayer.Media = vlc.CreateMediaFromLocation(FilePath);

            await Task.Run(() =>
            {
                mediaPlayer.Play();
                mediaPlayer.IsMute = true;
                Stopwatch s = new Stopwatch();
                s.Start();
                while (waiting)
                {
                    Thread.Sleep(100);
                }
                s.Stop();
                TimeSpan sec = new TimeSpan(s.ElapsedTicks);
                Console.WriteLine(sec.ToString());
                mediaPlayer.Dispose();
                vlc.Dispose();
            });

            _VideoAudioInfo.Resolution = JsonConvert.SerializeObject(_VideoResolution);
            //_VideoAudioInfo.Thumbnail = ThumbnailStream;
            return _VideoAudioInfo;
        }

        private void MediaPlayer_Playing(object sender, ObjectEventArgs<Meta.Vlc.Interop.Media.MediaState> e)
        {
            if (e.Value == Meta.Vlc.Interop.Media.MediaState.Playing)
            {
                if (mediaPlayer.VideoTrackCount <= 0)
                {
                    _VideoAudioInfo.Duration = mediaPlayer.Media.Duration.TotalSeconds;
                    waiting = false;
                }
            }
        }

        void DisplayCB(IntPtr opaque, IntPtr picture)
        {
            Context.Display();
            if (mediaPlayer.Position > position)
            {
                //Context.Image.Dispatcher.BeginInvoke(new Action(() =>
                //{
                BitmapSource bmp = Context.Image;
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                //using (var ms = ThumbnailStream)
                //{
                ThumbnailStream.Position = 0;
                encoder.Save(ThumbnailStream);
                //}
                _VideoAudioInfo.Duration = mediaPlayer.Length.TotalSeconds;
                bmp = null;
                encoder = null;
                waiting = false;
                mediaPlayer.Stop();
                //}));
            }
        }
        IntPtr LockCB(IntPtr opaque, ref IntPtr planes)
        {
            return planes = Context.MapView;
        }
        void unLockCB(IntPtr opaque, IntPtr picture, ref IntPtr planes)
        {

        }
        uint VideoFormatCallback(ref IntPtr opaque, ref uint chroma, ref uint width, ref uint height,
            ref uint pitches, ref uint lines)
        {
            if (mediaPlayer.Position < position)
                mediaPlayer.Position = position;
            Console.WriteLine(String.Format("Initialize Video Content : {0}x{1}, Chroma: {2:x}", width, height, chroma));
            uint thumbnailMax = 200, thumbnailHeight, thumbnailWidth;
            _VideoResolution.Width = Convert.ToString(width);
            _VideoResolution.Height = Convert.ToString(height);
            if (height > width)
            {
                thumbnailHeight = thumbnailMax;
                thumbnailWidth = (uint)((double)height / (double)width * (double)thumbnailMax);
            }
            else
            {
                thumbnailWidth = thumbnailMax;
                thumbnailHeight = (uint)((double)height / (double)width * (double)thumbnailMax);
            }
            //thumbnailHeight = height;
            //thumbnailWidth = width;
            if (Context == null || Context.Width != width || Context.Height != height)
                Context = new VideoContext(thumbnailWidth, thumbnailHeight);

            Context.IsAspectRatioChecked = false;
            chroma = ('R' << 00) | ('V' << 08) | ('3' << 16) | ('2' << 24);
            width = (uint)Context.Width;
            height = (uint)Context.Height;
            pitches = (uint)Context.Stride;
            lines = (uint)Context.Height;
            return (uint)Context.Size;
        }
        void VideoCleanupCallback(IntPtr opaque)
        {
        }


        class VideoContext
        {
            #region --- Fields ---

            private bool _disposed;
            private object _imageLock = new object();

            #endregion --- Fields ---

            #region --- Initialization ---

            public VideoContext(uint width, uint height)
                : this((int)width, (int)height)
            {
            }

            public VideoContext(double width, double height)
                : this((int)width, (int)height)
            {
            }

            public VideoContext(int width, int height)
            {
                PixelFormat = PixelFormats.Bgr32;
                IsAspectRatioChecked = false;
                Size = width * height * PixelFormat.BitsPerPixel / 8;
                DisplayWidth = Width = width;
                DisplayHeight = Height = height;
                Stride = width * PixelFormat.BitsPerPixel / 8;
                FileMapping = Win32Api.CreateFileMapping(new IntPtr(-1), IntPtr.Zero, PageAccess.ReadWrite, 0, Size, null);
                MapView = Win32Api.MapViewOfFile(FileMapping, FileMapAccess.AllAccess, 0, 0, (uint)Size);
                Image =
                    (InteropBitmap)
                        Imaging.CreateBitmapSourceFromMemorySection(FileMapping, Width, Height, PixelFormat, Stride, 0);
            }

            #endregion --- Initialization ---

            #region --- Cleanup ---

            public void Dispose(bool disposing)
            {
                if (_disposed) return;
                Size = 0;
                PixelFormat = PixelFormats.Default;
                Stride = 0;
                var generation = GC.GetGeneration(Image);
                Image = null;
                Win32Api.UnmapViewOfFile(MapView);
                Win32Api.CloseHandle(FileMapping);
                FileMapping = MapView = IntPtr.Zero;
                GC.Collect(generation);
                _disposed = true;
            }

            public void Dispose()
            {
                Dispose(true);
            }

            #endregion --- Cleanup ---

            #region --- Properties ---

            public int Size { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }
            public double DisplayWidth { get; private set; }
            public double DisplayHeight { get; private set; }
            public int Stride { get; private set; }
            public PixelFormat PixelFormat { get; private set; }
            public IntPtr FileMapping { get; private set; }
            public IntPtr MapView { get; private set; }
            public InteropBitmap Image { get; private set; }
            public bool IsAspectRatioChecked { get; set; }

            #endregion --- Properties ---

            #region --- Methods ---

            public void Display()
            {
                //Image.Dispatcher.BeginInvoke(new Action(() =>
                //{
                //    if (Image != null)
                //    {
                Image.Invalidate();
                //    }
                //}));
            }
            #endregion
        }

        public class VideoAudioInfo
        {
            public double Duration { get; set; }
            public string Resolution { get; set; }
            //public Stream Thumbnail { get; set; }
        }

        public class VideoResolution
        {
            public string Width { get; set; }
            public string Height { get; set; }
        }

    }
}