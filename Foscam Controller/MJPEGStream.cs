// AForge Video Library
// AForge.NET framework
// http://www.aforgenet.com/framework/
//
// Copyright © Andrew Kirillov, 2005-2010
// andrew.kirillov@aforgenet.com
//
// Updated by Ivan.Farkas@FL4SaleLive.com, 02/01/2010
// Fix related to AirLink cameras, which are not accurate with HTTP standard
//

namespace Foscam_Controller
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Net;
    using System.Diagnostics;

    public delegate void NewFrameEventHandler(object sender, NewFrameEventArgs eventArgs);
    public delegate void VideoSourceErrorEventHandler(object sender, VideoSourceErrorEventArgs eventArgs);
    public delegate void PlayingFinishedEventHandler(object sender, ReasonToFinishPlaying reason);
    public enum ReasonToFinishPlaying
    {
        EndOfStreamReached,
        StoppedByUser
    }

    public class NewFrameEventArgs : EventArgs
    {
        private byte[] frame;

        public NewFrameEventArgs(byte[] frame)
        {
            this.frame = frame;
        }

        public byte[] Frame
        {
            get { return frame; }
        }
    }

    public class VideoSourceErrorEventArgs : EventArgs
    {
        private string description;

        public VideoSourceErrorEventArgs(string description)
        {
            this.description = description;
        }

        public string Description
        {
            get { return description; }
        }
    }


    internal static class ByteArrayUtils
    {
        /// <summary>
        /// Check if the array contains needle at specified position.
        /// </summary>
        /// 
        /// <param name="array">Source array to check for needle.</param>
        /// <param name="needle">Needle we are searching for.</param>
        /// <param name="startIndex">Start index in source array.</param>
        /// 
        /// <returns>Returns <b>true</b> if the source array contains the needle at
        /// the specified index. Otherwise it returns <b>false</b>.</returns>
        /// 
        public static bool Compare(byte[] array, byte[] needle, int startIndex)
        {
            int needleLen = needle.Length;
            // compare
            for (int i = 0, p = startIndex; i < needleLen; i++, p++)
            {
                if (array[p] != needle[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Find subarray in the source array.
        /// </summary>
        /// 
        /// <param name="array">Source array to search for needle.</param>
        /// <param name="needle">Needle we are searching for.</param>
        /// <param name="startIndex">Start index in source array.</param>
        /// <param name="sourceLength">Number of bytes in source array, where the needle is searched for.</param>
        /// 
        /// <returns>Returns starting position of the needle if it was found or <b>-1</b> otherwise.</returns>
        /// 
        public static int Find(byte[] array, byte[] needle, int startIndex, int sourceLength)
        {
            int needleLen = needle.Length;
            int index;

            while (sourceLength >= needleLen)
            {
                // find needle's starting element
                index = Array.IndexOf(array, needle[0], startIndex, sourceLength - needleLen + 1);

                // if we did not find even the first element of the needls, then the search is failed
                if (index == -1)
                    return -1;

                int i, p;
                // check for needle
                for (i = 0, p = index; i < needleLen; i++, p++)
                {
                    if (array[p] != needle[i])
                    {
                        break;
                    }
                }

                if (i == needleLen)
                {
                    // needle was found
                    return index;
                }

                // continue to search for needle
                sourceLength -= (index - startIndex + 1);
                startIndex = index + 1;
            }
            return -1;
        }
    }


    /// <summary>
    /// MJPEG video source.
    /// </summary>
    /// 
    /// <remarks><para>The video source downloads JPEG images from the specified URL, which represents
    /// MJPEG stream.</para>
    /// 
    /// <para>Sample usage:</para>
    /// <code>
    /// // create MJPEG video source
    /// MJPEGStream stream = new MJPEGStream( "some url" );
    /// // set event handlers
    /// stream.NewFrame += new NewFrameEventHandler( video_NewFrame );
    /// // start the video source
    /// stream.Start( );
    /// // ...
    /// // signal to stop
    /// stream.SignalToStop( );
    /// </code>
    public class MJPEGStream
    {
        // URL for MJPEG stream
        private string source;
        // login and password for HTTP authentication
        private string login = null;
        private string password = null;
        // received frames count
        private int framesReceived;
        // recieved byte count
        private int bytesReceived;
        // use separate HTTP connection group or use default
        private bool useSeparateConnectionGroup = true;
        // timeout value for web request
        private int requestTimeout = 10000;

        // buffer size used to download MJPEG stream
        private const int bufSize = 512 * 1024;
        // size of portion to read at once
        private const int readSize = 1024;

        private Thread thread = null;
        private ManualResetEvent stopEvent = null;
        private ManualResetEvent reloadEvent = null;

        private string userAgent = "Mozilla/5.0";

        public event NewFrameEventHandler NewFrame;
        public event VideoSourceErrorEventHandler VideoSourceError;
        public event PlayingFinishedEventHandler PlayingFinished;

        public bool SeparateConnectionGroup
        {
            get { return useSeparateConnectionGroup; }
            set { useSeparateConnectionGroup = value; }
        }

        public string Source
        {
            get { return source; }
            set
            {
                source = value;
                // signal to reload
                if (thread != null)
                    reloadEvent.Set();
            }
        }

        /// <summary>
        /// Login value.
        /// </summary>
        /// 
        /// <remarks>Login required to access video source.</remarks>
        /// 
        public string Login
        {
            get { return login; }
            set { login = value; }
        }

        /// <summary>
        /// Password value.
        /// </summary>
        /// 
        /// <remarks>Password required to access video source.</remarks>
        /// 
        public string Password
        {
            get { return password; }
            set { password = value; }
        }

        public string HttpUserAgent
        {
            get { return userAgent; }
            set { userAgent = value; }
        }

        public int FramesReceived
        {
            get
            {
                int frames = framesReceived;
                framesReceived = 0;
                return frames;
            }
        }

        public int BytesReceived
        {
            get
            {
                int bytes = bytesReceived;
                bytesReceived = 0;
                return bytes;
            }
        }

        public bool IsRunning
        {
            get
            {
                if (thread != null)
                {
                    // check thread status
                    if (thread.Join(0) == false)
                        return true;

                    // the thread is not running, so free resources
                    Free();
                }
                return false;
            }
        }

        public MJPEGStream() { }
        public MJPEGStream(string source)
        {
            this.source = source;
        }

        public void Start()
        {
            if (!IsRunning)
            {
                // check source
                if ((source == null) || (source == string.Empty))
                    throw new ArgumentException("Video source is not specified.");

                framesReceived = 0;
                bytesReceived = 0;

                // create events
                stopEvent = new ManualResetEvent(false);
                reloadEvent = new ManualResetEvent(false);

                // create and start new thread
                //thread = new Thread(new ThreadStart(WorkerThread));
                //thread.Name = source;
                //thread.Start();
                

            }




            var request = (HttpWebRequest)HttpWebRequest.Create(source);


            request.BeginGetResponse((ar) =>
            {
                var req = ar.AsyncState as HttpWebRequest;

                try
                {
                    //get the response
                    var response = (HttpWebResponse)req.EndGetResponse(ar);

                    Debug.WriteLine("Response!");

                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Ex: " + ex);
                }
            }, request);





        }

        public void SignalToStop()
        {
            // stop thread
            if (thread != null)
            {
                // signal to stop
                stopEvent.Set();
            }
        }

        public void WaitForStop()
        {
            if (thread != null)
            {
                // wait for thread stop
                thread.Join();

                Free();
            }
        }

        public void Stop()
        {
            if (this.IsRunning)
            {
                stopEvent.Set();
                thread.Abort();
                WaitForStop();
            }
        }

        /// <summary>
        /// Free resource.
        /// </summary>
        /// 
        private void Free()
        {
            thread = null;

            // release events
            stopEvent.Close();
            stopEvent = null;
            reloadEvent.Close();
            reloadEvent = null;
        }

        // Worker thread
        private void WorkerThread()
        {
            // buffer to read stream
            byte[] buffer = new byte[bufSize];
            // JPEG magic number
            byte[] jpegMagic = new byte[] { 0xFF, 0xD8, 0xFF };
            int jpegMagicLength = 3;

            // while (true)
            {
                // reset reload event
                //reloadEvent.Reset();

                // HTTP web request
                HttpWebRequest request = null;
                // web responce
                WebResponse response = null;
                // stream for MJPEG downloading
                Stream stream = null;
                // boundary betweeen images
                byte[] boundary = null;
                // length of boundary
                int boundaryLen;
                // read amounts and positions
                int read, todo = 0, total = 0, pos = 0, align = 1;
                int start = 0, stop = 0;

                // align
                //  1 = searching for image start
                //  2 = searching for image end

                try
                {
                    // create request
                    request = (HttpWebRequest)HttpWebRequest.Create("http://www.google.com/");
                    // set user agent
                    if (userAgent != null)
                    {
                        //request.UserAgent = userAgent;
                    }
                    // set timeout value for the request
                    
                    // SLFAIL request.Timeout = requestTimeout;
                    // set login and password
                    /*
                    if ((login != null) && (password != null) && (login != string.Empty))
                        request.Credentials = new NetworkCredential(login, password);
                     */
                    // set connection group name
                    /* SLFAIL
                    if (useSeparateConnectionGroup)
                        request.ConnectionGroupName = GetHashCode().ToString();
                    */
                    // get response

                    request.BeginGetResponse((ar) =>
                    {
                        var req = ar.AsyncState as HttpWebRequest;

                        try
                        {
                            //get the response
                            response = (HttpWebResponse)req.EndGetResponse(ar);

                            Debug.WriteLine("Response!");

                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Ex: " + ex);
                        }
                    }, request);
                    return;

                    // check content type
                    string contentType = response.ContentType;
                    string[] contentTypeArray = contentType.Split('/');
                    if (!((contentTypeArray[0] == "multipart") && (contentType.Contains("mixed"))))
                    {
                        // provide information to clients
                        if (VideoSourceError != null)
                        {
                            VideoSourceError(this, new VideoSourceErrorEventArgs("Invalid content type"));
                        }

                        request.Abort();
                        request = null;
                        response.Close();
                        response = null;
                        /*
                        // need to stop ?
                        if (stopEvent.WaitOne(0, true))
                            break;
                        */
                        //continue;
                    }

                    // get boundary
                    UTF8Encoding encoding = new UTF8Encoding();
                    string boudaryStr = contentType.Substring(contentType.IndexOf("boundary=", 0) + 9);
                    boundary = encoding.GetBytes(boudaryStr);
                    boundaryLen = boundary.Length;
                    bool boundaryIsChecked = false;

                    // get response stream
                    stream = response.GetResponseStream();

                    // loop
                    while (true)
                    {
                        // check total read
                        if (total > bufSize - readSize)
                        {
                            total = pos = todo = 0;
                        }

                        // read next portion from stream
                        if ((read = stream.Read(buffer, total, readSize)) == 0)
                            throw new Exception("can't read??");

                        total += read;
                        todo += read;

                        // increment received bytes counter
                        bytesReceived += read;

                        // do we need to check boundary ?
                        if (!boundaryIsChecked)
                        {
                            // some IP cameras, like AirLink, claim that boundary is "myboundary",
                            // when it is really "--myboundary". this needs to be corrected.

                            pos = ByteArrayUtils.Find(buffer, boundary, 0, todo);
                            // continue reading if boudary was not found
                            if (pos == -1)
                                continue;

                            for (int i = pos - 1; i >= 0; i--)
                            {
                                byte ch = buffer[i];

                                if ((ch == (byte)'\n') || (ch == (byte)'\r'))
                                {
                                    break;
                                }

                                boudaryStr = (char)ch + boudaryStr;
                            }

                            boundary = encoding.GetBytes(boudaryStr);
                            boundaryLen = boundary.Length;
                            boundaryIsChecked = true;
                        }

                        // search for image start
                        if (align == 1)
                        {
                            start = ByteArrayUtils.Find(buffer, jpegMagic, pos, todo);
                            if (start != -1)
                            {
                                // found JPEG start
                                pos = start;
                                todo = total - pos;
                                align = 2;
                            }
                            else
                            {
                                // delimiter not found
                                todo = jpegMagicLength - 1;
                                pos = total - todo;
                            }
                        }

                        // search for image end
                        while ((align == 2) && (todo >= boundaryLen))
                        {
                            stop = ByteArrayUtils.Find(buffer, boundary, pos, todo);
                            if (stop != -1)
                            {
                                pos = stop;
                                todo = total - pos;

                                // increment frames counter
                                framesReceived++;

                                // image at stop
                                if ((NewFrame != null))
                                {
                                    var ms = new MemoryStream(buffer, start, stop - start);
                                    // notify client
                                    NewFrame(this, new NewFrameEventArgs(ms.ToArray()));
                                    // release the image
                                    //ms.Dispose();
                                    //ms = null;
                                }

                                // shift array
                                pos = stop + boundaryLen;
                                todo = total - pos;
                                Array.Copy(buffer, pos, buffer, 0, todo);

                                total = todo;
                                pos = 0;
                                align = 1;
                            }
                            else
                            {
                                // boundary not found
                                todo = boundaryLen - 1;
                                pos = total - todo;
                            }
                        }
                    }
                }
                catch (WebException exception)
                {
                    // provide information to clients
                    if (VideoSourceError != null)
                    {
                        VideoSourceError(this, new VideoSourceErrorEventArgs(exception.Message));
                    }
                    // wait for a while before the next try
                    Thread.Sleep(250);
                }
                catch (ArgumentOutOfRangeException)/// WTF TODO FIXME
                {
                    // wait for a while before the next try
                    Thread.Sleep(250);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
                finally
                {
                    // abort request
                    if (request != null)
                    {
                        request.Abort();
                        request = null;
                    }
                    // close response stream
                    if (stream != null)
                    {
                        stream.Close();
                        stream = null;
                    }
                    // close response
                    if (response != null)
                    {
                        response.Close();
                        response = null;
                    }
                }
                /*
                // need to stop ?
                if (stopEvent.WaitOne(0, true))
                    break;
                */
            }

            if (PlayingFinished != null)
            {
                PlayingFinished(this, ReasonToFinishPlaying.StoppedByUser);
            }
        }
    }
}
