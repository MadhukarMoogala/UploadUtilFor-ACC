namespace UploadUtil
{
    using Autodesk.Forge;
    using Autodesk.Forge.Core;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the <see cref="OAuthHandler" />.
    /// </summary>
    internal class OAuthHandler
    {
        /// <summary>
        /// Defines the PORT.
        /// </summary>
        private static string PORT = Environment.GetEnvironmentVariable("PORT") ?? "3000";

        /// <summary>
        /// Defines the FORGE_CALLBACK.
        /// </summary>
        private static string FORGE_CALLBACK = Environment.GetEnvironmentVariable("FORGE_CALLBACK") ?? "http://localhost:" + PORT + "/oauth";

        /// <summary>
        /// Defines the _scope.
        /// </summary>
        private static Scope[] _scope = new Scope[] { Scope.DataRead, Scope.DataWrite };

        /// <summary>
        /// Defines the _threeLeggedApi.
        /// </summary>
        private static ThreeLeggedApi _threeLeggedApi = new ThreeLeggedApi();

        /// <summary>
        /// Defines the _httpListener.
        /// </summary>
        private static HttpListener _httpListener = null;

        /// <summary>
        /// The AccessTokenDelegate.
        /// </summary>
        /// <param name="bearer">The bearer<see cref="dynamic"/>.</param>
        public delegate void AccessTokenDelegate(dynamic bearer);

        /// <summary>
        /// Defines the config.
        /// </summary>
        private static ForgeConfiguration config;

        /// <summary>
        /// The Create.
        /// </summary>
        /// <param name="forgeConfiguration">The forgeConfiguration<see cref="ForgeConfiguration"/>.</param>
        /// <returns>The <see cref="OAuthHandler"/>.</returns>
        public static OAuthHandler Create(ForgeConfiguration forgeConfiguration)
        {
            config = forgeConfiguration;
            return new OAuthHandler();
        }

        /// <summary>
        /// The Invoke3LeggedOAuth.
        /// </summary>
        /// <param name="cb">The cb<see cref="AccessTokenDelegate"/>.</param>
        public void Invoke3LeggedOAuth(AccessTokenDelegate cb)
        {
            _3leggedAsync(cb);
        }

        /// <summary>
        /// Gets or sets the InternalToken.
        /// </summary>
        private static dynamic InternalToken { get; set; }

        /// <summary>
        /// Get the access token from Autodesk.
        /// </summary>
        /// <param name="scopes">The scopes<see cref="Scope[]"/>.</param>
        /// <returns>The <see cref="Task{dynamic}"/>.</returns>
        public static async Task<dynamic> Get2LeggedTokenAsync(Scope[] scopes)
        {
            TwoLeggedApi oauth = new TwoLeggedApi();
            string grantType = "client_credentials";
            dynamic bearer = await oauth.AuthenticateAsync(config.ClientId,
              config.ClientSecret,
              grantType,
              scopes);
            return bearer;
        }

        /// <summary>
        /// The GetInternalAsync.
        /// </summary>
        /// <returns>The <see cref="Task{dynamic}"/>.</returns>
        public static async Task<dynamic> GetInternalAsync()
        {
            if (InternalToken == null || InternalToken.ExpiresAt < DateTime.UtcNow)
            {
                InternalToken = await Get2LeggedTokenAsync(new Scope[] { Scope.BucketCreate,
                                                                        Scope.BucketRead,
                                                                        Scope.BucketDelete,
                                                                        Scope.DataRead,
                                                                        Scope.DataWrite,
                                                                        Scope.DataCreate,
                                                                        Scope.CodeAll });
                InternalToken.ExpiresAt = DateTime.UtcNow.AddSeconds(InternalToken.expires_in);
            }

            return InternalToken;
        }

        /// <summary>
        /// The GetChromeExe.
        /// </summary>
        /// <returns>The <see cref="string"/>.</returns>
        private static string GetChromeExe()
        {
            bool isWindows = System.Runtime.InteropServices.RuntimeInformation
                                               .IsOSPlatform(OSPlatform.Windows);
            if (!isWindows)
            {
                return null;
            }
            const string suffix = @"Google\Chrome\Application\chrome.exe";
            var prefixes = new List<string> { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) };
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (programFilesx86 != programFiles)
            {
                prefixes.Add(programFiles);
            }
            else
            {
                if (Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion", "ProgramW6432Dir", null) is string programFilesDirFromReg) prefixes.Add(programFilesDirFromReg);
            }

            prefixes.Add(programFilesx86);
            var path = prefixes.Distinct().Select(prefix => Path.Combine(prefix, suffix)).FirstOrDefault(File.Exists);
            return path;
        }

        /// <summary>
        /// The _3leggedAsync.
        /// </summary>
        /// <param name="cb">The cb<see cref="AccessTokenDelegate"/>.</param>
        internal static void _3leggedAsync(AccessTokenDelegate cb)
        {
            if (!HttpListener.IsSupported)
                return;// HttpListener is not supported on this platform. // Initialize our web listener
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(FORGE_CALLBACK.Replace("localhost", "+") + "/");
            try
            {
                _httpListener.Start();
                IAsyncResult result = _httpListener.BeginGetContext(_3leggedAsyncWaitForCode, cb);
                // Generate a URL page that asks for permissions for the specified scopes, and call our default web browser.
                string oauthUrl = _threeLeggedApi.Authorize(config.ClientId, oAuthConstants.CODE, FORGE_CALLBACK, _scope);
                var file = GetChromeExe();
                ProcessStartInfo startInfo = new ProcessStartInfo(file)
                {
                    WindowStyle = ProcessWindowStyle.Minimized,
                    ArgumentList = {
                        "/incognito",
                        $@"{oauthUrl}"
                    },
                };
                var p = Process.Start(startInfo);
                //await p.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// The _3leggedAsyncWaitForCode.
        /// </summary>
        /// <param name="ar">The ar<see cref="IAsyncResult"/>.</param>
        internal static async void _3leggedAsyncWaitForCode(IAsyncResult ar)
        {
            try
            {
                // Our local web listener was called back from the Autodesk oAuth server
                // That means the user logged properly and granted our application access
                // for the requested scope.
                // Let's grab the code from the URL and request or final access_token

                //HttpListener listener =(HttpListener)result.AsyncState ;
                var context = _httpListener.EndGetContext(ar);
                string code = context.Request.QueryString[oAuthConstants.CODE];

                // The code is only to tell the user, he can close is web browser and return
                // to this application.
                var responseString = "<html><body>You can now close this window!</body></html>";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                var response = context.Response;
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                response.StatusCode = 200;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
                // Now request the final access_token
                if (!string.IsNullOrEmpty(code))
                {
                    // Call the asynchronous version of the 3-legged client with HTTP information
                    // HTTP information will help you to verify if the call was successful as well
                    // as read the HTTP transaction headers.
                    Autodesk.Forge.Client.ApiResponse<dynamic> bearer = await _threeLeggedApi.GettokenAsyncWithHttpInfo(config.ClientId,
                                                                                            config.ClientSecret,
                                                                                            oAuthConstants.AUTHORIZATION_CODE,
                                                                                           code, FORGE_CALLBACK);
                    
                   
                    //if ( bearer.StatusCode != 200 )
                    //	throw new Exception ("Request failed! (with HTTP response " + bearer.StatusCode + ")") ;

                    // The JSON response from the oAuth server is the Data variable and has been
                    // already parsed into a DynamicDictionary object.

                    //string token =bearer.Data.token_type + " " + bearer.Data.access_token ;
                    //DateTime dt =DateTime.Now ;
                    //dt.AddSeconds (double.Parse (bearer.Data.expires_in.ToString ())) ;

                    ((AccessTokenDelegate)ar.AsyncState)?.Invoke(bearer.Data);
                }
                else
                {
                    ((AccessTokenDelegate)ar.AsyncState)?.Invoke(null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                ((AccessTokenDelegate)ar.AsyncState)?.Invoke(null);
            }
            finally
            {
                _httpListener.Stop();
            }
        }

        internal async Task<dynamic> GetRefreshedTokenAsync(string refreshToken)
        {
            var scopes = new Scope[] {
                                    Scope.DataRead,
                                    Scope.DataWrite,
                                    Scope.DataCreate,
                                    };
           dynamic bearer = await  _threeLeggedApi.RefreshtokenAsync(config.ClientId, config.ClientSecret, "refresh_token", refreshToken, scopes);
           return bearer;
        }
    }
}
