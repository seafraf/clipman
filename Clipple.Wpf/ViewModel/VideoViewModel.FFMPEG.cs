﻿using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Clipple.ViewModel
{
    public partial class VideoViewModel
    {
        private const int DOWNSCALE_HEIGHT = 480;

        #region Methods
        protected unsafe void InitialiseFFMPEG()
        {
            // Use ffmpeg to try and determine FPS and video resolution
            AVFormatContext* formatContext = null;
            try
            {
                formatContext = CheckNull(ffmpeg.avformat_alloc_context(), 
                    "ffmpeg couldn't allocate context");

                CheckCode(ffmpeg.avformat_open_input(&formatContext, fileInfo.FullName, null, null),
                    $"ffmpeg couldn't open {fileInfo.FullName}");

                // Load stream information
                CheckCode(ffmpeg.avformat_find_stream_info(formatContext, null),
                    $"couldn't load stream info");

                // Try to find the best video stream
                var streamIndex = CheckCode(ffmpeg.av_find_best_stream(formatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0),
                    "couldn't find best stream");

                var stream = formatContext->streams[streamIndex];

                // Meta data
                VideoFPS      = (int)Math.Round(ffmpeg.av_q2d(ffmpeg.av_guess_frame_rate(formatContext, stream, null)));
                VideoWidth    = stream->codecpar->width;
                VideoHeight   = stream->codecpar->height;
                VideoDuration = TimeSpan.FromSeconds(stream->duration * ffmpeg.av_q2d(stream->time_base));

                // Setup decoder
                var codec = CheckNull(ffmpeg.avcodec_find_decoder(stream->codecpar->codec_id), 
                    "couldn't find decoder codec");

                var codecContext = CheckNull(ffmpeg.avcodec_alloc_context3(codec),
                    "couldn't allocate decoder codec context");

                CheckCode(ffmpeg.avcodec_parameters_to_context(codecContext, stream->codecpar), 
                    "couldn't copy codec params from stream");

                CheckCode(ffmpeg.avcodec_open2(codecContext, codec, null),
                    "couldn#t open codec");

                var frame  = CheckNull(ffmpeg.av_frame_alloc(), "alloc failure");
                var packet = CheckNull(ffmpeg.av_packet_alloc(), "alloc failure");

                while (ffmpeg.av_read_frame(formatContext, packet) >= 0)
                {
                    if (packet->stream_index == streamIndex)
                    {
                        CheckCode(ffmpeg.avcodec_send_packet(codecContext, packet),
                            "couldn't send packet to decoder");

                        CheckCode(ffmpeg.avcodec_receive_frame(codecContext, frame),
                            "decoder did not provide frame");

                        var scaledFrame = CheckNull(ffmpeg.av_frame_alloc(), "alloc failure");
                        var scaleFactor = DOWNSCALE_HEIGHT / (double)frame->height;

                        scaledFrame->format = (int)AVPixelFormat.AV_PIX_FMT_BGR24;
                        scaledFrame->width  = (int)(stream->codecpar->width * scaleFactor);
                        scaledFrame->height = DOWNSCALE_HEIGHT;

                        CheckCode(ffmpeg.av_frame_get_buffer(scaledFrame, 0), "alloc failure");

                        var swsContext = ffmpeg.sws_getContext(codecContext->width, codecContext->height, codecContext->pix_fmt,
                            scaledFrame->width, scaledFrame->height, (AVPixelFormat)scaledFrame->format, ffmpeg.SWS_BICUBIC, null, null, null);
                        CheckNull(swsContext, "couldn't create scale context");

                        // Scale frame down (or up, but in most cases the requested thumbnail size will be smaller than the input video size)
                        CheckCode(ffmpeg.sws_scale(swsContext, frame->data, frame->linesize, 0, frame->height, scaledFrame->data, scaledFrame->linesize), 
                            "couldn't scale image");

                        // Write bitmap data to a memory stream
                        using (var bitmapStream = new MemoryStream())
                        {
                            var bitmap = new Bitmap(scaledFrame->width, scaledFrame->height, scaledFrame->linesize[0], PixelFormat.Format24bppRgb, (IntPtr)scaledFrame->data[0]);
                            bitmap.Save(bitmapStream, ImageFormat.Bmp);
                            bitmapStream.Position = 0;

                            // Source bitmap image with the formatted bitmap data
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.StreamSource = bitmapStream;
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.EndInit();

                            // Use bitmap image as the thumbnail
                            Thumbnail = image;
                        }

                        break;
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (formatContext != null)
                    ffmpeg.avformat_close_input(&formatContext);
            }
        }

        private unsafe int CheckCode(int code, string error)
        {
            if (code < 0)
            {
                var bufferSize = 1024;
                var buffer = stackalloc byte[bufferSize];
                ffmpeg.av_strerror(code, buffer, (ulong)bufferSize);

                throw new InvalidOperationException($"{error}: {Marshal.PtrToStringAnsi((IntPtr)buffer)}");
            }

            return code;
        }

        private unsafe T* CheckNull<T>(T* nullable, string error) where T: unmanaged
        {
            if (nullable == null)
                throw new InvalidOperationException(error);

            return nullable;
        }
        #endregion

        #region Properties

        private ImageSource thumbnail;
        [JsonIgnore]
        public ImageSource Thumbnail
        {
            get => thumbnail;
            set => SetProperty(ref thumbnail, value);
        }
        #endregion
    }
}
