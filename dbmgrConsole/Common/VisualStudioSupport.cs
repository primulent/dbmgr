using System.IO;
using System.Xml;

namespace dbmgr.utilities.common
{
    public static class VisualStudioSupport
    {
        public static void AddMigrationToProjectFile(string destinationBase, string fileBase)
        {
            string codeToAdd = string.Format(
                @"<Content Include=""Database\Deltas\{0}.up""><CopyToOutputDirectory>Always</CopyToOutputDirectory></Content>
<Content Include=""Database\Deltas\{0}.down""><CopyToOutputDirectory>Always</CopyToOutputDirectory></Content>", fileBase);

            // Locate the project file
            foreach (string file in Directory.GetFiles(Path.Combine(destinationBase, "*.csproj")))
            {
                // Open the project file
                XmlDocument x = new XmlDocument();
                x.Load(file);

                // XPath to an ItemGroup

                // Append
            }
        }
    }
}
