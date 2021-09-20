namespace UploadUtil
{
    using Autodesk.Forge.Core;
    using Autodesk.Forge.DesignAutomation;
    using Autodesk.Forge.DesignAutomation.Model;
    using Das.WorkItemSigner;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Serilog;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the <see cref="Specifications" />.
    /// </summary>
    internal static class Specifications
    {
        /// <summary>
        /// Defines the OwnerName.
        /// </summary>
        public const string OwnerName = "dasmad";

        /// <summary>
        /// Defines the ActivityName.
        /// </summary>
        public const string ActivityName = "PlotToPDF";

        /// <summary>
        /// Defines the Alias.
        /// </summary>
        public const string Alias = "prod";

        /// <summary>
        /// Defines the TargetEngine.
        /// </summary>
        public const string TargetEngine = "Autodesk.AutoCAD+24";

        /// <summary>
        /// Defines the FQActivityId.
        /// </summary>
        public const string FQActivityId = "AutoCAD.PlotToPDF+prod";

        /// <summary>
        /// Defines the PROJECTID.
        /// </summary>
        public const string PROJECTID = "b.adeb4f3b-1ee0-4fca-bb80-30934ae15668";

        /// <summary>
        /// Defines the FOLDERID.
        /// </summary>
        public const string FOLDERID = "urn:adsk.wipprod:fs.folder:co.SbeD5ppRRQ-SiqRIbSPA0g";

        /// <summary>
        /// Defines the FILENAME.
        /// </summary>
        public const string FILENAME = "blocks_and_tables_imperial.pdf";
    }

    /// <summary>
    /// Defines the <see cref="WSResponse" />.
    /// </summary>
    public class WSResponse
    {
        /// <summary>
        /// Gets or sets the Action.
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Gets or sets the Data.
        /// </summary>
        public WorkItemStatus Data { get; set; }
    }

    /// <summary>
    /// Defines the <see cref="Program" />.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Defines the wsresp.
        /// </summary>
        private static WSResponse wsresp;

        /// <summary>
        /// Defines the Api.
        /// </summary>
        private static DesignAutomationClient Api;

        /// <summary>
        /// Defines the ws.
        /// </summary>
        private static ClientWebSocket ws = null;

        /// <summary>
        /// The GetOwnerAsync.
        /// </summary>
        /// <param name="clientId">The clientId<see cref="string"/>.</param>
        /// <returns>The <see cref="Task{(string owner, string token)}"/>.</returns>
        private static async Task<(string owner, string token)> GetOwnerAsync(string clientId)
        {
            Console.WriteLine("Setting up owner...");
            var resp = await Api.ForgeAppsApi.GetNicknameAsync("me");
            if (resp.Content == clientId)
            {
                Console.WriteLine("\tNo nickname for this clientId yet. Attempting to create one...");
                HttpResponseMessage response;
                response = await Api.ForgeAppsApi.CreateNicknameAsync("me",
                                                                    new NicknameRecord()
                                                                    {
                                                                        Nickname = Specifications.OwnerName
                                                                    },
                                                                    throwOnError: false);
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    Console.WriteLine("\tThere are already resources associated with this clientId or nickname is in use. " +
                        "Please use a different clientId or nickname.");
                    return (null, null);
                }
                await response.EnsureSuccessStatusCodeAsync();
                var nickName = await response.Content.ReadAsStringAsync();
                return (nickName, response.RequestMessage.Headers.Authorization.ToString());
            }
            return (resp.Content, resp.HttpResponse.RequestMessage.Headers.Authorization.ToString());
        }

        /// <summary>
        /// The DownloadToDocsAsync.
        /// </summary>
        /// <param name="url">The url<see cref="string"/>.</param>
        /// <param name="localFile">The localFile<see cref="string"/>.</param>
        /// <returns>The <see cref="Task{string}"/>.</returns>
        private static async Task<string> DownloadToDocsAsync(string url, string localFile)
        {
            var fname = Path.Combine(Environment.CurrentDirectory, localFile);
            using (var client = new HttpClient())
            {
                var content = (await client.GetAsync(url)).Content;
                using var output = System.IO.File.Create(fname);
                (await content.ReadAsStreamAsync()).CopyTo(output);
                output.Close();
            }
            return fname;
        }

        /// <summary>
        /// The Receiving.
        /// </summary>
        /// <returns>The <see cref="Task"/>.</returns>
        private static async Task Receiving()
        {
            var buffer = new byte[4096];
            var shouldExit = false;
            while (!shouldExit)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var wsRes = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    wsresp = JsonConvert.DeserializeObject<WSResponse>(wsRes);
                    if (wsresp.Action.Equals("error"))
                    {
                        Console.WriteLine(wsRes);
                        break;
                    }
                    if (wsresp.Action.Equals("status"))
                    {

                        Console.WriteLine($"\t{wsresp.Data.Status}");
                        switch (wsresp.Data.Status)
                        {
                            case Status.Cancelled:
                            case Status.FailedDownload:
                            case Status.FailedInstructions:
                            case Status.FailedUpload:
                                {
                                    var fname = await DownloadToDocsAsync(wsresp.Data.ReportUrl,
                                                       $"err_report{DateTime.Now.Ticks}.log");
                                    Console.WriteLine($"\t\tReport Downloaded {fname}");
                                    shouldExit = true;
                                    break;

                                }
                            case Status.Success:
                                {
                                    var fname = await DownloadToDocsAsync(wsresp.Data.ReportUrl,
                                                       $"ok_report{DateTime.Now.Ticks}.log");

                                    Console.WriteLine($"\t\tReport Downloaded {fname}");
                                    shouldExit = true;

                                    //Create New Version of file


                                    break;
                                }
                        }


                    }

                }
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ok", CancellationToken.None);
                    break;
                }

            }
        }

        /// <summary>
        /// The Main.
        /// </summary>
        /// <param name="args">The args<see cref="string[]"/>.</param>
        /// <returns>The <see cref="Task"/>.</returns>
        internal static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            //Step 1: Create Configuration
            /**
             * SET FORGE_CLIENT_ID=<>
             * SET FORGE_CLIENT_SECRET=<>
             */
            var config = new ConfigurationBuilder()
                .AddForgeAlternativeEnvironmentVariables()
                .AddJsonFile("appsettings.user.json", false, true)
                .Build();
            var forgeClientId = config.GetValue<String>("Forge:ClientId");
            var forgeClientSecret = config.GetValue<String>("Forge:ClientSecret");

            var logger = new LoggerConfiguration()
           .WriteTo.Console()
           .CreateLogger();
            //Step 2: Populate Forge Design Automation service,
            //get Design Automation API Client
            Api = new ServiceCollection()
                .AddLogging(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Information);
                    builder.AddSerilog(logger, dispose: true);

                })
                .AddDesignAutomation(config)                
                .Services
                .BuildServiceProvider()
                .GetRequiredService<DesignAutomationClient>();

            //Step 3: Create Owner
            var (owner, bearerToken) = await GetOwnerAsync(forgeClientId);
            if (bearerToken == null)
            {
                return;
            }


            BucketHandler bucketHandler = new BucketHandler(new ForgeConfiguration()
            {
                ClientId = forgeClientId,
                ClientSecret = forgeClientSecret
            });


            //Step 4: Create or Update Activity
            //await SetupActivityAsync();


            //Step 5: Generating the public signature
            var signer = Signer.Create();
            var publicKeyJson = signer.ToJson(false);


            //Step 6: Uploading public sign your app.
            PublicKey publicKey = JsonConvert.DeserializeObject<PublicKey>(publicKeyJson);
            var nickNameRecord = new NicknameRecord
            {
                PublicKey = publicKey,
                Nickname = Specifications.OwnerName
            };
            await Api.CreateNicknameAsync("me", nickNameRecord);



            //Step 7 Generate digital signature for the activityId

            var signature = new WorkItemSignatures()
            {
                ActivityId = signer.Sign(Specifications.FQActivityId)
            };

            //Step 8: 

            var (UploadArgument, objectId) = await bucketHandler.GetBIM360UploadUrlAndObjectIdAsync();

            var a = new Dictionary<string, IArgument>
                {
                    {
                     "HostDwg", new XrefTreeArgument
                        {
                            Url = "http://download.autodesk.com/us/samplefiles/acad/blocks_and_tables_-_imperial.dwg"
                        }
                    },
                    { "Result", UploadArgument}
                };
            var workItem = new WorkItem
            {
                ActivityId = Specifications.FQActivityId,
                Arguments = a,
                Signatures = signature

            };
            Console.WriteLine("Workitem Payload:");
            Console.WriteLine($"\n\t{JsonConvert.SerializeObject(workItem, Formatting.Indented)}");

            var buffer = new byte[4096];
            using (ws = new ClientWebSocket())
            {
                await ws.ConnectAsync(new Uri("wss://websockets.forgedesignautomation.io"), CancellationToken.None);

                JObject wsClientData = new JObject(new JProperty("action", "post-workitem"),
                           new JProperty("data", JObject.FromObject(workItem)),
                           new JProperty("headers",
                           new JObject(new JProperty("Authorization", $"{bearerToken}"))));
                var data = JsonConvert.SerializeObject(wsClientData, Formatting.Indented);
                await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(data)), WebSocketMessageType.Text, true, CancellationToken.None);
                //receiving loop
                await Receiving();
                //close
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                Console.WriteLine("\tDisconnected...");
            }

            if (wsresp.Data.Status == Status.Success)
            {
                var fileId = await bucketHandler.CreateVersionFileAsync(objectId);
                Console.WriteLine($"\tSuccessfully Versioned : {fileId}");
            }
        }
    }
}
