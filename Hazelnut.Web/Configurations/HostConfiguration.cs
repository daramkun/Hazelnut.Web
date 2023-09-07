using System.Runtime.InteropServices;
using Hazelnut.Web.Authorizes;
using Hazelnut.Web.Handler;
using Hazelnut.Web.Providers;

namespace Hazelnut.Web.Configurations;

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct HostConfiguration
{
    public ushort TargetPort;
    public string[] TargetHosts;

    private RequestHandlerTree _requestHandlerTree;

    public ErrorPageProvider ErrorPageProvider;

    public HostConfiguration(ushort targetPort, string[] targetHosts, params IRequestHandler[] requestHandlers)
    {
        TargetPort = targetPort;
        TargetHosts = targetHosts;

        _requestHandlerTree = new RequestHandlerTree(requestHandlers);

        ErrorPageProvider = ErrorPageProvider.Default;
    }

    public IRequestHandler? FindRequestHandler(string queryString) =>
        _requestHandlerTree.FindRequestHandler(queryString);

    private class RequestHandlerTree
    {
        private readonly TreeNode _rootNode;

        public RequestHandlerTree(IEnumerable<IRequestHandler> requestHandlers)
        {
            _rootNode = new TreeNode("/");
            
            foreach (var requestHandler in requestHandlers.OrderBy(requestHandler => requestHandler.Location))
            {
                if (requestHandler.Location == "/")
                {
                    _rootNode.RequestHandler = requestHandler;
                    continue;
                }
                
                var location = requestHandler.Location.Split('/');
                var currentNode = _rootNode;
                for (var i = 1; i < location.Length; ++i)
                {
                    var nextNode = currentNode.Children.FirstOrDefault(child => child.Node.Equals(location[i], StringComparison.OrdinalIgnoreCase));
                    if (nextNode == null)
                    {
                        nextNode = new TreeNode(location[i]);
                        currentNode.Children.Add(nextNode);
                    }

                    currentNode = nextNode;
                }

                currentNode.RequestHandler = requestHandler;
            }
        }

        public IRequestHandler? FindRequestHandler(string queryString)
        {
            if (queryString == "/")
                return _rootNode.RequestHandler;

            var location = queryString.Split('/');
            var currentNode = _rootNode;
            for (var i = 1; i < location.Length; ++i)
            {
                var nextNode = currentNode.Children.FirstOrDefault(child => child.Node.Equals(location[i], StringComparison.OrdinalIgnoreCase));
                if (nextNode == null)
                    return currentNode.RequestHandler;
                currentNode = nextNode;
            }
            
            return currentNode.RequestHandler;
        }
        
        private sealed class TreeNode
        {
            public readonly string Node;
            public IRequestHandler? RequestHandler;
            public readonly List<TreeNode> Children;

            public TreeNode(string node)
            {
                Node = node;
                Children = new List<TreeNode>();
            }
        }
    }
}