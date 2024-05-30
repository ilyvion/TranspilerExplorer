using System.IO;
using System.Threading;
using Verse;

namespace TranspilerExplorer
{
    public class TranspilerExplorerServer
    {
        readonly Server server;

        public TranspilerExplorerServer(ModContentPack content)
        {
            var sitePath = Path.Combine(content.RootDir, "Site");
            server = new Server(
                TranspilerExplorerMod.settings.port,
                sitePath
            );

            new Thread(server.Start).Start();
        }
    }
}
