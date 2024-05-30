using HarmonyLib;
using ICSharpCode.Decompiler.IL;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace TranspilerExplorer
{
    [HotSwappable]
    public class Server
    {
        public HttpListener listener;
        public bool running = true;

        private int port;
        private string contentFolder;
        private List<(MethodBase o, MethodInfo t, bool err)> transpilerList;
        private List<(MethodBase o, MethodInfo s, MethodInfo t, bool err)> reverseTranspiledList;
        private Dictionary<MethodBase, string[]> variableNames = new Dictionary<MethodBase, string[]>();

        public Server(int port, string contentFolder)
        {
            this.port = port;
            this.contentFolder = contentFolder;
        }

        public void Start()
        {
            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            HandleRequests();

            listener.Close();
        }

        private void HandleRequests()
        {
            while (running)
            {
                var ctx = listener.GetContext();
                HandleRequest(ctx);
                ctx.Response.Close();
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var cmd = req.Url.PathPart(0);

            try
            {
                _ = ctx.Request.HttpMethod switch
                {
                    "POST" => cmd switch
                    {
                        "stop" => ServeStop(ctx),
                        "newlist" => ServeNewList(ctx),
                        "decomp" => ServeCode(ctx, DecompileWithPostProcessing),
                        "disasm" => ServeCode(ctx, Decompiler.Disasm),
                        _ => false
                    },
                    "GET" => cmd switch
                    {
                        "alive" => ServeAlive(ctx),
                        _ => ServeFile(ctx)
                    },
                    _ => false
                };
            }
            catch (Exception e)
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.WriteJson(JObject.FromObject(new { exception = e.ToString() }));
            }
        }

        private bool ServeFile(HttpListenerContext ctx)
        {
            var reqPath = ctx.Request.Url.AbsolutePath;
            var path = contentFolder + (reqPath == "/" ? "/index.html" : reqPath);

            if (!File.Exists(path))
            {
                ctx.Response.StatusCode = 404;
                return false;
            }

            ctx.Response.WriteBytes(File.ReadAllBytes(path));

            return true;
        }

        private bool ServeNewList(HttpListenerContext ctx)
        {
            transpilerList = new();

            foreach (var m in Harmony.GetAllPatchedMethods())
                foreach (var tr in Harmony.GetPatchInfo(m).Transpilers)
                    transpilerList.Add((m, tr.PatchMethod, false));

            foreach (var e in HarmonyPatch_UpdateWrapper_Patch.erroringTranspilers)
                transpilerList.Add((e.Item1, e.Item2, true));

            reverseTranspiledList = [];
            foreach (var (original, standin, transpiler, erroring) in HarmonyPatch_Register_ReversePatches_Patch.reverseTranspilers)
                reverseTranspiledList.Add((original, standin, transpiler, erroring));

            ctx.Response.WriteJson(JObject.FromObject(new
            {
                transpilers = from p in transpilerList
                              select new
                              {
                                  original = $"{p.o.DeclaringType?.FullDescription()}::{p.o.Name}",
                                  transpiler = $"{p.t.DeclaringType?.FullDescription()}::{p.t.Name}",
                                  erroring = p.err
                              },
                reverseTranspiled = from p in reverseTranspiledList
                                    select new
                                    {
                                        original = $"{p.o.DeclaringType?.FullDescription()}::{p.o.Name}",
                                        reverse = $"{p.s.DeclaringType?.FullDescription()}::{p.s.Name}",
                                        erroring = p.err,
                                    }
            }
            ));

            return true;
        }

        private bool ServeCode(HttpListenerContext ctx, Func<MethodBase, MethodInfo, MethodInfo, string> processor)
        {
            string idPathPart = ctx.Request.Url.PathPart(1);
            string idKind = idPathPart.Substring(0, idPathPart.IndexOf('-'));
            if (!int.TryParse(idPathPart.Substring(idPathPart.IndexOf('-') + 1), out var id))
                return false;

            switch (idKind)
            {
                case "tr":
                    var type = ctx.Request.Url.PathPart(2);
                    var tr = transpilerList[id];
                    string code;

                    try
                    {
                        code = processor(tr.o, null, type == "original" ? null : tr.t);
                    }
                    catch (Exception e)
                    {
                        ctx.Response.StatusCode = 500;
                        ctx.Response.WriteJson(JObject.FromObject(new { exception = e.ToString() }));

                        return false;
                    }

                    ctx.Response.WriteJson(JObject.FromObject(new { code }));

                    return true;

                case "rev":
                    type = ctx.Request.Url.PathPart(2);
                    var rev = reverseTranspiledList[id];

                    try
                    {
                        code = processor(rev.o, type == "original" ? null : rev.s, type == "original" ? null : rev.t);
                    }
                    catch (Exception e)
                    {
                        ctx.Response.StatusCode = 500;
                        ctx.Response.WriteJson(JObject.FromObject(new { exception = e.ToString() }));

                        return false;
                    }

                    ctx.Response.WriteJson(JObject.FromObject(new { code }));

                    return true;

                default:
                    ctx.Response.StatusCode = 400;
                    ctx.Response.WriteJson(JObject.FromObject(new { exception = "Invalid id" }));
                    return false;
            }
        }

        private string DecompileWithPostProcessing(MethodBase orig, MethodInfo standin, MethodInfo transpiler)
        {
            // If getting the original code, save the variable names assigned by the decompiler
            if (transpiler == null)
                return Decompiler.Decompile(orig, standin, transpiler, d =>
                {
                    variableNames[orig] = VariableNames.CollectVarNames(d);
                }, null);

            // Otherwise use the previously saved names for the given original method (names might be null)
            variableNames.TryGetValue(orig, out var names);

            // The decompiler changes similar looking variable names which end in a number.
            // The appended string prevents that. It's stripped after decompilation finishes.
            const string appended = "_0___1_";
            var namesCopy = names?.Select(s => s == null ? null : (s + appended)).ToArray();

            return
                Decompiler.Decompile(orig, standin, transpiler, null, new VariableNameProvider(namesCopy))
                .Replace(appended, "");
        }

        private bool ServeStop(HttpListenerContext ctx)
        {
            running = false;
            return false;
        }

        private bool ServeAlive(HttpListenerContext ctx)
        {
            return true;
        }
    }
}
