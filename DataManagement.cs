using Autodesk.Forge;
using Autodesk.Forge.Core;
using Autodesk.Forge.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UploadUtil
{
    public class DataManagement
    {
        private ForgeConfiguration forgeConfiguration;
        internal static OAuthHandler GetAuthHandler;
        public DataManagement(ForgeConfiguration configuration)
        {
            forgeConfiguration = configuration;
            GetAuthHandler = OAuthHandler.Create(configuration);
        }
        public class TreeNode
        {
            public string ID;            
            public string Text;
            public IList<TreeNode> Children;

        }

        public async Task<TreeNode[]> GetList(dynamic bearer)
        {
            HubsApi hubsApi = new HubsApi();
            hubsApi.Configuration.AccessToken = bearer.access_token;
            IList<TreeNode> hubNodes = new List<TreeNode>();
            var hubsresponse = await hubsApi.GetHubsAsync();
            Hubs hubs = hubsresponse.ToObject<Hubs>();
            foreach(Hub hub in hubs.Data)
            {
                var id = hub.Id;
                if (id.StartsWith('b'))
                {
                    TreeNode item = new TreeNode
                    {
                        ID = hub.Id,
                        Text = hub.Attributes.Name,
                        Children = await GetProjects(hub.Id, bearer)
                    };
                    hubNodes.Add(item);
                    
                }
                else
                {
                    TreeNode item = new TreeNode
                    {
                        ID = hub.Id,
                        Text = hub.Attributes.Name,
                        Children = new List<TreeNode>()
                    };
                    hubNodes.Add(item);
                }
            }

            return hubNodes.ToArray();
        }


        public async static Task<TreeNode[]> GetHubs(dynamic bearer)
        {
                HubsApi hubsApi = new HubsApi();
                hubsApi.Configuration.AccessToken = bearer.access_token;

                IList<TreeNode> hubNodes = new List<TreeNode>();
                var hubs = await hubsApi.GetHubsAsync();
                foreach (KeyValuePair<string, dynamic> hubInfo in new DynamicDictionaryItems(hubs.data))
                {

                TreeNode item = new TreeNode
                {
                    ID = hubInfo.Value.id,
                    Text = hubInfo.Value.attributes.name,
                    Children = await GetProjects(hubInfo.Value.id,bearer)
                };
                
                    hubNodes.Add(item);
                }
                return hubNodes.ToArray();
            
        }

        public async static Task<TreeNode[]> GetProjects(string hubId,dynamic bearer)
        {


                //dynamic userAccessToken = await GetAuthHandler.GetInternalAsync();

                IList<TreeNode> nodes = new List<TreeNode>();

                ProjectsApi projectsApi = new ProjectsApi();
                projectsApi.Configuration.AccessToken = bearer.access_token;
                var projects = await projectsApi.GetHubProjectsAsync(hubId);
                foreach (KeyValuePair<string, dynamic> projectInfo in new DynamicDictionaryItems(projects.data))
                {

                    TreeNode projectNode = new TreeNode();
                    projectNode.ID = projectInfo.Value.id;
                    projectNode.Text = projectInfo.Value.attributes.name;
                    projectNode.Children = await GetTopFolder(hubId, projectInfo.Value.id,bearer);
                    nodes.Add(projectNode);

                
                }
                return nodes.ToArray();
            
        }

        public async static Task<TreeNode[]> GetTopFolder(string hubId, string projectId,dynamic bearer)
        {
           // dynamic userAccessToken = await GetAuthHandler.GetInternalAsync();

            IList<TreeNode> nodes = new List<TreeNode>();

            ProjectsApi projectsApi = new ProjectsApi();
            projectsApi.Configuration.AccessToken = bearer.access_token;
            var folders = await projectsApi.GetProjectTopFoldersAsync(hubId, projectId);
            foreach (KeyValuePair<string, dynamic> folderInfo in new DynamicDictionaryItems(folders.data))
            {
                TreeNode projectNode = new TreeNode();
                projectNode.ID = folderInfo.Value.id;
                projectNode.Text = folderInfo.Value.attributes.displayName;
                projectNode.Children = await GetFolderContents(projectId, folderInfo.Value.id,bearer);
                nodes.Add(projectNode);
            }

            return nodes.ToArray();
        }

        public async static Task<TreeNode[]> GetFolderContents(string projectId, string folderId, dynamic bearer)
        {
           // dynamic userAccessToken = await GetAuthHandler.GetInternalAsync();

            IList<TreeNode> folderItems = new List<TreeNode>();

            FoldersApi folderApi = new FoldersApi();
            folderApi.Configuration.AccessToken = bearer.access_token;
            var folderContents = await folderApi.GetFolderContentsAsync(projectId, folderId);
            foreach (KeyValuePair<string, dynamic> folderContentItem in new DynamicDictionaryItems(folderContents.data))
            {
                string displayName = folderContentItem.Value.attributes.displayName;
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                TreeNode itemNode = new TreeNode
                {
                    ID = folderContentItem.Value.id,
                    Text = displayName
                };
                itemNode.Children = await GetItemVersions(projectId, folderContentItem.Value.id, bearer);
                folderItems.Add(itemNode);
            }

            return folderItems.ToArray();
        }

        public async static Task<TreeNode[]> GetItemVersions(string projectId, string itemId, dynamic bearer)
        {
           // dynamic userAccessToken = await GetAuthHandler.GetInternalAsync();

            IList<TreeNode> itemList = new List<TreeNode>();
            var foldersApi = new FoldersApi();
            foldersApi.Configuration.AccessToken = bearer.access_token;
            try
            {
                DynamicJsonResponse folderContents = await foldersApi.GetFolderContentsAsync(projectId,
                                                      itemId,
                                                      filterType: new List<string>() { "items" },
                                                      filterExtensionType: new List<string>() { "items:autodesk.bim360:File" });
                var items = folderContents.ToObject<Items>();
                foreach (Item item in items.Data)
                {
                    TreeNode itemNode = new TreeNode();
                    itemNode.ID = item.Id;
                    itemNode.Text = item.Attributes.DisplayName;
                    itemNode.Children = new List<TreeNode>();
                    itemList.Add(itemNode);
                    
   
                }
                return itemList.ToArray();
            }
            catch { }              
            

            return itemList.ToArray();

        }
    }
}
