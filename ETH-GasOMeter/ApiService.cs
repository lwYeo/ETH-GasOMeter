using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ETH_GasOMeter
{
    class ApiService : IDisposable
    {
        public delegate void MessageHandler(object sender, MessageArgs e);
        public event MessageHandler OnMessage;

        public delegate void APIResponseHandler(object sender, APIResponseArgs e);
        public event APIResponseHandler OnAPIResponse;

        private const string DefaultAPIPath = "127.0.0.1:1888";

        private bool _IsOngoing;
        private HttpListener _Listener;

        public ApiService()
        {
            if (!HttpListener.IsSupported) { throw new NotSupportedException("Obsolete Windows version detected, API will not start."); }
        }

        public void Start(string apiBind)
        {
            if (string.IsNullOrWhiteSpace(apiBind))
            {
                OnMessage.Invoke(this, new MessageArgs(string.Format("API-bind is null or empty, using default {0}", DefaultAPIPath)));
                apiBind = DefaultAPIPath;
            }

            if (!apiBind.StartsWith("http://") || apiBind.StartsWith("https://")) { apiBind = "http://" + apiBind; }

            if (!apiBind.EndsWith("/")) { apiBind += "/"; }

            try
            {
                _Listener = new HttpListener();

                _Listener.Prefixes.Add(apiBind);

                _IsOngoing = true;

                Task.Factory.StartNew(() => Process(_Listener));
            }
            catch (Exception)
            {
                _IsOngoing = false;
                throw new ArgumentException("An error has occured while starting API.");
            }
        }

        public void Stop()
        {
            if (_IsOngoing)
            {
                OnMessage?.Invoke(this, new MessageArgs("API service stopping..."));
                _IsOngoing = false;
                _Listener.Stop();
            }
        }

        private void Process(HttpListener listener)
        {
            listener.Start();
            OnMessage?.Invoke(this, new MessageArgs(string.Format("API service started at {0}...", listener.Prefixes.ElementAt(0))));
            while (_IsOngoing)
            {
                Task.Delay(500);
                var responseArgs = new APIResponseArgs();
                OnAPIResponse?.Invoke(this, responseArgs);
                byte[] buffer = Encoding.UTF8.GetBytes(responseArgs.Response);

                HttpListenerResponse response = listener.GetContext().Response;
                response.ContentLength64 = buffer.Length;

                Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
            }
        }

        public class APIResponseArgs : EventArgs
        {
            public APIResponseArgs() { }

            public string Response { get; set; }
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_Listener != null) { _Listener.Close(); }
                }

                _Listener = null;
                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ApiService() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion
    }
}
