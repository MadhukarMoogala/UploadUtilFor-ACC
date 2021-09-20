namespace UploadUtil
{
    using Autodesk.Forge;
    using Autodesk.Forge.Client;
    using Autodesk.Forge.Core;
    using Autodesk.Forge.DesignAutomation.Model;
    using Autodesk.Forge.Model;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Linq;

    /// <summary>
    /// Defines the <see cref="BucketHandler" />.
    /// </summary>
    internal class BucketHandler
    {
        /// <summary>
        /// Defines the config.
        /// </summary>
        private static ForgeConfiguration config;

        /// <summary>
        /// Defines the BucketKey.
        /// </summary>
        private const string BucketKey = "dwguploads";

        /// <summary>
        /// Initializes a new instance of the <see cref="BucketHandler"/> class.
        /// </summary>
        /// <param name="forgeConfiguration">The forgeConfiguration<see cref="ForgeConfiguration"/>.</param>
        public BucketHandler(ForgeConfiguration forgeConfiguration)
        {
            config = forgeConfiguration;
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
        /// The OSSManager.
        /// </summary>
        /// <param name="inputFilePath">The inputFilePath<see cref="string"/>.</param>
        /// <param name="resultFileName">The resultFileName<see cref="string"/>.</param>
        /// <returns>The <see cref="Task{(XrefTreeArgument downloadUrl, XrefTreeArgument uploadUrl)}"/>.</returns>
        public static async Task<(XrefTreeArgument downloadUrl, XrefTreeArgument uploadUrl)> OSSManager(string inputFilePath, string resultFileName)
        {
            dynamic oauth = await GetInternalAsync();
            string uploadUrl = string.Empty;
            string downloadUrl = string.Empty;
            // 1. ensure bucket exists

            BucketsApi buckets = new BucketsApi();
            buckets.Configuration.AccessToken = oauth.access_token;
            try
            {
                PostBucketsPayload bucketPayload = new(BucketKey, null, PostBucketsPayload.PolicyKeyEnum.Transient);
                dynamic bucketsRes = await buckets.CreateBucketAsync(bucketPayload, "US");
            }
            catch
            {
                // in case bucket already exists
                Console.WriteLine($"\tBucket {BucketKey} exists");
            };
            ObjectsApi objectsApi = new ObjectsApi();
            objectsApi.Configuration.AccessToken = oauth.access_token;
            var fi = new FileInfo(inputFilePath);
            var fileName = fi.Name;
            long fileSize = fi.Length;

            dynamic objectResp = await objectsApi.UploadObjectAsync(BucketKey, fileName, (int)fileSize, new FileStream(inputFilePath, FileMode.Open));


            try
            {
                PostBucketsSigned bucketsSigned = new PostBucketsSigned(60);
                dynamic signedResp = await objectsApi.CreateSignedResourceAsync(BucketKey, fileName, bucketsSigned, "read");
                downloadUrl = signedResp.signedUrl;
                signedResp = await objectsApi.CreateSignedResourceAsync(BucketKey, resultFileName, bucketsSigned, "readwrite");
                uploadUrl = signedResp.signedUrl;
                Console.WriteLine($"\tSuccess: signed resource for input.dwg created!\n\t{downloadUrl}");
                Console.WriteLine($"\tSuccess: signed resource for result.pdf created!\n\t{uploadUrl}");
            }
            catch { }
            return (
            new XrefTreeArgument()
            {
                Url = downloadUrl,
                Verb = Verb.Get,

            },
            new XrefTreeArgument()
            {
                Url = uploadUrl,
                Verb = Verb.Post,
            });
        }

        /// <summary>
        /// The GetBIM360UploadObjectId.
        /// </summary>
        /// <param name="oAuth">The oAuth<see cref="dynamic"/>.</param>
        /// <returns>The <see cref="Task{string}"/>.</returns>
        public async Task<string> GetBIM360UploadObjectId(dynamic oAuth)
        {

            string uploadUrl = string.Empty;

            //Create Storage.
            var projectsApi = new ProjectsApi { Configuration = { AccessToken = oAuth.access_token } };
            //1. We know our Hub, Project, and FolderId upfront.
            StorageRelationshipsTargetData storageRelData =
                    new StorageRelationshipsTargetData(StorageRelationshipsTargetData.TypeEnum.Folders, Specifications.FOLDERID);
            CreateStorageDataRelationshipsTarget storageTarget =
                new CreateStorageDataRelationshipsTarget(storageRelData);
            CreateStorageDataRelationships storageRel = new CreateStorageDataRelationships(storageTarget);
            BaseAttributesExtensionObject attributes =
                new BaseAttributesExtensionObject(string.Empty, string.Empty, new JsonApiLink(string.Empty), null);
            CreateStorageDataAttributes storageAtt = new CreateStorageDataAttributes(Specifications.FILENAME, attributes);
            CreateStorageData storageData =
                new CreateStorageData(CreateStorageData.TypeEnum.Objects, storageAtt, storageRel);
            CreateStorage storage =
                new CreateStorage(new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0), storageData);



            dynamic postStorageAsync = await projectsApi.PostStorageAsync(Specifications.PROJECTID, storage);
            try
            {
                string id = postStorageAsync.data.id;
                return id;
            }
            catch (ApiException e)
            {
                throw e.InnerException;
            }
        }

        /// <summary>
        /// The GetBIM360UploadUrlAndObjectIdAsync.
        /// </summary>
        /// <returns>The <see cref="Task{(XrefTreeArgument uploadUrl,string objectId)}"/>.</returns>
        public async Task<(XrefTreeArgument uploadUrl, string objectId)> GetBIM360UploadUrlAndObjectIdAsync()
        {
            dynamic oAuth = await GetInternalAsync();
            var objectId = await GetBIM360UploadObjectId(oAuth);
            var match = Regex.Match(objectId, ".*:.*:(.*)/(.*)");
            var bucketName = match.Groups[1].Value;
            var objectName = match.Groups[2].Value;
            var UploadUrl = $"https://developer.api.autodesk.com/oss/v2/buckets/{bucketName}/objects/{objectName}";
            return (new XrefTreeArgument()
            {
                Verb = Verb.Put,
                Url = UploadUrl,
                Headers = new Dictionary<string, string>()
                {
                    { "Authorization", "Bearer " + oAuth.access_token }
                }
            }, objectId);
        }

        /// <summary>
        /// The CreateVersionFileAsync.
        /// </summary>
        /// <param name="objectId">The objectId<see cref="string"/>.</param>
        /// <returns>The <see cref="Task{string}"/>.</returns>
        public async Task<string> CreateVersionFileAsync(string objectId)
        {

            dynamic oauth = await GetInternalAsync();
            ItemsApi itemsApi = new ItemsApi();
            itemsApi.Configuration.AccessToken = oauth.access_token;      
            var itemBody =  new CreateItem
            (
                new JsonApiVersionJsonapi
                (
                    JsonApiVersionJsonapi.VersionEnum._0
                ),
                new CreateItemData
                (
                    CreateItemData.TypeEnum.Items,
                    new CreateItemDataAttributes
                    (
                        DisplayName: Specifications.FILENAME,
                        new BaseAttributesExtensionObject
                        (
                            Type: "items:autodesk.bim360:File",
                            Version: "1.0"
                        )
                    ),
                    new CreateItemDataRelationships
                    (
                        new CreateItemDataRelationshipsTip
                        (
                            new CreateItemDataRelationshipsTipData
                            (
                                CreateItemDataRelationshipsTipData.TypeEnum.Versions,
                                CreateItemDataRelationshipsTipData.IdEnum._1
                            )
                        ),
                        new CreateStorageDataRelationshipsTarget
                        (
                            new StorageRelationshipsTargetData
                            (
                                StorageRelationshipsTargetData.TypeEnum.Folders,
                                Id: Specifications.FOLDERID
                            )
                        )
                    )
                ),
                new List<CreateItemIncluded>
                {
                    new CreateItemIncluded
                    (
                        CreateItemIncluded.TypeEnum.Versions,
                        CreateItemIncluded.IdEnum._1,
                        new CreateStorageDataAttributes
                        (
                            Specifications.FILENAME,
                            new BaseAttributesExtensionObject
                            (
                                Type:"versions:autodesk.bim360:File",
                                Version:"1.0"
                            )
                        ),
                        new CreateItemRelationships(
                            new CreateItemRelationshipsStorage
                            (
                                new CreateItemRelationshipsStorageData
                                (
                                    CreateItemRelationshipsStorageData.TypeEnum.Objects,
                                    objectId
                                )
                            )
                        )
                    )
                }
            );

            string itemId = "";
            try
            {
                DynamicJsonResponse postItemJsonResponse = await itemsApi.PostItemAsync(Specifications.PROJECTID, itemBody);
                var uploadItem = postItemJsonResponse.ToObject<ItemCreated>();
                Console.WriteLine("Attributes of uploaded BIM 360 file");
                Console.WriteLine($"\n\t{uploadItem.Data.Attributes.ToJson()}");
                itemId = uploadItem.Data.Id;
            }
            catch (ApiException ex)
            {
                //we met a conflict
                
                ErrorContent errorContent = JsonConvert.DeserializeObject<ErrorContent>(ex.ErrorContent);
                if(errorContent.Errors?[0].Status == "409")//Conflict
                {
                    //Get ItemId of our file
                    itemId = await GetItemIdAsync(oauth);

                    //Lets create a new version
                    itemId = await UpdateVersionAsync(objectId, oauth, itemId);
                }

            }           
            return itemId;
        }

        private async Task<string> GetItemIdAsync(dynamic oauth)
        {
            FoldersApi foldersApi = new FoldersApi();
            foldersApi.Configuration.AccessToken = oauth.access_token;
            DynamicJsonResponse foldersResponse = await foldersApi.GetFolderContentsAsync(Specifications.PROJECTID,
                                                Specifications.FOLDERID,
                                                filterType: new List<string>() { "items" },
                                                filterExtensionType: new List<string>() { "items:autodesk.bim360:File" });
            IEnumerable<dynamic> itemList = await GetFolderItems(Specifications.PROJECTID, Specifications.FOLDERID, oauth);
            var itemId = itemList.First(item => item.Attributes.DisplayName.Equals(Specifications.FILENAME,
                StringComparison.OrdinalIgnoreCase)).Id;
            return itemId;
        }

        private static async Task<string> UpdateVersionAsync(string objectId, dynamic oauth, string itemId)
        {
            var versionsApi = new VersionsApi();
            versionsApi.Configuration.AccessToken = oauth.access_token;
            var relationships = new CreateVersionRefsRelationships
                                            (
                                                new CreateVersionDataRelationshipsItem
                                                (
                                                    new CreateVersionDataRelationshipsItemData
                                                    (
                                                        CreateVersionDataRelationshipsItemData.TypeEnum.Items,
                                                        itemId
                                                    )
                                                ),
                                                new CreateItemRelationshipsStorage
                                                (
                                                    new CreateItemRelationshipsStorageData
                                                    (
                                                        CreateItemRelationshipsStorageData.TypeEnum.Objects,
                                                        objectId
                                                    )
                                                )
                                            );
            var createVersion = new CreateVersion(
            new JsonApiVersionJsonapi(
                JsonApiVersionJsonapi.VersionEnum._0
                ),
            new CreateVersionData(
                CreateVersionData.TypeEnum.Versions,
                new CreateStorageDataAttributes(
                    Specifications.FILENAME,
                    new BaseAttributesExtensionObject(
                        "versions:autodesk.bim360:File",
                        "1.0",
                        new JsonApiLink(string.Empty),
                        null
                        )
                    ),
                relationships
                )
            );
            dynamic versionResponse = await versionsApi.PostVersionAsync(Specifications.PROJECTID, createVersion);
            itemId = versionResponse.data.id;
            return itemId;
        }

        public async Task<IEnumerable<Item>> GetFolderItems(string projectId, string folderId , dynamic oAuth)
        {
            var foldersApi = new FoldersApi();
            foldersApi.Configuration.AccessToken = oAuth.access_token;
            dynamic folderContents =  await foldersApi.GetFolderContentsAsync(projectId, folderId, filterType: new List<string>() { "items" }, filterExtensionType: new List<string>() { "items:autodesk.bim360:File" });
            return folderContents.data;
        }
    }
    public class Jsonapi
    {
        public string version { get; set; }
    }

    public class Error
    {
        public string Id { get; set; }
        public string Status { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
    }

    public class ErrorContent
    {
        public Jsonapi Jsonapi { get; set; }
        public List<Error> Errors { get; set; }
    }
}
