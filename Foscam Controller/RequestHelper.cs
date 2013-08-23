using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Threading;
using System.IO;

namespace Foscam_Controller
{
    public static class RequestHelper
    {
        /// <summary>
        /// A blocking operation that does not continue until a response has been
        /// received for a given <see cref="HttpWebRequest"/>, or the request
        /// timed out.
        /// </summary>
        /// <param name="request">The request to be sent.</param>
        /// <param name="timeout">An optional timeout.</param>
        /// <returns>The response that was received for the request.</returns>
        /// <exception cref="TimeoutException">If the <paramref name="timeout"/>
        /// parameter was set, and no response was received within the specified
        /// time.</exception>
        /// <remarks>You must not invoke this method on the UI thread, or the call will
        /// time out. This is because the <see cref="HttpWebRequest.EndGetResponse"/>
        /// method accesses the UI thread as well, which will starve the worker thread.</remarks>
        public static HttpWebResponse GetResponse(this HttpWebRequest request, int? timeout)
        {
            // Debug.WriteLine("GetResponse");
            if (request == null) throw new ArgumentNullException("request");

            if (System.Windows.Deployment.Current.Dispatcher.CheckAccess())
            {
                const string msg = "Invoking this method on the UI thread is forbidden. Use a worker thread.";
                throw new InvalidOperationException(msg);
            }

            AutoResetEvent waitHandle = new AutoResetEvent(false);
            HttpWebResponse response = null;
            Exception exception = null;

            AsyncCallback callback = ar =>
            {
                var req = ar.AsyncState as HttpWebRequest;
                try
                {
                    //get the response
                    response = (HttpWebResponse)req.EndGetResponse(ar);
                    //Debug.WriteLine("Got End GetResponse");
                }
                catch (Exception e)
                {
                    exception = e;
                }
                finally
                {
                    //setting the handle unblocks the loop below
                    waitHandle.Set();
                }
            };


            //request response async
            var asyncResult = request.BeginGetResponse(callback, request);
            if (asyncResult.CompletedSynchronously) return response;

            bool hasSignal = waitHandle.WaitOne(timeout ?? Timeout.Infinite);
            if (!hasSignal)
            {
                throw new TimeoutException("No response received in time.");
            }

            //bubble exception that occurred on worker thread
            if (exception != null) throw exception;

            return response;
        }



        /// <summary>
        /// Synchronously gets a request stream for a given request.
        /// </summary>
        /// <param name="request">The request to be sent.</param>
        /// <param name="timeout">An optional timeout.</param>
        /// <returns>The stream that was received for the request.</returns>
        /// <exception cref="TimeoutException">If the <paramref name="timeout"/>
        /// parameter was set, and no stream was received within the specified
        /// time.</exception>
        public static Stream GetRequestStream(this HttpWebRequest request, int? timeout)
        {
            // Debug.WriteLine("GetRequestStream");
            if (request == null) throw new ArgumentNullException("request");

            AutoResetEvent waitHandle = new AutoResetEvent(false);
            Stream requestStream = null;
            Exception exception = null;

            AsyncCallback callback = ar =>
            {
                //get the response
                var req = ar.AsyncState as HttpWebRequest;
                try
                {
                    requestStream = req.EndGetRequestStream(ar);
                    //Debug.WriteLine("Got Request Stream End");
                }
                catch (Exception e)
                {
                    exception = e;
                }
                finally
                {
                    //setting the handle unblocks the loop below
                    waitHandle.Set();
                }
            };

            //request stream async
            var asyncResult = request.BeginGetRequestStream(callback, request);
            if (asyncResult.CompletedSynchronously) return requestStream;

            bool hasSignal = waitHandle.WaitOne(timeout ?? Timeout.Infinite);
            if (!hasSignal)
            {
                throw new TimeoutException("No response received in time.");
            }

            //bubble exception that occurred on worker thread
            if (exception != null) throw exception;

            return requestStream;
        }
    }
}
