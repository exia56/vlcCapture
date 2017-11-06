
using System;
using System.Collections.Generic;
using System.IO;
using Meta.Vlc;
using System.Windows.Media.Imaging;
//using WMPLib;

namespace testWMPGetVideoInfo
{
    class GrtVideoInfo
    {
        static VlcMediaPlayer mediaPlayer;
        static float position = 0.1f;
        static double Width, Height, duration;
        static bool waiting = true;
        static void Main(string[] args)
        {
            AppDomain app = AppDomain.CurrentDomain;
            app.UnhandledException += App_UnhandledException;
            //string filePath = @"file:///C:/dslib/media/1050/02369bc5-00d5-4924-a3b5-5c18bc43c449.mp4";

            //string filePath = @"http://downloads.4ksamples.com/downloads/SES.Astra.UHD.Test.1.2160p.UHDTV.AAC.HEVC.x265-LiebeIst.mkv";
            string filePath = @"http://mirrors.standaloneinstaller.com/video-sample/Panasonic_HDC_TM_700_P_50i.mp4";
            //string filePath = @"http://www.sample-videos.com/audio/mp3/wave.mp3";
            string file = "";
            if (args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            {
                file = args[0];
            }
            else
            {
                file = filePath;
            }
            string thumbnailPath = @"C:/dslib/media/anything.png";
            FileStream fs = new FileStream(thumbnailPath, FileMode.OpenOrCreate);
            VideoThumbnail vt = new VideoThumbnail(file);
            MemoryStream ms = new MemoryStream();
            var t = vt.Execute(fs);
            t.Wait();
            var v = t.Result;
            ms.Position = 0;
            ms.CopyTo(fs);
            Console.WriteLine($" Video Duration {v.Duration}");
            Console.WriteLine($" Video {v.Resolution}");
            Console.WriteLine($"Done Capture press any key to exit");
            Console.ReadKey();
        }

        private static void App_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.ExceptionObject.ToString());
        }
    }
}
