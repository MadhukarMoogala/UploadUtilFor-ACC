using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UploadUtil
{
    public class PrintManager
    {
        private const string _cross = " ├─";
        private const string _corner = " └─";
        private const string _vertical = " │ ";
        private const string _space = "   ";
        public static void PrintNode(DataManagement.TreeNode node, string indent)
        {
            Console.WriteLine(node.Text);

            // Loop through the children recursively, passing in the
            // indent, and the isLast parameter
            var numberOfChildren = node.Children.Count;
            for (var i = 0; i < numberOfChildren; i++)
            {
                var child = node.Children[i];
                var isLast = (i == (numberOfChildren - 1));
                PrintChildNode(child, indent, isLast);
            }
        }

        static void PrintChildNode(DataManagement.TreeNode node, string indent, bool isLast)
        {
            // Print the provided pipes/spaces indent
            Console.Write(indent);

            // Depending if this node is a last child, print the
            // corner or cross, and calculate the indent that will
            // be passed to its children
            if (isLast)
            {
                Console.Write(_corner);
                indent += _space;
            }
            else
            {
                Console.Write(_cross);
                indent += _vertical;
            }

            PrintNode(node, indent);
        }
    }
}
