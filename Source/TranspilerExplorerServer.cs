using System.IO;
using System.Threading;
using Verse;

namespace TranspilerExplorer
{
    public class TranspilerExplorerServer
    {
        private string sitePath;
        private Server server;
        private Thread serverThread;

        public TranspilerExplorerServer(ModContentPack content)
        {
            sitePath = Path.Combine(content.RootDir, "Site");
            StartServer(TranspilerExplorerMod.settings.port);
        }

        public void StartServer(int port)
        {
            server = new Server(
                port,
                sitePath
            );

            serverThread = new Thread(server.Start);
            serverThread.Start();
        }

        public void ChangePort(int port)
        {
            server.Stop();
            serverThread.Join();

            StartServer(TranspilerExplorerMod.settings.port);
        }
    }
}
